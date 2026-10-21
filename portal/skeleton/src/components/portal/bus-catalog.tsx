'use client';

/**
 * Каталог контрактов центральной шины (хаб-вид панели): ПОИМЁННО все события
 * и все сервисы — кто кладёт ресурс в шину (Publish / Register) и кто его
 * забирает (Subscribe / Resolve+[Inject]). Отвечает на вопрос владельца
 * «покажи глубже, а не только количества».
 *
 * Данные — производная от снимка bus_map.json (nodes[] + checks[]), без
 * изменения формата: каталог агрегируется на клиенте.
 *
 * Режимы: События (EventBus) | Сервисы (DI). Фильтр «только проблемы»,
 * поиск по имени контракта и по участникам. Проблемные записи — кверху.
 */

import { useMemo, useState } from 'react';
import { Archive, CircleX, Info, TriangleAlert } from 'lucide-react';

import type { BusMapFile } from '@/lib/portal/types';
import { thinScrollClass } from '@/lib/portal/helpers';

/* ── агрегация снимка ───────────────────────────────────────────────────── */

interface FlowSide {
  node: string;
  files: string[];
}

type EventStatus = 'ok' | 'noPublisher' | 'noSubscriber' | 'dead';

interface EventEntry {
  name: string;
  pubs: FlowSide[];
  subs: FlowSide[];
  status: EventStatus;
  debugOnly: boolean; // слушают только QA-харнессы
}

type ServiceStatus = 'ok' | 'unconsumed' | 'byImpl' | 'collection';

interface ServiceEntry {
  name: string;
  providers: Array<{ node: string; impl: string; kind: string }>;
  consumers: Array<{ node: string; via: string[]; kind: string }>;
  status: ServiceStatus;
}

const EVENT_RANK: Record<EventStatus, number> = {
  noPublisher: 0,
  noSubscriber: 1,
  dead: 2,
  ok: 3,
};

const SERVICE_RANK: Record<ServiceStatus, number> = {
  unconsumed: 0,
  ok: 1,
  byImpl: 2,
  collection: 3,
};

function buildEventCatalog(m: BusMapFile): EventEntry[] {
  const map = new Map<string, EventEntry>();
  const get = (name: string): EventEntry => {
    let e = map.get(name);
    if (!e) {
      e = { name, pubs: [], subs: [], status: 'ok', debugOnly: false };
      map.set(name, e);
    }
    return e;
  };

  for (const node of m.nodes) {
    for (const ev of node.publishes) {
      get(ev).pubs.push({ node: node.id, files: node.publishes_files?.[ev] ?? [] });
    }
    for (const ev of node.subscribes) {
      get(ev).subs.push({ node: node.id, files: node.subscribes_files?.[ev] ?? [] });
    }
  }
  for (const c of m.checks.events_no_publisher) get(c.event).status = 'noPublisher';
  for (const c of m.checks.events_no_subscriber) {
    const e = get(c.event);
    e.status = 'noSubscriber';
    e.debugOnly = c.debug_only ?? false;
  }
  for (const name of m.checks.contracts_unused) get(name).status = 'dead';

  return [...map.values()].sort(
    (a, b) => EVENT_RANK[a.status] - EVENT_RANK[b.status] || a.name.localeCompare(b.name),
  );
}

