'use client';

/**
 * Вкладка «Плейтест»: чек-лист физического плейтеста владельца
 * (директива 22.10 — до 25.10 физического плейтеста нет, детали
 * должны жить в портале). Данные — portal/data/playtest.json в рантайме;
 * статусы пунктов обновляет main-агент по отчётам владельца.
 *
 * Фильтры: поиск по тексту, приоритет, статус; категории секциями,
 * карточка пункта = шаги (нумерация) + ожидание (галочки) + ссылки-рефы.
 */

import { useMemo, useState } from 'react';
import { CheckCircle2, Gamepad2, Search } from 'lucide-react';

import {
  Collapsible,
  CollapsibleContent,
  CollapsibleTrigger,
} from '@/components/ui/collapsible';
import { Input } from '@/components/ui/input';
import type { PlayTestFile, PlayTestItem } from '@/lib/portal/types';
import { thinScrollClass } from '@/lib/portal/helpers';

interface PlayTestTabProps {
  playtest: PlayTestFile;
}

/* ── Словари статусов/приоритетов (палитра вкладок: zinc/stone + amber) ─── */

const STATUS_META: Record<string, { label: string; badge: string }> = {
  planned: {
    label: 'запланировано',
    badge: 'border-stone-500/60 bg-transparent text-stone-300',
  },
  passed: {
    label: 'проверено',
    badge: 'border-emerald-500/50 bg-emerald-500/10 text-emerald-300',
  },
  failed: {
    label: 'сломано',
    badge: 'border-rose-500/60 bg-rose-500/10 text-rose-300',
  },
  skipped: {
    label: 'отложено',
    badge: 'border-zinc-700 bg-zinc-800/60 text-zinc-400',
  },
};

const PRIORITY_META: Record<string, { label: string; badge: string }> = {
  high: {
    label: 'главный путь',
    badge: 'border-amber-500/70 bg-amber-500/15 text-amber-300',
  },
  normal: {
    label: 'стоит проверить',
    badge: 'border-stone-500/50 bg-transparent text-stone-300',
  },
  optional: {
    label: 'диагностика/читы',
    badge: 'border-zinc-600 bg-zinc-800/40 text-zinc-400',
  },
};

function chip(isActive: boolean): string {
  return (
    'inline-flex min-h-9 items-center gap-1.5 rounded-full border px-3 text-xs font-medium transition-colors focus-visible:outline-2 focus-visible:outline-amber-500/70 ' +
    (isActive
      ? 'border-amber-500/70 bg-amber-500/15 text-amber-300'
      : 'border-stone-800 bg-zinc-900/50 text-stone-400 hover:border-stone-600 hover:text-stone-200')
  );
}

/* ── Карточка пункта ────────────────────────────────────────────────────── */

function ItemCard({ item }: { item: PlayTestItem }) {
  const status = STATUS_META[item.status] ?? {
    label: item.status,
    badge: 'border-zinc-700 text-zinc-300',
  };
  const priority = PRIORITY_META[item.priority] ?? {
    label: item.priority,
    badge: 'border-zinc-600 text-zinc-300',
  };

  return (
    <article
      className="flex flex-col gap-3 rounded-xl border border-stone-800 bg-zinc-900/40 p-4 transition-colors hover:border-stone-700"
      aria-label={`${item.id}: ${item.title}`}
    >
      <header className="flex flex-wrap items-center gap-1.5">
        <span className="rounded border border-stone-700 bg-zinc-950/70 px-1.5 py-0.5 font-mono text-[10px] text-amber-300/90">
          {item.id}
        </span>
        <span
          className={`rounded-full border px-2 py-0.5 text-[10px] font-medium ${status.badge}`}
        >
          {status.label}
        </span>
        <span
          className={`rounded-full border px-2 py-0.5 text-[10px] font-medium ${priority.badge}`}
          title="Приоритет проверки"
        >
          {priority.label}
        </span>
        {(item.refs ?? []).map((ref) => (
          <span
            key={ref}
            title="Ссылка на запись реестра/этап"
            className="rounded-full border border-amber-500/30 bg-amber-500/[0.07] px-2 py-0.5 font-mono text-[10px] text-amber-200/90"
          >
            {ref}
          </span>
        ))}
      </header>

      <h3 className="text-sm font-semibold leading-snug text-stone-100">
        {item.title}
      </h3>

      {item.steps.length > 0 ? (
        <div>
          <p className="mb-1 text-[10px] font-semibold uppercase tracking-[0.16em] text-stone-600">
            Как проверять
          </p>
          <ol className="space-y-1 text-xs leading-relaxed text-stone-300">
            {item.steps.map((step, i) => (
              <li key={i} className="flex gap-2">
                <span className="shrink-0 font-mono text-[10px] text-stone-500">
                  {i + 1}.
                </span>
                <span>{step}</span>
              </li>
            ))}
          </ol>
        </div>
      ) : null}

      {item.expected.length > 0 ? (
        <div>
          <p className="mb-1 text-[10px] font-semibold uppercase tracking-[0.16em] text-stone-600">
            Ожидание
          </p>
          <ul className="space-y-1 text-xs leading-relaxed text-stone-300">
            {item.expected.map((exp, i) => (
              <li key={i} className="flex gap-2">
                <CheckCircle2
                  aria-hidden="true"
                  className="mt-0.5 size-3.5 shrink-0 text-emerald-500/80"
                />
                <span>{exp}</span>
              </li>
            ))}
          </ul>
        </div>
      ) : null}

      {item.note ? (
        <p className="text-xs text-stone-500">{item.note}</p>
      ) : null}
    </article>
  );
}

