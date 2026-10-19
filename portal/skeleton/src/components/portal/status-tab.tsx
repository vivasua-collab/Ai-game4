'use client';

/**
 * Вкладка «Статус»: снимок состояния из portal/data/status.json —
 * крупный заголовок, блок плейтеста (заметная amber-рамка), сборка,
 * фокус (нумерованный список), дальше (список), источники данных.
 */

import {
  CalendarClock,
  Crosshair,
  Database,
  FlaskConical,
  GitBranch,
  ListTodo,
  ShieldCheck,
} from 'lucide-react';

import { Separator } from '@/components/ui/separator';
import type { StatusFile } from '@/lib/portal/types';

export function StatusTab({ status }: { status: StatusFile }) {
  const playtest = status.playtest;
  const build = status.build;
  const focus = status.focus ?? [];
  const next = status.next ?? [];
  const sources = status.portal?.data_sources ?? [];

  return (
    <div className="flex flex-col gap-6 pt-4">
      {/* Заголовок-снимок */}
      <section aria-label="Снимок состояния">
        <p className="text-[10px] font-semibold uppercase tracking-[0.2em] text-stone-600">
          Снимок состояния
        </p>
        <h3 className="mt-1.5 font-serif text-xl font-semibold leading-snug text-stone-100 sm:text-2xl">
          {status.headline ?? '—'}
        </h3>
      </section>

      {/* Плейтест — заметный блок */}
      {playtest?.note || playtest?.window ? (
        <section
          aria-label="Плейтест"
          className="rounded-lg border border-amber-500/40 bg-amber-500/[0.06] p-4"
        >
          <p className="flex items-center gap-2 text-[10px] font-semibold uppercase tracking-[0.2em] text-amber-400">
            <CalendarClock aria-hidden="true" className="size-4" />
            Плейтест
          </p>
          <p className="mt-2 text-sm leading-relaxed text-stone-200">
            {playtest?.note}
          </p>
          {playtest?.window ? (
            <p className="mt-1.5 inline-flex rounded border border-amber-500/30 bg-zinc-950/50 px-2 py-0.5 font-mono text-xs text-amber-300">
              {playtest.window}
            </p>
          ) : null}
        </section>
      ) : null}

      {/* Сборка и QA */}
      {build ? (
        <section
          aria-label="Сборка"
          className="grid gap-4 rounded-lg border border-stone-800/70 bg-zinc-900/40 p-4 sm:grid-cols-2"
        >
          <div>
            <p className="flex items-center gap-2 text-[10px] font-semibold uppercase tracking-[0.2em] text-stone-600">
              <GitBranch aria-hidden="true" className="size-3.5" />
              HEAD
            </p>
            <p className="mt-1.5 font-mono text-sm text-amber-300">{build.head ?? '—'}</p>
            {build.last_verified ? (
              <p className="mt-0.5 text-[11px] text-stone-500">
                верифицирован: {build.last_verified}
              </p>
            ) : null}
          </div>
          <div className="flex flex-col gap-3">
            <div>
              <p className="flex items-center gap-2 text-[10px] font-semibold uppercase tracking-[0.2em] text-stone-600">
                <ShieldCheck aria-hidden="true" className="size-3.5" />
                Сборка
              </p>
              <p className="mt-1 text-sm leading-snug text-stone-300">
                {build.result ?? '—'}
              </p>
            </div>
            <div>
              <p className="flex items-center gap-2 text-[10px] font-semibold uppercase tracking-[0.2em] text-stone-600">
                <FlaskConical aria-hidden="true" className="size-3.5" />
                QA
              </p>
              <p className="mt-1 text-sm leading-snug text-stone-300">
                {build.qa ?? '—'}
              </p>
            </div>
          </div>
        </section>
      ) : null}

      <Separator className="bg-stone-800/70" />

      {/* Фокус — нумерованный список */}
      {focus.length ? (
        <section aria-label="Текущий фокус">
          <p className="flex items-center gap-2 text-xs font-semibold uppercase tracking-[0.2em] text-stone-500">
            <Crosshair aria-hidden="true" className="size-4 text-amber-400" />
            В фокусе
          </p>
          <ol className="mt-2.5 flex flex-col gap-2">
            {focus.map((f, i) => (
              <li key={i} className="flex items-baseline gap-3">
                <span className="font-mono text-xs tabular-nums text-amber-400/90">
                  {String(i + 1).padStart(2, '0')}
                </span>
                <span className="text-sm leading-snug text-stone-300">{f}</span>
              </li>
            ))}
          </ol>
        </section>
      ) : null}

      {/* Дальше — список */}
      {next.length ? (
        <section aria-label="Дальше">
          <p className="flex items-center gap-2 text-xs font-semibold uppercase tracking-[0.2em] text-stone-500">
            <ListTodo aria-hidden="true" className="size-4 text-amber-400" />
            Дальше
          </p>
          <ul className="mt-2.5 flex flex-col gap-2">
            {next.map((n, i) => (
              <li key={i} className="flex items-baseline gap-2.5">
                <span aria-hidden="true" className="mt-1.5 size-1 shrink-0 rounded-full bg-amber-500/80" />
                <span className="text-sm leading-snug text-stone-300">{n}</span>
              </li>
            ))}
          </ul>
        </section>
      ) : null}

      {/* Источники данных — мелко */}
      {sources.length ? (
        <section aria-label="Источники данных портала" className="text-[11px] text-stone-600">
          <p className="flex items-center gap-1.5">
            <Database aria-hidden="true" className="size-3" />
            Источники данных:
          </p>
          <ul className="mt-1 flex flex-col gap-0.5 pl-4.5 font-mono">
            {sources.map((s) => (
              <li key={s}>{s}</li>
            ))}
          </ul>
        </section>
      ) : null}
    </div>
  );
}