function buildServiceCatalog(m: BusMapFile): ServiceEntry[] {
  const map = new Map<string, ServiceEntry>();
  const get = (name: string): ServiceEntry => {
    let e = map.get(name);
    if (!e) {
      e = { name, providers: [], consumers: [], status: 'ok' };
      map.set(name, e);
    }
    return e;
  };
  /** impl-имя → provides-записи с этой реализацией (резолв по конкретному типу). */
  const byImpl = new Map<string, Array<{ node: string; impl: string; kind: string }>>();

  for (const node of m.nodes) {
    for (const p of node.provides) {
      get(p.service).providers.push({ node: node.id, impl: p.impl, kind: p.kind });
      if (p.impl !== p.service) {
        const list = byImpl.get(p.impl) ?? [];
        list.push({ node: node.id, impl: p.impl, kind: p.kind });
        byImpl.set(p.impl, list);
      }
    }
    for (const r of node.requires) {
      get(r.service).consumers.push({ node: node.id, via: r.via, kind: r.kind });
    }
  }

  for (const e of map.values()) {
    if (e.providers.length === 0) {
      const viaImpl = byImpl.get(e.name);
      if (viaImpl && viaImpl.length > 0) {
        e.status = 'byImpl';
        e.providers = viaImpl;
      } else {
        e.status = 'collection'; // ResolveAll-интерфейсы (IStartable, ITickable…)
      }
    }
  }
  for (const c of m.checks.services_unconsumed) {
    const e = map.get(c.service);
    if (e) e.status = 'unconsumed';
  }

  return [...map.values()].sort(
    (a, b) => SERVICE_RANK[a.status] - SERVICE_RANK[b.status] || a.name.localeCompare(b.name),
  );
}

/* ── строки каталога ────────────────────────────────────────────────────── */

function Chip({ text, title, tone }: { text: string; title: string; tone: 'pub' | 'sub' }) {
  return (
    <span
      title={title}
      className={`rounded border px-1 font-mono text-[10px] ${
        tone === 'pub'
          ? 'border-amber-500/20 bg-amber-500/[0.07] text-amber-200/90'
          : 'border-emerald-500/20 bg-emerald-500/[0.07] text-emerald-200/90'
      }`}
    >
      {text}
    </span>
  );
}

function StatusBadge({ text, tone }: { text: string; tone: 'red' | 'amber' | 'gray' }) {
  const cls =
    tone === 'red'
      ? 'border-red-500/40 bg-red-500/10 text-red-300'
      : tone === 'amber'
        ? 'border-amber-500/40 bg-amber-500/10 text-amber-300'
        : 'border-stone-700 bg-zinc-900 text-stone-500';
  return (
    <span className={`rounded border px-1 font-mono text-[9px] uppercase tracking-wide ${cls}`}>
      {text}
    </span>
  );
}

function EventRow({ e }: { e: EventEntry }) {
  return (
    <div className="rounded-md border border-stone-800/60 bg-zinc-950/60 px-2.5 py-1.5">
      <div className="flex flex-wrap items-baseline gap-x-2 gap-y-0.5">
        {e.status === 'noPublisher' ? (
          <CircleX aria-hidden="true" className="size-3 shrink-0 self-center text-red-400" />
        ) : e.status === 'noSubscriber' ? (
          <TriangleAlert aria-hidden="true" className="size-3 shrink-0 self-center text-amber-400" />
        ) : e.status === 'dead' ? (
          <Archive aria-hidden="true" className="size-3 shrink-0 self-center text-stone-500" />
        ) : null}
        <code className="min-w-0 break-all text-[11px] leading-relaxed text-stone-200">{e.name}</code>
        <span className="ml-auto flex shrink-0 flex-wrap gap-1">
          {e.status === 'noPublisher' ? <StatusBadge text="нет издателя" tone="red" /> : null}
          {e.status === 'noSubscriber' ? (
            <>
              <StatusBadge text="нет прод-подписчиков" tone="amber" />
              {e.debugOnly ? <StatusBadge text="только QA-слушатели" tone="gray" /> : null}
            </>
          ) : null}
          {e.status === 'dead' ? <StatusBadge text="мёртвый контракт" tone="gray" /> : null}
        </span>
      </div>

      {e.status !== 'dead' ? (
        <div className="mt-1 flex flex-wrap items-center gap-x-1.5 gap-y-1 pl-0.5 text-[10px]">
          <span aria-hidden="true" className="font-mono text-amber-400/80">→</span>
          {e.pubs.length > 0 ? (
            e.pubs.map((p) => (
              <Chip
                key={`p-${p.node}`}
                text={p.node}
                title={`${p.node}: ${p.files.join(', ') || '—'}`}
                tone="pub"
              />
            ))
          ) : (
            <span className="text-red-400/80">никто не публикует</span>
          )}
          <span aria-hidden="true" className="ml-1 font-mono text-emerald-400/80">←</span>
          {e.subs.length > 0 ? (
            e.subs.map((s) => (
              <Chip
                key={`s-${s.node}`}
                text={s.node}
                title={`${s.node}: ${s.files.join(', ') || '—'}`}
                tone="sub"
              />
            ))
          ) : (
            <span className="text-amber-400/80">никто не слушает</span>
          )}
        </div>
      ) : (
        <p className="mt-0.5 pl-0.5 text-[10px] text-stone-600">
          объявлен в Core/Messaging/Contracts, не публикуется и не слушается
        </p>
      )}
    </div>
  );
}

