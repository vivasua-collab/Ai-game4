'use client';

/**
 * Sticky-шапка портала: название, короткая строка status.headline,
 * бейдж результата сборки и метка обновления. Компактно, с blur-фоном.
 */

import { Flame, ShieldCheck } from 'lucide-react';

import { Skeleton } from '@/components/ui/skeleton';
import type { PortalData } from '@/lib/portal/types';
import { latestUpdated } from '@/lib/portal/helpers';

interface PortalHeaderProps {
  data: PortalData | null;
  loading: boolean;
}

export function PortalHeader({ data, loading }: PortalHeaderProps) {
  const status = data?.status ?? null;
  const bugs = data?.bugs ?? null;
  const updated = latestUpdated(status?.updated, bugs?.updated);
  const buildResult = status?.build?.result;

  return (
    <header className="sticky top-0 z-40 border-b border-stone-800/70 bg-zinc-950/90 backdrop-blur-sm">
      <div className="mx-auto flex w-full max-w-6xl flex-col gap-1 px-4 py-2.5 sm:px-6 sm:py-3">
        {/* flex-wrap: на узких экранах правый кластер (бейдж сборки + метка
            обновления) переносится на вторую линию вместо выхода за вьюпорт
            (390px: иконка + заголовок + shrink-0-кластер не помещались в одну) */}
        <div className="flex min-h-8 flex-wrap items-center gap-2.5 sm:gap-3">
          <Flame
            aria-hidden="true"
            className="size-5 shrink-0 text-amber-400 sm:size-6"
          />
          <h1 className="min-w-0 truncate font-serif text-base font-semibold tracking-tight text-stone-100 sm:text-lg">
            Карта проекта{' '}
            <span className="text-amber-400">Ai-game4</span>
          </h1>
          <div className="ml-auto flex shrink-0 items-center gap-2 sm:gap-3">
            {loading && !data ? (
              <Skeleton className="h-5 w-24 bg-zinc-800" />
            ) : buildResult ? (
              <span
                className="inline-flex max-w-[210px] items-center gap-1.5 rounded-md border border-emerald-500/40 bg-emerald-500/10 px-2 py-0.5 text-[11px] font-medium text-emerald-300 sm:max-w-none"
                title={`Сборка: ${buildResult}`}
              >
                <ShieldCheck aria-hidden="true" className="size-3.5 shrink-0" />
                <span className="truncate">{buildResult}</span>
              </span>
            ) : null}
            {loading && !data ? (
              <Skeleton className="h-5 w-32 bg-zinc-800" />
            ) : updated ? (
              <span className="whitespace-nowrap font-mono text-[11px] text-stone-500">
                обновлено {updated}
              </span>
            ) : null}
          </div>
        </div>
        {loading && !data ? (
          <Skeleton className="h-4 w-3/4 bg-zinc-900" />
        ) : status?.headline ? (
          <p className="truncate text-xs text-stone-400 sm:text-sm" title={status.headline}>
            {status.headline}
          </p>
        ) : (
          <p className="text-xs text-stone-600 sm:text-sm">
            Cultivation World Simulator — визуализация «звуковых дорожек» проекта
          </p>
        )}
      </div>
    </header>
  );
}
