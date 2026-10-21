'use client';

/**
 * Панель деталей выбранного узла шины: четыре списка «ресурсов» —
 * что узел ОТДАЁТ в шину (Publish / Register) и что ЗАПРАШИВАЕТ
 * (Subscribe / Resolve+[Inject]). Для хаба — сводка каналов и групп контрактов.
 *
 * Списки скроллятся (max-h-96, тонкий скроллбар), поиск фильтрует по подстроке.
 */

import { useMemo } from 'react';
import {
  ArrowDownToLine,
  ArrowUpFromLine,
  Cable,
  Plug,
  Puzzle,
  Search,
} from 'lucide-react';

import type { BusMapFile, BusNode } from '@/lib/portal/types';
import { thinScrollClass } from '@/lib/portal/helpers';

/* ── подкомпоненты ─────────────────────────────────────────────────────── */

function ListCard({
  title,
  icon,
  accent,
  empty,
  children,
  count,
}: {
  title: string;
  icon: React.ReactNode;
  accent: 'amber' | 'emerald';
  empty: string;
  children: React.ReactNode;
  count: number;
}) {
  const headColor = accent === 'amber' ? 'text-amber-400' : 'text-emerald-400';
  const borderColor = accent === 'amber' ? 'border-amber-500/25' : 'border-emerald-500/25';
  return (
    <div className={`rounded-lg border ${borderColor} bg-zinc-900/40 p-3`}>
      <p className={`flex items-center gap-1.5 text-[10px] font-semibold uppercase tracking-[0.18em] ${headColor}`}>
        {icon}
        {title}
        <span className="ml-auto font-mono text-stone-500">{count}</span>
      </p>
      {count === 0 ? (
        <p className="mt-2 text-xs text-stone-600">{empty}</p>
      ) : (
        <div className={`mt-2 flex max-h-96 flex-col gap-1 overflow-y-auto pr-1 ${thinScrollClass}`}>
          {children}
        </div>
      )}
    </div>
  );
}

function ItemRow({ label, note, badge }: { label: string; note?: string; badge?: string }) {
  return (
    <div className="flex items-baseline gap-2 rounded border border-stone-800/60 bg-zinc-950/60 px-2 py-1">
      <code className="min-w-0 break-all text-[11px] leading-relaxed text-stone-300">{label}</code>
      {badge ? (
        <span className="ml-auto shrink-0 rounded border border-stone-700 bg-zinc-900 px-1 font-mono text-[9px] uppercase text-stone-500">
          {badge}
        </span>
      ) : null}
      {note ? <span className="sr-only">{note}</span> : null}
    </div>
  );
}

function filterList<T>(items: T[], q: string, key: (t: T) => string): T[] {
  if (!q) return items;
  const needle = q.toLowerCase();
  return items.filter((x) => key(x).toLowerCase().includes(needle));
}

/* ── хаб ───────────────────────────────────────────────────────────────── */

function HubPanel({ busMap }: { busMap: BusMapFile }) {
  const hub = busMap.hub;
  const events = hub?.channels.find((c) => c.id === 'events');
  const services = hub?.channels.find((c) => c.id === 'services');
  const groups = events?.groups ?? [];

  return (
    <div className="flex flex-col gap-4">
      <p className="text-sm leading-relaxed text-stone-300">
        Центральная шина — единственный канал межмодульного взаимодействия
        (Hub-and-Spoke). Выберите узел на схеме, чтобы увидеть, какие{' '}
        <span className="text-amber-300">ресурсы он отдаёт</span> в шину и какие{' '}
        <span className="text-emerald-300">запрашивает</span> из неё.
      </p>

      <div className="grid gap-3 sm:grid-cols-2">
        {events ? (
          <div className="rounded-lg border border-amber-500/30 bg-amber-500/[0.05] p-3.5">
            <p className="flex items-center gap-2 text-xs font-semibold text-amber-300">
              <Cable aria-hidden="true" className="size-4" />
              Канал 1 — события
            </p>
            <p className="mt-1.5 font-mono text-[11px] text-stone-400">{events.title}</p>
            <p className="mt-1 text-xs leading-relaxed text-stone-400">{events.role}</p>
            <p className="mt-2 font-mono text-lg tabular-nums text-amber-300">
              {events.contracts_total ?? '—'}
              <span className="ml-1 text-[10px] uppercase tracking-wider text-stone-500">
                контрактов
              </span>
            </p>
            <div className="mt-2.5 flex flex-wrap gap-1">
              {groups.map((g) => (
                <span
                  key={g.file}
                  title={`${g.file}Contracts — ${g.count} контрактов`}
                  className="rounded border border-stone-700/80 bg-zinc-900 px-1.5 py-0.5 font-mono text-[10px] text-stone-400"
                >
                  {g.file}·{g.count}
                </span>
              ))}
            </div>
          </div>
        ) : null}

        {services ? (
          <div className="rounded-lg border border-emerald-500/30 bg-emerald-500/[0.05] p-3.5">
            <p className="flex items-center gap-2 text-xs font-semibold text-emerald-300">
              <Plug aria-hidden="true" className="size-4" />
              Канал 2 — сервисы
            </p>
            <p className="mt-1.5 font-mono text-[11px] text-stone-400">{services.title}</p>
            <p className="mt-1 text-xs leading-relaxed text-stone-400">{services.role}</p>
            <p className="mt-2 font-mono text-lg tabular-nums text-emerald-300">
              {services.services_total ?? '—'}
              <span className="ml-1 text-[10px] uppercase tracking-wider text-stone-500">
                сервисов
              </span>
            </p>
          </div>
        ) : null}
      </div>

      {/* легенда */}
      <div className="rounded-lg border border-stone-800/70 bg-zinc-900/40 p-3.5">
        <p className="flex items-center gap-2 text-[10px] font-semibold uppercase tracking-[0.2em] text-stone-500">
          <Puzzle aria-hidden="true" className="size-3.5" />
          Как читать схему
        </p>
        <ul className="mt-2 grid gap-1.5 text-xs text-stone-400 sm:grid-cols-2">
          <li className="flex items-center gap-2">
            <span aria-hidden="true" className="inline-block h-0.5 w-6 bg-amber-400" />→ шина:
            отдаёт события (Publish)
          </li>
          <li className="flex items-center gap-2">
            <span aria-hidden="true" className="inline-block h-0.5 w-6 bg-emerald-400" />← шина:
            забирает события (Subscribe)
          </li>
          <li className="flex items-center gap-2">
            <span
              aria-hidden="true"
              className="inline-block h-0.5 w-6 border-t-2 border-dashed border-amber-400"
            />
            → DI: предоставляет сервисы (Register)
          </li>
          <li className="flex items-center gap-2">
            <span
              aria-hidden="true"
              className="inline-block h-0.5 w-6 border-t-2 border-dashed border-emerald-400"
            />
            ← DI: запрашивает сервисы (Resolve / [Inject] / ctor)
          </li>
        </ul>
        <p className="mt-2 text-[11px] text-stone-600">
          Толщина ребра ~ объёму трафика. Пунктирные узлы — QA-харнессы, их
          подписки не считаются продакшн-потреблением.
        </p>
      </div>
    </div>
  );
}

