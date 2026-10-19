'use client';

/**
 * Skeleton-каркас портала на время загрузки данных (/api/portal).
 */

import { Skeleton } from '@/components/ui/skeleton';

export function PortalSkeleton() {
  return (
    <div aria-busy="true" aria-label="Загрузка данных портала" className="mt-6 flex flex-col gap-6">
      {/* KPI */}
      <div className="flex flex-wrap gap-2">
        {Array.from({ length: 7 }).map((_, i) => (
          <Skeleton
            key={i}
            className={`h-16 ${i === 6 ? 'min-w-[160px] flex-[2]' : 'min-w-[104px] flex-1'} bg-zinc-900`}
          />
        ))}
      </div>
      {/* Вехи */}
      <div>
        <Skeleton className="h-3 w-28 bg-zinc-900" />
        <div className="mt-2.5 flex gap-1.5">
          {Array.from({ length: 8 }).map((_, i) => (
            <Skeleton key={i} className="h-7 flex-1 bg-zinc-900" />
          ))}
        </div>
      </div>
      {/* Табы */}
      <Skeleton className="h-9 w-72 bg-zinc-900" />
      <div className="flex flex-col gap-2.5">
        {Array.from({ length: 6 }).map((_, i) => (
          <Skeleton key={i} className="h-16 w-full bg-zinc-900" />
        ))}
      </div>
    </div>
  );
}
