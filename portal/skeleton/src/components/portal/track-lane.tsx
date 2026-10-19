'use client';

/**
 * Дорожка-строка «звуковой дорожки»: один трек карты проекта.
 * Шкала этапов — flex-цепочка сегментов (done=emerald, in_progress=amber
 * с пульсацией, planned=zinc-контур, deferred=rose, not_started=ghost).
 * Клик по строке раскрывает Collapsible со списком этапов и пруфами.
 */

import { useState } from 'react';
import { ChevronDown, FileText, Flag } from 'lucide-react';

import {
  Collapsible,
  CollapsibleContent,
  CollapsibleTrigger,
} from '@/components/ui/collapsible';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import {
  baseName,
  countStages,
  formatWindow,
  repoFileUrl,
  thinScrollClass,
} from '@/lib/portal/helpers';
import type { Stage, Track } from '@/lib/portal/types';
import { stageMeta, trackMeta } from './status-meta';

interface TrackLaneProps {
  track: Track;
  /** URL репозитория (для ссылок на чекпоинты-пруфы). */
  repoUrl: string;
}

/* ── Шкала этапов ───────────────────────────────────────────────────────── */

function StageChain({ stages, label }: { stages: Stage[]; label: string }) {
  const { byStatus } = countStages(stages);
  const summary =
    `${stages.length} ${stages.length === 1 ? 'этап' : 'этапов'}: ` +
    `${byStatus.done} выполнено, ${byStatus.in_progress} в работе, ` +
    `${byStatus.planned} в плане, ${byStatus.deferred} отложено`;

  return (
    <div
      className="flex min-w-0 flex-1 items-center gap-[3px] sm:gap-1"
      role="img"
      aria-label={`Шкала этапов «${label}»: ${summary}`}
    >
      {stages.map((s) => {
        const meta = stageMeta(s.status);
        return (
          <Tooltip key={s.id}>
            <TooltipTrigger asChild>
              <span
                className={`h-2.5 min-w-0 flex-1 cursor-help rounded-[3px] transition-transform sm:h-3 ${meta.segment} hover:scale-y-150`}
              />
            </TooltipTrigger>
            <TooltipContent
              side="bottom"
              className="max-w-[280px] border border-stone-700/70 bg-zinc-900 text-stone-100"
            >
              <p className="whitespace-normal text-xs leading-snug">{s.title}</p>
              <p className="mt-0.5 text-[11px] text-stone-400">
                {meta.label} · {formatWindow(s.window)}
              </p>
            </TooltipContent>
          </Tooltip>
        );
      })}
    </div>
  );
}

/* ── Дорожка ────────────────────────────────────────────────────────────── */