/* ── узел ───────────────────────────────────────────────────────────────── */

function NodePanel({ node, query }: { node: BusNode; query: string }) {
  const publishes = useMemo(
    () => filterList(node.publishes, query, (x) => x),
    [node.publishes, query],
  );
  const subscribes = useMemo(
    () => filterList(node.subscribes, query, (x) => x),
    [node.subscribes, query],
  );
  const provides = useMemo(
    () => filterList(node.provides, query, (p) => `${p.service} ${p.impl}`),
    [node.provides, query],
  );
  const requires = useMemo(
    () => filterList(node.requires, query, (r) => r.service),
    [node.requires, query],
  );

  return (
    <div className="grid gap-3 lg:grid-cols-2">
      <ListCard
        title="Публикует → шина"
        icon={<ArrowUpFromLine aria-hidden="true" className="size-3.5" />}
        accent="amber"
        count={publishes.length}
        empty="События не публикует."
      >
        {publishes.map((ev) => {
          const files = node.publishes_files?.[ev] ?? [];
          return (
            <ItemRow
              key={ev}
              label={ev}
              note={files.length ? `файлы: ${files.join(', ')}` : undefined}
              badge={files.length > 1 ? `${files.length}ф` : undefined}
            />
          );
        })}
      </ListCard>

      <ListCard
        title="Подписан ← шина"
        icon={<ArrowDownToLine aria-hidden="true" className="size-3.5" />}
        accent="emerald"
        count={subscribes.length}
        empty="События не слушает."
      >
        {subscribes.map((ev) => {
          const files = node.subscribes_files?.[ev] ?? [];
          return (
            <ItemRow
              key={ev}
              label={ev}
              note={files.length ? `файлы: ${files.join(', ')}` : undefined}
              badge={files.length > 1 ? `${files.length}ф` : undefined}
            />
          );
        })}
      </ListCard>

      <ListCard
        title="Предоставляет → DI"
        icon={<Plug aria-hidden="true" className="size-3.5" />}
        accent="amber"
        count={provides.length}
        empty="Сервисы не регистрирует."
      >
        {provides.map((p) => (
          <ItemRow
            key={`${p.service}-${p.impl}`}
            label={`${p.service} → ${p.impl}`}
            badge={p.kind === 'config' ? 'cfg' : p.kind === 'self' ? 'self' : p.kind === 'instance' ? 'inst' : undefined}
          />
        ))}
      </ListCard>

      <ListCard
        title="Запрашивает ← DI"
        icon={<Cable aria-hidden="true" className="size-3.5" />}
        accent="emerald"
        count={requires.length}
        empty="Сервисы не запрашивает."
      >
        {requires.map((r) => (
          <ItemRow
            key={r.service}
            label={r.service}
            badge={r.kind === 'config' ? 'cfg' : r.via.join('+')}
          />
        ))}
      </ListCard>
    </div>
  );
}

/* ── экспорт ────────────────────────────────────────────────────────────── */

export interface BusNodePanelProps {
  busMap: BusMapFile;
  node: BusNode | null; // null → хаб
  query: string;
}

export function BusNodePanel({ busMap, node, query }: BusNodePanelProps) {
  if (!node) return <HubPanel busMap={busMap} />;
  return (
    <div className="flex flex-col gap-3.5">
      <div className="flex flex-wrap items-baseline gap-x-3 gap-y-1">
        <h4 className="font-serif text-lg font-semibold text-stone-100">{node.id}</h4>
        <span className="rounded border border-stone-700 bg-zinc-900 px-1.5 py-0.5 font-mono text-[10px] uppercase text-stone-400">
          {node.layer} · {node.kind}
        </span>
        <span className="text-xs text-stone-500">
          {node.description} · файлов: {node.files}
        </span>
      </div>
      <NodePanel node={node} query={query} />
    </div>
  );
}