function ServiceRow({ s }: { s: ServiceEntry }) {
  return (
    <div className="rounded-md border border-stone-800/60 bg-zinc-950/60 px-2.5 py-1.5">
      <div className="flex flex-wrap items-baseline gap-x-2 gap-y-0.5">
        {s.status === 'unconsumed' ? (
          <TriangleAlert aria-hidden="true" className="size-3 shrink-0 self-center text-amber-400" />
        ) : s.status === 'byImpl' || s.status === 'collection' ? (
          <Info aria-hidden="true" className="size-3 shrink-0 self-center text-stone-500" />
        ) : null}
        <code className="min-w-0 break-all text-[11px] leading-relaxed text-stone-200">{s.name}</code>
        <span className="ml-auto flex shrink-0 flex-wrap gap-1">
          {s.status === 'unconsumed' ? (
            <StatusBadge text="мёртвый регистр" tone="amber" />
          ) : null}
          {s.status === 'byImpl' ? <StatusBadge text="по конкретному типу" tone="gray" /> : null}
          {s.status === 'collection' ? <StatusBadge text="ResolveAll" tone="gray" /> : null}
        </span>
      </div>

      <div className="mt-1 flex flex-wrap items-center gap-x-1.5 gap-y-1 pl-0.5 text-[10px]">
        <span aria-hidden="true" className="font-mono text-amber-400/80">→</span>
        {s.providers.length > 0 ? (
          s.providers.map((p, i) => (
            <Chip
              key={`pr-${p.node}-${i}`}
              text={p.impl !== s.name ? `${p.node}·${p.impl}` : p.node}
              title={`${p.node}: ${p.impl} (${p.kind})`}
              tone="pub"
            />
          ))
        ) : (
          <span className="text-stone-600">нет регистрации под этим именем</span>
        )}
        <span aria-hidden="true" className="ml-1 font-mono text-emerald-400/80">←</span>
        {s.consumers.length > 0 ? (
          s.consumers.map((c, i) => (
            <Chip
              key={`cn-${c.node}-${i}`}
              text={c.node}
              title={`${c.node}: via ${c.via.join('+') || c.kind}`}
              tone="sub"
            />
          ))
        ) : s.status === 'unconsumed' ? (
          <span className="text-amber-400/80">никто не снимает с шины</span>
        ) : (
          <span
            className="text-stone-600"
            title="Проверка генератора не считает регистрацию мёртвой — потребляется косвенно"
          >
            потребляется косвенно (ResolveAll / по типу)
          </span>
        )}
      </div>
    </div>
  );
}

/* ── каталог ────────────────────────────────────────────────────────────── */

type Mode = 'events' | 'services';

