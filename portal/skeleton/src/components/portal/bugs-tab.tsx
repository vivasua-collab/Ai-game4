'use client';

/**
 * Вкладка «Реестр аудита»: фильтры-чипы по волне и статусу,
 * сортировка волна → серьёзность, сетка карточек (1 колонка на мобиле,
 * 2 колонки от xl).
 */

import { useMemo, useState } from 'react';

import { BUG_STATUS_ORDER, sortBugs } from '@/lib/portal/helpers';
import type { BugsFile } from '@/lib/portal/types';
import { BugCard } from './bug-card';
import { bugStatusMeta } from './status-meta';

interface BugsTabProps {
  bugs: BugsFile;
  repoUrl: string;
}

type WaveFilter = number | 'all';

const WAVE_LABELS: Record<number, string> = { 1: 'Волна 1', 2: 'Волна 2', 3: 'Волна 3' };

/** Стиль чипа фильтра. */
function chip(isActive: boolean): string {
  return (
    'inline-flex min-h-9 items-center gap-1.5 rounded-full border px-3 text-xs font-medium transition-colors focus-visible:outline-2 focus-visible:outline-amber-500/70 ' +
    (isActive
      ? 'border-amber-500/70 bg-amber-500/15 text-amber-300'
      : 'border-stone-800 bg-zinc-900/50 text-stone-400 hover:border-stone-600 hover:text-stone-200')
  );
}

export function BugsTab({ bugs, repoUrl }: BugsTabProps) {
  const items = bugs.items ?? [];
  const [wave, setWave] = useState<WaveFilter>('all');
  const [status, setStatus] = useState<string>('all');

  const waves = useMemo(() => {
    const set = new Set<number>();
    for (const b of items) set.add(b.fix_wave);
    return [...set].sort((a, b) => a - b);
  }, [items]);

  const statuses = useMemo(
    () => BUG_STATUS_ORDER.filter((s) => items.some((b) => b.status === s)),
    [items],
  );

  const visible = useMemo(() => {
    const filtered = items.filter(
      (b) =>
        (wave === 'all' || b.fix_wave === wave) &&
        (status === 'all' || b.status === status),
    );
    return sortBugs(filtered);
  }, [items, wave, status]);

  const format = bugs.format ?? {};

  return (
    <div className="pt-4">
      {/* Фильтры */}
      <nav aria-label="Фильтры реестра" className="flex flex-col gap-2.5">
        <div className="flex flex-wrap items-center gap-1.5">
          <span className="mr-1 text-[10px] font-semibold uppercase tracking-[0.16em] text-stone-600">
            Волна
          </span>
          <button
            type="button"
            onClick={() => setWave('all')}
            aria-pressed={wave === 'all'}
            className={chip(wave === 'all')}
          >
            Все
            <span className="font-mono text-[10px] text-stone-500">{items.length}</span>
          </button>
          {waves.map((w) => (
            <button
              key={w}
              type="button"
              onClick={() => setWave(wave === w ? 'all' : w)}
              aria-pressed={wave === w}
              className={chip(wave === w)}
              title={format.fix_wave?.[String(w)]}
            >
              {WAVE_LABELS[w] ?? `Волна ${w}`}
              <span className="font-mono text-[10px] text-stone-500">
                {items.filter((b) => b.fix_wave === w).length}
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
          {statuses.map((s) => {
            const meta = bugStatusMeta(s);
            const n = items.filter((b) => b.status === s).length;
            return (
              <button
                key={s}
                type="button"
                onClick={() => setStatus(status === s ? 'all' : s)}
                aria-pressed={status === s}
                className={chip(status === s)}
                title={format.item_status?.[s]}
              >
                {meta.label}
                <span className="font-mono text-[10px] text-stone-500">{n}</span>
              </button>
            );
          })}
        </div>
      </nav>

      {/* Сетка карточек */}
      <div className="mt-4 grid grid-cols-1 gap-4 xl:grid-cols-2">
        {visible.map((bug) => (
          <BugCard key={bug.id} bug={bug} format={format} repoUrl={repoUrl} />
        ))}
      </div>
      {visible.length === 0 ? (
        <p className="mt-6 text-sm text-stone-500">
          Нет находок под выбранные фильтры.
        </p>
      ) : null}
    </div>
  );
}
