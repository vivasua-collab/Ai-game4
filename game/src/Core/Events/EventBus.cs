#nullable enable
using System;
using System.Collections.Generic;

namespace CultivationGame.Core.Events;

/// <summary>
/// Handler delegate for messages of type <typeparamref name="T"/>. The
/// <c>in</c> parameter passes the readonly struct by reference (zero-GC,
/// no copy of large structs).
/// </summary>
public delegate void MessageHandler<T>(in T message) where T : struct;

/// <summary>
/// Publishes messages of type <typeparamref name="T"/>. All messages are
/// <c>readonly struct</c> — passed by <c>in</c> for zero GC.
/// </summary>
public interface IPublisher<T> where T : struct
{
    void Publish(in T message);
}

/// <summary>
/// Subscribes to messages of type <typeparamref name="T"/>.
/// Returns an <see cref="IDisposable"/> that, when disposed, removes the
/// subscription.
/// </summary>
public interface ISubscriber<T> where T : struct
{
    IDisposable Subscribe(MessageHandler<T> handler);
}

/// <summary>
/// Thread-safe event bus. Per-message-type subscription list, no boxing on
/// the publish path (handlers stored as <see cref="MessageHandler{T}"/> and
/// invoked with <c>in</c>).
///
/// Re-entrancy protection: if a handler publishes a message of the SAME type
/// during its invocation, the re-entrant message is queued and processed after
/// the current publish completes. This prevents StackOverflowException from
/// cascading events (Q13: Queue re-entrant events).
///
/// АУДИТ-1020 (BUG-004; ранее R34 P2-22): защита распространена и на ДРЕН —
/// во время дрена ЛЮБАЯ публикация (того же типа и любых других) откладывается
/// в очередь и обрабатывается тем же итеративным циклом НА ОДНОМ уровне стека
/// (sentinel <c>_draining</c>). Раньше дрен выполнялся ПОСЛЕ снятия типа из
/// <c>_publishing</c>: публикация из queued-хендлера уходила в normal-path, её
/// finally-дрен выполнялся ВЛОЖЕННО, и цепочка drain→InvokeHandlers→handler→
/// Publish→finally-drain→… росла без границы → StackOverflowException
/// (самопубликация одного типа и взаимная публикация A↔B). Аллокационный
/// контракт (P2-9) прежний: дрен — редкий путь, копия очереди живёт только
/// в нём.
///
/// Exception isolation (аудит 09.22, P1-5): исключение одного подписчика НЕ
/// прерывает fan-out (последующие подписчики получают событие) и НЕ покидает
/// <see cref="Publish{T}"/> — иначе один неисправный хендлер роняет уже и
/// вызывающий код, а re-entrant очередь протухает ([ThreadStatic] живёт до
/// следующего Publish) и способна исполниться позже, в чужом логическом
/// контексте.
///
/// Аллокации (честный контракт, аудит 09.22 P2-9): стабильный publish-path
/// (подписки не меняются, re-entrancy нет) — allocation-free; копия snapshot
/// списка хендлеров аллоцируется ТОЛЬКО при первой публикации после мутации
/// подписок (copy-on-read); re-entrant события создают closure на постановку
/// и копию очереди на дрен — редкий путь, приемлемая цена.
/// </summary>
public sealed class EventBus : IDisposable
{
    private readonly object _lock = new();
    private readonly Dictionary<Type, object> _subscriptions = new();

    // Re-entrancy protection: tracks which message types are currently being published.
    // If a handler publishes the same type, it goes to _pendingQueue instead of recursing.
    [ThreadStatic]
    private static HashSet<Type>? _publishing;

    // Queue of deferred re-entrant messages. Each entry is an Action that invokes
    // the handlers with the queued message. Processed when _publishing becomes empty.
    [ThreadStatic]
    private static List<Action>? _pendingQueue;

