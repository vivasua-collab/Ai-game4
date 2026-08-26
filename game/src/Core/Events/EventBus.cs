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

    /// <summary>
    /// Publish a message to all subscribers of type <typeparamref name="T"/>.
    /// Allocations: zero (no boxing, no closure capture if handler is static).
    /// Re-entrant publishes are queued and processed after current publish completes.
    /// </summary>
    public void Publish<T>(in T message) where T : struct
    {
        _publishing ??= new HashSet<Type>();
        _pendingQueue ??= new List<Action>();

        // Check for re-entrancy: if this type is already being published, queue it.
        if (_publishing.Contains(typeof(T)))
        {
            // Queue a deferred invocation. This allocates a closure (Action<T>),
            // but re-entrancy is rare — acceptable cost.
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
        }

        // If this was the outermost publish, process any queued re-entrant messages
        if (_publishing.Count == 0 && _pendingQueue.Count > 0)
        {
            var queue = new List<Action>(_pendingQueue);
            _pendingQueue.Clear();
            foreach (var action in queue)
            {
                action();
            }
        }
    }

    /// <summary>Invoke handlers for type T (internal, used by Publish and queue).</summary>
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
            handlers[i](in message);
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
