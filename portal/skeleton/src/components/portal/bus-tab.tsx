'use client';

/**
 * Вкладка «Схема шины»: карта взаимодействия модулей игры через центральную
 * шину (EventBus + DI). Данные — снимок portal/data/bus_map.json,
 * генерируется по требованию (tools/gen_bus_map.py), портал только читает.
 *
 * Состав: мета-строка снимка → фильтры слоёв → радиальная схема →
 * панель деталей узла (с поиском) → проверка полноты (6 сверок).
 */

import { useMemo, useState } from 'react';
import { CalendarClock, GitBranch, RotateCcw, Search, Waypoints } from 'lucide-react';

import { Input } from '@/components/ui/input';
import type { BusMapFile, BusNode } from '@/lib/portal/types';
import { formatUpdated, thinScrollClass } from '@/lib/portal/helpers';
import { BusChecksSection } from './bus-checks';
import { BusDiagram } from './bus-diagram';
import { BusNodePanel } from './bus-node-panel';

type LayerFilter = 'module' | 'entry' | 'adapter' | 'debug';

const FILTER_LABELS: Record<LayerFilter, string> = {
  module: 'Модули',
  entry: 'Entry',
  adapter: 'Adapter',
  debug: 'QA-харнессы',
};

function layerOf(node: BusNode): LayerFilter {
  if (node.debug) return 'debug';
  if (node.kind === 'module') return 'module';
  if (node.kind === 'entry') return 'entry';
  return 'adapter';
}

