'use client';

/**
 * KPI-полоса: треки, счётчики этапов по статусам и общий прогресс
 * (доля done-этапов от всех, кроме deferred — правило progress_rule
 * из project_map.json). Всё считается на клиенте из tracks.
 */

import { countMilestones, countStages, overallProgress } from '@/lib/portal/helpers';
import type { Milestone, Track } from '@/lib/portal/types';

interface KpiRowProps {
  tracks: Track[];
  milestones: Milestone[];
}

interface KpiBlock {
  label: string;
  value: string;
  valueClass?: string;
  hint?: string;
}

export function KpiRow({ tracks, milestones }: KpiRowProps) {
  const allStages = tracks.flatMap((t) => t.stages ?? []);
  const { total, byStatus } = countStages(allStages);
  const progress = overallProgress(allStages);
  const ms = countMilestones(milestones);
  const msDone = ms.done ?? 0;

  const blocks: KpiBlock[] = [
    {
      label: 'Треков',
      value: String(tracks.length),
      hint: `${msDone} из ${milestones.length} вех выполнены`,
    },
    {
      label: 'Этапов',
      value: String(total),
      hint: 'всего по 29 дорожкам карты',
    },
    {
      label: 'Выполнено',
      value: String(byStatus.done),
      valueClass: 'text-emerald-400',
    },
    {
      label: 'В работе',
      value: String(byStatus.in_progress),
      valueClass: 'text-amber-400',
    },
    {
      label: 'Запланировано',
      value: String(byStatus.planned),
      valueClass: 'text-stone-300',
    },
    {
      label: 'Отложено',
      value: String(byStatus.deferred),
      valueClass: 'text-rose-400',
    },
  ];

  return (
    <section aria-label="Ключевые показатели проекта" className="mt-6">
      <div className="flex flex-wrap gap-2">
        {blocks.map((b) => (
          <div
            key={b.label}
            className="min-w-[104px] flex-1 rounded-lg border border-stone-800/70 bg-zinc-900/40 px-3 py-2"
          >
            <p className="text-[10px] font-medium uppercase tracking-[0.14em] text-stone-500">
              {b.label}
            </p>
            <p className={`mt-0.5 text-xl font-semibold tabular-nums text-stone-100 ${b.valueClass ?? ''}`}>
              {b.value}
            </p>
            {b.hint ? <p className="mt-0.5 text-[10px] text-stone-600">{b.hint}</p> : null}
          </div>
        ))}
        <div className="min-w-[160px] flex-[2] rounded-lg border border-stone-800/70 bg-zinc-900/40 px-3 py-2">
          <p className="text-[10px] font-medium uppercase tracking-[0.14em] text-stone-500">
            Общий прогресс
          </p>
          <div className="mt-1.5 flex items-center gap-2.5">
            <span
              className="text-xl font-semibold tabular-nums text-amber-300"
              aria-label={`Общий прогресс ${progress} процентов`}
            >
              {progress}%
            </span>
            <div
              role="progressbar"
              aria-valuenow={progress}
              aria-valuemin={0}
              aria-valuemax={100}
              aria-label="Доля выполненных этапов"
              className="h-1.5 flex-1 overflow-hidden rounded-full bg-zinc-800"
            >
              <div
                className="h-full rounded-full bg-gradient-to-r from-amber-600 to-amber-400"
                style={{ width: `${progress}%` }}
              />
            </div>
          </div>
          <p className="mt-0.5 text-[10px] text-stone-600">
            доля выполненных этапов, без учёта отложенных
          </p>
        </div>
      </div>
    </section>
  );
}