    // АУДИТ-1020 (BUG-004; ранее R34 P2-22): sentinel «идёт дрен отложенной
    // очереди». Пока он выставлен, ЛЮБАЯ публикация на этом потоке уходит в
    // _pendingQueue и обрабатывается тем же итеративным циклом дрена —
    // queued-хендлер не может породить вложенный Publish/дрен (прежний
    // источник безграничного роста стека → StackOverflowException).
    [ThreadStatic]
    private static bool _draining;

    /// <summary>
    /// Publish a message to all subscribers of type <typeparamref name="T"/>.
    /// Re-entrant publishes are queued and processed after current publish
    /// completes. Исключения подписчиков изолируются (лог + продолжение
    /// fan-out) — наружу не выходят.
    /// </summary>
    public void Publish<T>(in T message) where T : struct
    {
        _publishing ??= new HashSet<Type>();
        _pendingQueue ??= new List<Action>();

        // Check for re-entrancy: if this type is already being published — or the
        // deferred queue is being DRAINED right now (АУДИТ-1020, BUG-004) — queue
        // it; queued publishes are processed iteratively by the drain loop,
        // never nested. This allocates a closure (Action<T>), but re-entrancy
        // is rare — acceptable cost.
        if (_publishing.Contains(typeof(T)) || _draining)
        {
            T msgCopy = message; // copy for closure
            _pendingQueue.Add(() => InvokeHandlers(msgCopy));
            return;
        }

        // Normal publish path
        _publishing.Add(typeof(T));
        try
        {
            InvokeHandlers(in message);
        }
        finally
        {
            _publishing.Remove(typeof(T));

            // P1-5 (аудит 09.22): дрен — в finally, на внешнем уровне.
            // Прежний дрен ПОСЛЕ try/finally был недостижим при исключении в
            // хендлере: очередь [ThreadStatic] протухала до следующего
            // Publish на потоке — отложенное событие исполнялось в чужом
            // контексте (runtime-подтверждено: стэл-дрен рушил посторонний
            // Publish). Каждый queued-вызов тоже изолирован (InvokeHandlers
            // ловит исключения per-handler).
            //
            // АУДИТ-1020 (BUG-004; ранее R34 P2-22): дрен итеративный, на
            // ОДНОМ уровне стека, под сентинелом _draining. Публикация
            // ЛЮБОГО типа из queued-хендлера (см. условие постановки в
            // Publish) откладывается в ту же очередь и подбирается ЭТИМ
            // циклом — вложенных Publish/дренов не возникает. Прежний код
            // дренил уже ПОСЛЕ Remove из _publishing: queued-хендлер
            // публиковал тот же/другой тип в normal-path, его finally-дрен
            // выполнялся ВЛОЖЕННО → drain→handler→Publish→finally-drain→…
            // → безграничный рост стека → StackOverflowException.
            if (!_draining && _publishing.Count == 0 && _pendingQueue.Count > 0)
            {
                _draining = true;
                try
                {
                    while (_pendingQueue.Count > 0)
                    {
                        var queue = new List<Action>(_pendingQueue);
                        _pendingQueue.Clear();
                        for (int i = 0; i < queue.Count; i++)
                        {
                            try { queue[i](); }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[EventBus] Queued re-entrant handler threw: {ex.GetType().Name}: {ex.Message}");
                            }
                        }
                    }
                }
                finally { _draining = false; }
            }
        }
    }

    /// <summary>Invoke handlers for type T (internal, used by Publish and queue).
    /// P1-5 (аудит 09.22): пер-хендлерная изоляция — исключение подписчика
    /// логируется и НЕ прерывает доставку остальным и НЕ покидает шину;
    /// вызывающий код (publisher) не должен падать от неисправного слушателя.</summary>
    private void InvokeHandlers<T>(in T message) where T : struct
    {
        List<MessageHandler<T>>? handlers;
        lock (_lock)
        {
            if (!_subscriptions.TryGetValue(typeof(T), out var bucket)) return;
            handlers = ((SubscriptionList<T>)bucket).GetSnapshot();
        }
        if (handlers is null) return;
        // Invoke outside the lock to avoid reentrancy deadlock.
        for (int i = 0; i < handlers.Count; i++)
        {
            try { handlers[i](in message); }
            catch (Exception ex)
            {
                Console.WriteLine($"[EventBus] Handler for {typeof(T).Name} threw: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    public IDisposable Subscribe<T>(MessageHandler<T> handler) where T : struct
    {
        if (handler is null) throw new ArgumentNullException(nameof(handler));
        SubscriptionList<T> list;
        lock (_lock)
        {
            if (!_subscriptions.TryGetValue(typeof(T), out var bucket))
            {
                list = new SubscriptionList<T>();
                _subscriptions[typeof(T)] = list;
            }
            else
            {
                list = (SubscriptionList<T>)bucket;
            }
            list.Add(handler);
        }
        return new UnsubscribeToken<T>(this, handler);
    }

    /// <summary>Number of active subscribers for a given message type.</summary>
    public int SubscriberCount<T>() where T : struct
    {
        lock (_lock)
        {
            if (!_subscriptions.TryGetValue(typeof(T), out var bucket)) return 0;
            return ((SubscriptionList<T>)bucket).Count;
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _subscriptions.Clear();
        }
    }

    private void Unsubscribe<T>(MessageHandler<T> handler) where T : struct
    {
        lock (_lock)
        {
            if (!_subscriptions.TryGetValue(typeof(T), out var bucket)) return;
            var list = (SubscriptionList<T>)bucket;
            list.Remove(handler);
            if (list.Count == 0) _subscriptions.Remove(typeof(T));
        }
    }

    /// <summary>
    /// Per-type subscription storage. Holds a List of <see cref="MessageHandler{T}"/>
    /// and produces a snapshot list for invocation (so unsubscribes during
    /// publish don't mutate the iteration).
    /// </summary>
    private sealed class SubscriptionList<T> where T : struct
    {
        private readonly List<MessageHandler<T>> _handlers = new();
        private List<MessageHandler<T>>? _snapshot;

        public int Count => _handlers.Count;

        public void Add(MessageHandler<T> handler)
        {
            _handlers.Add(handler);
            _snapshot = null;
        }

        public void Remove(MessageHandler<T> handler)
        {
            _handlers.Remove(handler);
            _snapshot = null;
        }

        public List<MessageHandler<T>>? GetSnapshot()
        {
            // Cheap copy-on-read: rebuild snapshot only when mutated.
            if (_snapshot is not null) return _snapshot;
            if (_handlers.Count == 0) return null;
            _snapshot = new List<MessageHandler<T>>(_handlers);
            return _snapshot;
        }
    }

    private sealed class UnsubscribeToken<T> : IDisposable where T : struct
    {
        private readonly EventBus _bus;
        private readonly MessageHandler<T> _handler;
        private bool _disposed;

        public UnsubscribeToken(EventBus bus, MessageHandler<T> handler)
        {
            _bus = bus;
            _handler = handler;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _bus.Unsubscribe(_handler);
        }
    }
}

/// <summary>
/// Convenience adapter that exposes a single <see cref="EventBus"/> instance
/// through the <see cref="IPublisher{T}"/> / <see cref="ISubscriber{T}"/>
/// interfaces (so modules can inject them without depending on the bus class).
/// </summary>
public sealed class EventBusPublisher<T> : IPublisher<T> where T : struct
{
    private readonly EventBus _bus;
    public EventBusPublisher(EventBus bus) { _bus = bus; }
    public void Publish(in T message) => _bus.Publish(in message);
}

public sealed class EventBusSubscriber<T> : ISubscriber<T> where T : struct
{
    private readonly EventBus _bus;
    public EventBusSubscriber(EventBus bus) { _bus = bus; }
    public IDisposable Subscribe(MessageHandler<T> handler) => _bus.Subscribe(handler);
}