export function BusTab({ busMap }: { busMap: BusMapFile }) {
  const [visible, setVisible] = useState<Set<LayerFilter>>(
    new Set(['module', 'entry', 'adapter', 'debug']),
  );
  const [selectedId, setSelectedId] = useState<string>('Core');
  const [hoverId, setHoverId] = useState<string | null>(null);
  const [query, setQuery] = useState('');

  const nodes = busMap.nodes;
  const visibleNodes = useMemo(
    () => nodes.filter((n) => visible.has(layerOf(n))),
    [nodes, visible],
  );
  const counts = useMemo(() => {
    const acc: Record<LayerFilter, number> = { module: 0, entry: 0, adapter: 0, debug: 0 };
    for (const n of nodes) acc[layerOf(n)] += 1;
    return acc;
  }, [nodes]);

  const selectedNode = selectedId === 'Core' ? null : (nodes.find((n) => n.id === selectedId) ?? null);
  const totals = busMap.totals;

  const toggle = (f: LayerFilter) => {
    setVisible((prev) => {
      const next = new Set(prev);
      if (next.has(f)) {
        if (next.size > 1) next.delete(f); // хотя бы один слой виден
      } else {
        next.add(f);
      }
      return next;
    });
  };

  const select = (id: string) => {
    setSelectedId((prev) => (prev === id ? 'Core' : id));
    setQuery('');
  };

  return (
    <div className="flex flex-col gap-5 pt-4">
      {/* мета-строка снимка */}
      <div className="flex flex-wrap items-baseline gap-x-4 gap-y-1.5">
        <p className="flex items-center gap-2 text-xs font-semibold uppercase tracking-[0.2em] text-stone-500">
          <Waypoints aria-hidden="true" className="size-4 text-amber-400" />
          Схема взаимодействия модулей
        </p>
        <span className="font-mono text-[11px] text-stone-500">
          {totals.modules} модулей · {totals.contracts_defined} контрактов ·{' '}
          {totals.services_provided} сервисов
        </span>
        {busMap.updated ? (
          <span
            className="inline-flex items-center gap-1 font-mono text-[11px] text-stone-600"
            title={`Политика обновления: ${busMap.update_policy ?? 'по требованию'}`}
          >
            <CalendarClock aria-hidden="true" className="size-3" />
            снимок: {formatUpdated(busMap.updated)}
          </span>
        ) : null}
        {busMap.source_head ? (
          <span className="inline-flex items-center gap-1 font-mono text-[11px] text-stone-600">
            <GitBranch aria-hidden="true" className="size-3" />
            {busMap.source_head}
          </span>
        ) : null}
      </div>

      {/* фильтры слоёв */}
      <div className="flex flex-wrap gap-2" role="group" aria-label="Слои схемы">
        {(Object.keys(FILTER_LABELS) as LayerFilter[]).map((f) => {
          const on = visible.has(f);
          return (
            <button
              key={f}
              type="button"
              onClick={() => toggle(f)}
              aria-pressed={on}
              className={`rounded-full border px-3 py-1 text-xs font-medium transition-colors ${
                on
                  ? 'border-amber-500/50 bg-amber-500/10 text-amber-200'
                  : 'border-stone-700/80 bg-zinc-900/60 text-stone-500 hover:text-stone-300'
              }`}
            >
              {FILTER_LABELS[f]}
              <span className="ml-1.5 font-mono text-[10px] text-stone-500">{counts[f]}</span>
            </button>
          );
        })}
        <span className="ml-auto hidden items-center gap-1.5 text-[11px] text-stone-600 sm:flex">
          <RotateCcw aria-hidden="true" className="size-3" />
          клик по узлу — детали; повторный клик — хаб
        </span>
      </div>

      {/* схема */}
      <div className="overflow-x-auto rounded-lg border border-stone-800/70 bg-zinc-950/80 p-2 sm:p-3">
        <div className="min-w-[640px]">
          <BusDiagram
            nodes={visibleNodes}
            selectedId={selectedId}
            hoverId={hoverId}
            onHover={setHoverId}
            onSelect={select}
          />
        </div>
      </div>

      {/* чипы узлов — компактный селектор (удобно на мобильных) */}
      <div
        className={`flex max-h-24 flex-wrap gap-1.5 overflow-y-auto pr-1 ${thinScrollClass}`}
        role="group"
        aria-label="Быстрый выбор узла"
      >
        {visibleNodes.map((n) => (
          <button
            key={n.id}
            type="button"
            onClick={() => select(n.id)}
            onMouseEnter={() => setHoverId(n.id)}
            onMouseLeave={() => setHoverId(null)}
            className={`rounded border px-2 py-0.5 font-mono text-[11px] transition-colors ${
              selectedId === n.id
                ? 'border-amber-400/70 bg-amber-500/10 text-amber-200'
                : n.debug
                  ? 'border-stone-800 bg-zinc-950/60 text-stone-500 hover:text-stone-300'
                  : 'border-stone-700/80 bg-zinc-900/60 text-stone-300 hover:border-stone-500'
            }`}
            title={`${n.publishes.length}pub / ${n.subscribes.length}sub / ${n.provides.length}prov / ${n.requires.length}req`}
          >
            {n.id}
          </button>
        ))}
      </div>

      {/* панель деталей */}
      <div className="flex flex-col gap-3 rounded-lg border border-stone-800/70 bg-zinc-900/30 p-4">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <p className="text-[10px] font-semibold uppercase tracking-[0.2em] text-stone-600">
            {selectedNode ? `Ресурсы узла · ${selectedNode.id}` : 'Центральная шина · сводка'}
          </p>
          <div className="relative w-full max-w-xs">
            <Search
              aria-hidden="true"
              className="pointer-events-none absolute left-2.5 top-1/2 size-3.5 -translate-y-1/2 text-stone-600"
            />
            <Input
              value={query}
              onChange={(e) => setQuery(e.target.value)}
              placeholder="Поиск события / сервиса…"
              aria-label="Поиск по ресурсам узла"
              className="h-8 border-stone-700/80 bg-zinc-950/70 pl-8 text-xs text-stone-200 placeholder:text-stone-600"
            />
          </div>
        </div>
        <BusNodePanel busMap={busMap} node={selectedNode} query={query.trim()} />
      </div>

      {/* проверка полноты */}
      <BusChecksSection checks={busMap.checks} />

      {/* примечание об обновлении */}
      <p className="text-[11px] leading-relaxed text-stone-600">
        Снимок не живой: карта генерируется из кода по требованию —{' '}
        <code className="rounded bg-zinc-900/80 px-1 py-0.5 text-stone-400">
          python3 tools/gen_bus_map.py
        </code>{' '}
        (детектор дрейфа —{' '}
        <code className="rounded bg-zinc-900/80 px-1 py-0.5 text-stone-400">--check</code>
        ), коммитится в git вместе с{' '}
        <code className="rounded bg-zinc-900/80 px-1 py-0.5 text-stone-400">
          portal/data/bus_map.json
        </code>
        . Издатели/подписанты QA-харнессов исключены из продакшн-проверок, чтобы
        тестовые слушатели не маскировали осиротевшие события.
      </p>
    </div>
  );
}