/* ── Вкладка ────────────────────────────────────────────────────────────── */

export function PlayTestTab({ playtest }: PlayTestTabProps) {
  const [query, setQuery] = useState('');
  const [priority, setPriority] = useState<string>('all');
  const [status, setStatus] = useState<string>('all');

  const categories = playtest.categories ?? [];
  const allItems = useMemo(
    () => categories.flatMap((c) => c.items),
    [categories],
  );

  const counts = useMemo(() => {
    const byStatus: Record<string, number> = {};
    const byPriority: Record<string, number> = {};
    for (const it of allItems) {
      byStatus[it.status] = (byStatus[it.status] ?? 0) + 1;
      byPriority[it.priority] = (byPriority[it.priority] ?? 0) + 1;
    }
    return { byStatus, byPriority };
  }, [allItems]);

  const visible = useMemo(() => {
    const q = query.trim().toLowerCase();
    const matchesItem = (it: PlayTestItem): boolean => {
      const byText =
        q.length === 0 ||
        it.title.toLowerCase().includes(q) ||
        it.id.toLowerCase().includes(q) ||
        (it.refs ?? []).some((r) => r.toLowerCase().includes(q)) ||
        [...it.steps, ...it.expected].some((s) => s.toLowerCase().includes(q));
      const byPriority = priority === 'all' || it.priority === priority;
      const byStatus = status === 'all' || it.status === status;
      return byText && byPriority && byStatus;
    };
    const cats = categories
      .map((cat) => ({
        ...cat,
        items: cat.items.filter((it) => matchesItem(it)),
      }))
      .filter((cat) => cat.items.length > 0);
    const total = cats.reduce((n, c) => n + c.items.length, 0);
    return { cats, total };
  }, [categories, query, priority, status]);

  const statusKeys = ['planned', 'passed', 'failed', 'skipped'].filter(
    (s) => (counts.byStatus[s] ?? 0) > 0,
  );
  const priorityKeys = ['high', 'normal', 'optional'].filter(
    (p) => (counts.byPriority[p] ?? 0) > 0,
  );

  return (
    <div className="pt-4">
      {/* Шапка: окно плейтеста + инструкция */}
      <section
        aria-label="Окно плейтеста"
        className="rounded-xl border border-amber-500/25 bg-amber-500/[0.04] p-4"
      >
        <div className="flex flex-wrap items-center gap-2">
          <Gamepad2 aria-hidden="true" className="size-4 text-amber-400" />
          <h2 className="text-sm font-semibold text-stone-100">
            Чек-лист физического плейтеста
          </h2>
          <span className="font-mono text-[10px] text-stone-500">
            {allItems.length} пунктов · {counts.byPriority.high ?? 0} главный ·{' '}
            {counts.byStatus.planned ?? 0} запланировано
          </span>
          {playtest.window?.target ? (
            <span className="rounded-full border border-amber-500/40 bg-amber-500/10 px-2 py-0.5 text-[10px] font-medium text-amber-300">
              окно: {playtest.window.target}
            </span>
          ) : null}
        </div>
        {playtest.window?.note ? (
          <p className="mt-2 text-xs leading-relaxed text-stone-400">
            {playtest.window.note}
          </p>
        ) : null}

        {(playtest.how ?? []).length > 0 ? (
          <Collapsible>
            <CollapsibleTrigger className="mt-2 text-[11px] font-medium text-stone-400 underline decoration-stone-700 underline-offset-4 hover:text-stone-200">
              Как стартовать / отчёт по пунктам
            </CollapsibleTrigger>
            <CollapsibleContent>
              <ol className="mt-2 max-h-64 space-y-1 overflow-y-auto pr-1 text-xs leading-relaxed text-stone-400">
                {(playtest.how ?? []).map((h, i) => (
                  <li key={i} className="flex gap-2">
                    <span className="shrink-0 font-mono text-[10px] text-amber-400/70">
                      {i + 1}.
                    </span>
                    <span>{h}</span>
                  </li>
                ))}
              </ol>
            </CollapsibleContent>
          </Collapsible>
        ) : null}
      </section>

      {/* Фильтры */}
      <nav aria-label="Фильтры плейтеста" className="mt-4 flex flex-col gap-2.5">
        <div className="flex flex-wrap items-center gap-1.5">
          <span className="mr-1 text-[10px] font-semibold uppercase tracking-[0.16em] text-stone-600">
            Приоритет
          </span>
          <button
            type="button"
            onClick={() => setPriority('all')}
            aria-pressed={priority === 'all'}
            className={chip(priority === 'all')}
          >
            Все
            <span className="font-mono text-[10px] text-stone-500">
              {allItems.length}
            </span>
          </button>
          {priorityKeys.map((p) => (
            <button
              key={p}
              type="button"
              onClick={() => setPriority(priority === p ? 'all' : p)}
              aria-pressed={priority === p}
              className={chip(priority === p)}
              title={playtest.format?.priority?.[p]}
            >
              {PRIORITY_META[p]?.label ?? p}
              <span className="font-mono text-[10px] text-stone-500">
                {counts.byPriority[p] ?? 0}
              </span>
            </button>
          ))}
        </div>
        <div className="flex flex-wrap items-center gap-1.5">
          <span className="mr-1 text-[10px] font-semibold uppercase tracking-[0.16em] text-stone-600">
            Статус
          </span>
          <button
            type="button"
            onClick={() => setStatus('all')}
            aria-pressed={status === 'all'}
            className={chip(status === 'all')}
          >
            Все
          </button>
          {statusKeys.map((s) => (
            <button
              key={s}
              type="button"
              onClick={() => setStatus(status === s ? 'all' : s)}
              aria-pressed={status === s}
              className={chip(status === s)}
              title={playtest.format?.item_status?.[s]}
            >
              {STATUS_META[s]?.label ?? s}
              <span className="font-mono text-[10px] text-stone-500">
                {counts.byStatus[s] ?? 0}
              </span>
            </button>
          ))}
        </div>
        <div className="relative max-w-sm">
          <Search
            aria-hidden="true"
            className="pointer-events-none absolute left-2.5 top-1/2 size-3.5 -translate-y-1/2 text-stone-600"
          />
          <Input
            type="search"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder="Поиск: номер, текст, референс…"
            aria-label="Поиск по пунктам плейтеста"
            className="h-9 max-h-24 border-stone-800 bg-zinc-900/60 pl-8 text-xs text-stone-200 placeholder:text-stone-600 focus-visible:outline-amber-500/70"
          />
        </div>
      </nav>

      {/* Категории */}
      <div className="mt-4 space-y-5">
        {visible.cats.map((cat) => {
          const total = cat.items.length;
          const full =
            categories.find((c) => c.id === cat.id)?.items.length ?? total;
          return (
            <section
              key={cat.id}
              aria-label={cat.title}
              className="rounded-xl border border-stone-800/80 bg-zinc-950/40 p-3"
            >
              <header className="mb-3 flex items-center gap-2 px-1">
                <h3 className="text-xs font-semibold uppercase tracking-[0.14em] text-amber-200/80">
                  {cat.title}
                </h3>
                <span className="font-mono text-[10px] text-stone-500">
                  {total}
                  {total !== full ? `/${full}` : ''}
                </span>
              </header>
              <div className="grid grid-cols-1 gap-3 xl:grid-cols-2">
                {cat.items.map((item) => (
                  <ItemCard key={item.id} item={item} />
                ))}
              </div>
            </section>
          );
        })}
      </div>

      {visible.total === 0 ? (
        <p className="mt-6 text-sm text-stone-500">
          Нет пунктов под выбранные фильтры.
        </p>
      ) : (
        <p className={`mt-4 text-[11px] text-stone-600 ${thinScrollClass}`}>
          Показано {visible.total} из {allItems.length} · отчёт владельца:
          «PT-XX пройден/сломан» — агент обновляет portal/data/playtest.json,
          провалы уходят в реестр аудита.
        </p>
      )}
    </div>
  );
}