export function TrackLane({ track, repoUrl }: TrackLaneProps) {
  const [open, setOpen] = useState(false);
  const meta = trackMeta(track.status);
  const pct = Math.max(0, Math.min(100, track.progress_pct ?? 0));

  return (
    <Collapsible open={open} onOpenChange={setOpen}>
      <div
        className={`overflow-hidden rounded-lg border transition-colors ${
          open
            ? 'border-stone-700 bg-zinc-900/70'
            : 'border-stone-800/70 bg-zinc-900/40 hover:border-stone-700/80 hover:bg-zinc-900/60'
        }`}
      >
        <CollapsibleTrigger asChild>
          <button
            type="button"
            className="block w-full min-h-11 px-3 py-2.5 text-left focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-amber-500/70 sm:px-4"
          >
            <span className="flex items-center gap-2 sm:gap-3">
              <span className="w-6 shrink-0 font-mono text-xs tabular-nums text-stone-500">
                {String(track.number).padStart(2, '0')}
              </span>
              <span className="min-w-0 flex-1 truncate text-sm font-medium text-stone-200">
                {track.title}
              </span>
              <span
                className={`hidden shrink-0 rounded border px-1.5 py-px text-[10px] font-medium md:inline-flex ${meta.badge}`}
                title={`Статус трека: ${meta.label}`}
              >
                {meta.label}
              </span>
              <span className="flex shrink-0 items-center gap-2" aria-hidden="true">
                <span className="text-xs font-mono tabular-nums text-amber-300">
                  {pct}%
                </span>
                <span className="hidden h-1.5 w-16 overflow-hidden rounded-full bg-zinc-800 sm:block sm:w-20">
                  <span
                    className="block h-full rounded-full bg-gradient-to-r from-amber-600 to-amber-400"
                    style={{ width: `${pct}%` }}
                  />
                </span>
              </span>
              <ChevronDown
                className={`size-4 shrink-0 text-stone-500 transition-transform duration-200 ${open ? 'rotate-180 text-amber-400' : ''}`}
                aria-hidden="true"
              />
            </span>
            <span className="mt-2.5 flex items-center gap-2">
              <span
                className="w-[88px] shrink-0 truncate font-mono text-[10px] text-stone-500 sm:w-24"
                title={track.start?.date ? `Начало: ${track.start.date}` : 'Начало не указано'}
              >
                {track.start?.date ?? '—'}
              </span>
              <StageChain stages={track.stages} label={track.title} />
              <Tooltip>
                <TooltipTrigger asChild>
                  <span className="shrink-0 cursor-help" aria-label="Финал трека (DoD)">
                    <Flag
                      className={`size-3.5 transition-colors sm:size-4 ${open ? 'text-amber-400' : 'text-amber-500/60 hover:text-amber-300'}`}
                      aria-hidden="true"
                    />
                  </span>
                </TooltipTrigger>
                <TooltipContent
                  side="bottom"
                  className="max-w-[320px] border border-stone-700/70 bg-zinc-900 text-stone-100"
                >
                  <p className="text-[11px] font-medium text-amber-300">Финал (DoD)</p>
                  <p className="whitespace-normal text-xs leading-snug text-stone-300">
                    {track.final}
                  </p>
                </TooltipContent>
              </Tooltip>
            </span>
          </button>
        </CollapsibleTrigger>

        <CollapsibleContent>
          <div className="border-t border-stone-800/70 px-3 pb-3.5 pt-3 sm:px-4">
            <div className="flex flex-col gap-2 text-xs leading-relaxed">
              <p className="text-stone-400">
                <span className="mr-2 text-[10px] font-semibold uppercase tracking-[0.16em] text-stone-600">
                  Цель
                </span>
                {track.goal}
              </p>
              <p className="flex gap-2 text-stone-400">
                <Flag aria-hidden="true" className="mt-0.5 size-3.5 shrink-0 text-amber-400/80" />
                <span>
                  <span className="mr-2 text-[10px] font-semibold uppercase tracking-[0.16em] text-stone-600">
                    Финал
                  </span>
                  {track.final}
                </span>
              </p>
            </div>

            <ul
              className={`mt-3 max-h-96 list-none flex-col gap-3 overflow-y-auto pl-1 pr-2 ${thinScrollClass} flex`}
              aria-label={`Этапы дорожки «${track.title}»`}
            >
              {track.stages.map((s) => {
                const sm = stageMeta(s.status);
                const Icon = sm.icon;
                return (
                  <li key={s.id} className="flex gap-2.5">
                    <Icon aria-hidden="true" className={`mt-0.5 size-4 shrink-0 ${sm.iconClass}`} />
                    <div className="min-w-0 flex-1">
                      <div className="flex flex-wrap items-baseline gap-x-2 gap-y-0.5">
                        <span className="text-sm leading-snug text-stone-200">{s.title}</span>
                        <span className="font-mono text-[11px] text-stone-500">
                          {formatWindow(s.window)}
                        </span>
                        <span
                          className={`rounded border px-1.5 py-px text-[10px] font-medium ${sm.badge}`}
                        >
                          {sm.label}
                        </span>
                      </div>
                      {s.note ? (
                        <p className="mt-0.5 text-xs leading-snug text-stone-400">{s.note}</p>
                      ) : null}
                      {s.proofs?.length ? (
                        <div className="mt-1.5 flex flex-wrap gap-1.5">
                          {s.proofs.map((p) => (
                            <a
                              key={p}
                              href={repoFileUrl(repoUrl, p)}
                              target="_blank"
                              rel="noreferrer"
                              title={p}
                              className="inline-flex min-h-6 items-center gap-1 rounded border border-stone-700/80 bg-zinc-950/60 px-1.5 py-0.5 font-mono text-[10px] text-stone-400 transition-colors hover:border-amber-500/50 hover:text-amber-300 focus-visible:outline-2 focus-visible:outline-amber-500/70"
                            >
                              <FileText aria-hidden="true" className="size-3 shrink-0" />
                              {baseName(p)}
                            </a>
                          ))}
                        </div>
                      ) : null}
                    </div>
                  </li>
                );
              })}
            </ul>
          </div>
        </CollapsibleContent>
      </div>
    </Collapsible>
  );
}
