'use client';

/**
 * Карточка находки аудита: id, серьёзность, тип, статус, волна исправления,
 * саммари, раскрываемые «Анализ»/«План», мини-бар прогресса и вертикальный
 * таймлайн истории. Расшифровки серьёзности/типа/статуса — из bugs.json
 * (format.*), в коде не хардкодятся.
 */

import { ChevronRight, FileText, MapPin, UserRound } from 'lucide-react';

import {
  Collapsible,
  CollapsibleContent,
  CollapsibleTrigger,
} from '@/components/ui/collapsible';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import { baseName } from '@/lib/portal/helpers';
import type { BugItem } from '@/lib/portal/types';
import { bugStatusMeta, bugTypeMeta, severityMeta, WAVE_CHIP } from './status-meta';

interface BugCardProps {
  bug: BugItem;
  format: {
    severity?: Record<string, string>;
    type?: Record<string, string>;
    item_status?: Record<string, string>;
  };
  repoUrl: string;
}

/** Порог: тексты длиннее считаются «длинными» и сворачиваются по умолчанию. */
const LONG_TEXT = 220;

function ExpandableText({
  title,
  text,
  defaultOpen,
}: {
  title: string;
  text: string;
  defaultOpen: boolean;
}) {
  if (!text) return null;
  return (
    <Collapsible defaultOpen={defaultOpen}>
      <CollapsibleTrigger asChild>
        <button
          type="button"
          className="inline-flex min-h-6 items-center gap-1 rounded text-[11px] font-semibold uppercase tracking-[0.14em] text-stone-500 transition-colors hover:text-amber-300 focus-visible:outline-2 focus-visible:outline-amber-500/70"
        >
          <ChevronRight
            aria-hidden="true"
            className="size-3.5 transition-transform duration-200 [[data-state=open]>&]:rotate-90"
          />
          {title}
        </button>
      </CollapsibleTrigger>
      <CollapsibleContent>
        <p className="mt-1 border-l border-stone-800 pl-2.5 text-xs leading-relaxed text-stone-400">
          {text}
        </p>
      </CollapsibleContent>
    </Collapsible>
  );
}

