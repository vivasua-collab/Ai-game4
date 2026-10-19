'use client';

/**
 * Дорожка вех M0–M7: горизонтальная лента сегментов.
 * done=emerald, in_progress=amber, not_started=zinc-контур.
 * Hover → tooltip: название + окно + саммари. Подписи M-номеров под лентой.
 */

import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import { formatWindow } from '@/lib/portal/helpers';
import type { Milestone } from '@/lib/portal/types';
import { milestoneMeta } from './status-meta';

export function MilestoneStrip({ milestones }: { milestones: Milestone[] }) {
  if (!milestones.length) return null;

  return (
    <section aria-label="Вехи проекта M0–M7" className="mt-6">
      <div className="flex items-center justify-between gap-3">
        <h2 className="text-xs font-semibold uppercase tracking-[0.2em] text-stone-500">
          Вехи M0–M7
        </h2>
        <div className="flex items-center gap-3 text-[10px] text-stone-600">
          <span className="inline-flex items-center gap-1.5">
            <span aria-hidden="true" className="size-2 rounded-sm bg-emerald-500/80" />
            выполнена
          </span>
          <span className="inline-flex items-center gap-1.5">
            <span aria-hidden="true" className="size-2 rounded-sm bg-amber-500" />
            в работе
          </span>
          <span className="inline-flex items-center gap-1.5">
            <span aria-hidden="true" className="size-2 rounded-sm border border-zinc-600 bg-zinc-950" />
            не начата
          </span>
        </div>
      </div>

      <div className="mt-2.5 flex items-stretch gap-1 sm:gap-1.5">
        {milestones.map((m) => {
          const meta = milestoneMeta(m.status);
          return (
            <Tooltip key={m.id}>
              <TooltipTrigger asChild>
                <div
                  role="img"
                  aria-label={`${m.id}: ${m.title} — ${meta.label}`}
                  className="group flex min-w-0 flex-1 cursor-help flex-col focus-visible:outline-none"
                >
                  <div
                    className={`h-6 rounded border transition-colors sm:h-7 ${meta.segment}`}
                  />
                  <span className="mt-1 truncate text-center font-mono text-[10px] text-stone-500 group-hover:text-stone-300 sm:text-[11px]">
                    {m.id}
                  </span>
                </div>
              </TooltipTrigger>
              <TooltipContent side="bottom" className="max-w-[300px] border border-stone-700/70 bg-zinc-900 text-stone-100">
                <p className="font-medium text-amber-300">
                  {m.id} · {m.title}
                </p>
                <p className="mt-0.5 text-[11px] text-stone-400">
                  {meta.label} · {formatWindow(m.window)}
                </p>
                <p className="mt-1 whitespace-normal text-xs leading-snug text-stone-300">
                  {m.summary}
                </p>
              </TooltipContent>
            </Tooltip>
          );
        })}
      </div>
    </section>
  );
}