export function BusCatalog({ busMap, query }: { busMap: BusMapFile; query: string }) {
  const [mode, setMode] = useState<Mode>('events');
  const [onlyProblems, setOnlyProblems] = useState(false);

  const events = useMemo(() => buildEventCatalog(busMap), [busMap]);
  const services = useMemo(() => buildServiceCatalog(busMap), [busMap]);

  const needle = query.trim().toLowerCase();

  const filteredEvents = useMemo(() => {
    const src = onlyProblems ? events.filter((e) => e.status !== 'ok') : events;
    if (!needle) return src;
    return src.filter(
      (e) =>
        e.name.toLowerCase().includes(needle) ||
        e.pubs.some((p) => p.node.toLowerCase().includes(needle)) ||
        e.subs.some((s) => s.node.toLowerCase().includes(needle)),
    );
  }, [events, onlyProblems, needle]);

  const filteredServices = useMemo(() => {
    const src = onlyProblems
      ? services.filter((s) => s.status === 'unconsumed')
      : services;
    if (!needle) return src;
    return src.filter(
      (s) =>
        s.name.toLowerCase().includes(needle) ||
        s.providers.some(
          (p) =>
            p.node.toLowerCase().includes(needle) || p.impl.toLowerCase().includes(needle),
        ) ||
        s.consumers.some((c) => c.node.toLowerCase().includes(needle)),
    );
  }, [services, onlyProblems, needle]);

  const list = mode === 'events' ? filteredEvents : filteredServices;

  const eventProblems = events.filter((e) => e.status !== 'ok').length;
  const serviceProblems = services.filter((s) => s.status === 'unconsumed').length;
  const problems = mode === 'events' ? eventProblems : serviceProblems;
  const total = mode === 'events' ? events.length : services.length;

  return (
    <div className="rounded-lg border border-stone-800/70 bg-zinc-900/30 p-3.5">
      {/* заголовок + режимы */}
      <div className="flex flex-wrap items-center gap-2">
        <p className="flex items-center gap-1.5 text-[10px] font-semibold uppercase tracking-[0.2em] text-stone-500">
          Каталог контрактов шины
        </p>
        <div
          role="tablist"
          aria-label="Режим каталога"
          className="flex rounded-lg border border-stone-800 bg-zinc-900/70 p-0.5"
        >
          {(
            [
              ['events', `События·${events.length}`],
              ['services', `Сервисы·${services.length}`],
            ] as Array<[Mode, string]>
          ).map(([m, label]) => (
            <button
              key={m}
              type="button"
              role="tab"
              aria-selected={mode === m}
              onClick={() => setMode(m)}
              className={`rounded-md px-2.5 py-1 font-mono text-[11px] transition-colors ${
                mode === m
                  ? 'bg-zinc-800 text-stone-50'
                  : 'text-stone-500 hover:text-stone-300'
              }`}
            >
              {label}
            </button>
          ))}
        </div>
        <button
          type="button"
          onClick={() => setOnlyProblems((v) => !v)}
          aria-pressed={onlyProblems}
          className={`rounded-full border px-2.5 py-1 text-[11px] font-medium transition-colors ${
            onlyProblems
              ? 'border-amber-500/50 bg-amber-500/10 text-amber-200'
              : 'border-stone-700/80 bg-zinc-900/60 text-stone-500 hover:text-stone-300'
          }`}
        >
          ⚠ только проблемы
          <span className="ml-1 font-mono text-[10px] text-stone-500">{problems}</span>
        </button>
        <span className="ml-auto font-mono text-[10px] text-stone-600">
          показано {list.length} из {total}
        </span>
      </div>

      <p className="mt-2 text-[11px] leading-relaxed text-stone-500">
        {mode === 'events' ? (
          <>
            Каждое событие: <span className="text-amber-300">→</span> кто публикует (файлы в
            подсказках), <span className="text-emerald-300">←</span> кто слушает. Проблемные —
            кверху: красный «нет издателя», янтарный «нет прод-подписчиков», серый — мёртвый
            контракт.
          </>
        ) : (
          <>
            Каждый сервис: <span className="text-amber-300">→</span> кто региструет и какая
            реализация, <span className="text-emerald-300">←</span> кто снимает с шины
            (Resolve/[Inject]). Включая конфиги, self-регистрации, резолв по конкретному типу и
            ResolveAll-коллекции.
          </>
        )}
      </p>

      {/* список */}
      <div
        className={`mt-2.5 flex max-h-[26rem] flex-col gap-1 overflow-y-auto pr-1 ${thinScrollClass}`}
      >
        {list.length === 0 ? (
          <p className="py-3 text-center text-xs text-stone-600">
            {needle ? `Ничего не найдено по «${query.trim()}»` : 'Список пуст'}
          </p>
        ) : mode === 'events' ? (
          filteredEvents.map((e) => <EventRow key={e.name} e={e} />)
        ) : (
          filteredServices.map((s) => <ServiceRow key={s.name} s={s} />)
        )}
      </div>
    </div>
  );
}