export function BugCard({ bug, format, repoUrl }: BugCardProps) {
  const sev = severityMeta(bug.severity);
  const type = bugTypeMeta(bug.type);
  const st = bugStatusMeta(bug.status);
  const StatusIcon = st.icon;
  const TypeIcon = type.icon;
  const pct = Math.max(0, Math.min(100, bug.progress_pct ?? 0));
  const history = bug.history ?? [];

  return (
    <article className="flex flex-col gap-2.5 rounded-lg border border-stone-800/70 bg-zinc-900/40 p-4 transition-colors hover:border-stone-700/80">
      {/* Ряд идентификации */}
      <div className="flex flex-wrap items-center gap-1.5">
        <span className="font-mono text-xs font-semibold text-amber-300">{bug.id}</span>
        <Tooltip>
          <TooltipTrigger asChild>
            <span
              className={`inline-flex min-h-6 cursor-help items-center rounded border px-1.5 text-[10px] font-semibold ${sev.badge}`}
            >
              {bug.severity}
            </span>
          </TooltipTrigger>
          <TooltipContent side="top" className="max-w-[260px] border border-stone-700/70 bg-zinc-900 text-stone-100">
            <p className="whitespace-normal text-xs leading-snug">
              {format.severity?.[bug.severity] ?? 'Серьёзность'}
            </p>
          </TooltipContent>
        </Tooltip>
        <Tooltip>
          <TooltipTrigger asChild>
            <span
              className={`inline-flex min-h-6 cursor-help items-center gap-1 rounded border px-1.5 text-[10px] font-medium ${type.badge}`}
            >
              <TypeIcon aria-hidden="true" className="size-3" />
              {type.label}
            </span>
          </TooltipTrigger>
          <TooltipContent side="top" className="max-w-[260px] border border-stone-700/70 bg-zinc-900 text-stone-100">
            <p className="whitespace-normal text-xs leading-snug">
              {format.type?.[bug.type] ?? 'Тип находки'}
            </p>
          </TooltipContent>
        </Tooltip>
        <Tooltip>
          <TooltipTrigger asChild>
            <span
              className={`inline-flex min-h-6 cursor-help items-center gap-1 rounded border px-1.5 text-[10px] font-semibold ${st.badge}`}
            >
              {StatusIcon ? <StatusIcon aria-hidden="true" className="size-3" /> : null}
              {st.label}
            </span>
          </TooltipTrigger>
          <TooltipContent side="top" className="max-w-[280px] border border-stone-700/70 bg-zinc-900 text-stone-100">
            <p className="whitespace-normal text-xs leading-snug">
              {format.item_status?.[bug.status] ?? 'Статус'}
            </p>
          </TooltipContent>
        </Tooltip>
        <span
          className={`ml-auto inline-flex min-h-6 shrink-0 items-center rounded border px-1.5 font-mono text-[10px] font-semibold ${WAVE_CHIP}`}
          title={format.item_status ? undefined : `Волна исправления ${bug.fix_wave}`}
        >
          Волна {bug.fix_wave}
        </span>
      </div>

      {/* Заголовок и модуль */}
      <h3 className="text-sm font-semibold leading-snug text-stone-100">
        {bug.title}
      </h3>
      <div className="flex flex-wrap items-center gap-x-3 gap-y-1 text-[11px] text-stone-500">
        <span className="truncate">{bug.module}</span>
        {bug.track_ref ? (
          <span
            className="inline-flex items-center gap-1 rounded border border-stone-700/70 bg-zinc-950/50 px-1.5 py-px font-mono text-[10px] text-stone-400"
            title={`Этап карты проекта: ${bug.track_ref}`}
          >
            <MapPin aria-hidden="true" className="size-3 shrink-0" />
            {bug.track_ref}
          </span>
        ) : null}
        {bug.assignee ? (
          <span className="inline-flex items-center gap-1 font-mono text-[10px] text-stone-500">
            <UserRound aria-hidden="true" className="size-3 shrink-0" />
            {bug.assignee}
          </span>
        ) : null}
      </div>

      {/* Саммари */}
      <p className="text-xs leading-relaxed text-stone-300">{bug.summary}</p>

      {/* Анализ и план */}
      {bug.analysis ? (
        <ExpandableText
          title="Анализ"
          text={bug.analysis}
          defaultOpen={bug.analysis.length <= LONG_TEXT}
        />
      ) : null}
      {bug.fix_plan ? (
        <ExpandableText
          title="План"
          text={bug.fix_plan}
          defaultOpen={bug.fix_plan.length <= LONG_TEXT}
        />
      ) : null}

      {/* Прогресс исправления */}
      <div className="flex items-center gap-2.5">
        <span className="text-[10px] font-semibold uppercase tracking-[0.14em] text-stone-600">
          Прогресс
        </span>
        <div
          role="progressbar"
          aria-valuenow={pct}
          aria-valuemin={0}
          aria-valuemax={100}
          aria-label={`Прогресс исправления ${bug.id}`}
          className="h-1.5 flex-1 overflow-hidden rounded-full bg-zinc-800"
        >
          <div
            className="h-full rounded-full bg-gradient-to-r from-amber-600 to-amber-400"
            style={{ width: `${pct}%` }}
          />
        </div>
        <span className="w-8 text-right font-mono text-[11px] tabular-nums text-amber-300">
          {pct}%
        </span>
      </div>

      {/* Таймлайн истории */}
      {history.length ? (
        <div className="relative mt-1 pl-4">
          <span
            aria-hidden="true"
            className="absolute bottom-2 left-[3px] top-2 w-px bg-stone-800"
          />
          <ol className="flex flex-col gap-2">
            {history.map((h, i) => (
              <li key={i} className="relative">
                <span
                  aria-hidden="true"
                  className="absolute -left-4 top-[5px] size-[7px] rounded-full border border-amber-500/70 bg-zinc-950"
                />
                <p className="text-[11px] leading-snug text-stone-400">
                  <span className="font-mono text-stone-500">{h.date}</span>
                  {' — '}
                  {h.event}
                </p>
              </li>
            ))}
          </ol>
        </div>
      ) : null}

      {/* Ссылки-пруфы (refs) */}
      {bug.refs?.length ? (
        <div className="mt-1 flex flex-wrap gap-1.5 border-t border-stone-800/60 pt-2">
          {bug.refs.map((r) => (
            <a
              key={r}
              href={`${repoUrl.replace(/\/$/, '')}/blob/main/${r.replace(/^\//, '')}`}
              target="_blank"
              rel="noreferrer"
              title={r}
              className="inline-flex min-h-6 items-center gap-1 rounded border border-stone-700/80 bg-zinc-950/60 px-1.5 py-0.5 font-mono text-[10px] text-stone-500 transition-colors hover:border-amber-500/50 hover:text-amber-300 focus-visible:outline-2 focus-visible:outline-amber-500/70"
            >
              <FileText aria-hidden="true" className="size-3 shrink-0" />
              {baseName(r)}
            </a>
          ))}
        </div>
      ) : null}
    </article>
  );
}
