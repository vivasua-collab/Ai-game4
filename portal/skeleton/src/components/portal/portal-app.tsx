'use client';

/**
 * Каркас портала «Карта проекта Ai-game4»: загрузка данных из /api/portal
 * (тонкий API над JSON git-репозитория), состояния загрузки/ошибки,
 * sticky-шапка, KPI-полоса, дорожка вех и три вкладки.
 * Никаких данных в коде — всё приходит в рантайме.
 */

import { useCallback, useEffect, useState } from 'react';
import { motion } from 'framer-motion';
import { Activity, AudioLines, ClipboardList } from 'lucide-react';

import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import type { PortalData } from '@/lib/portal/types';
import { BugsTab } from './bugs-tab';
import { KpiRow } from './kpi-row';
import { MilestoneStrip } from './milestone-strip';
import { PortalError } from './portal-error';
import { PortalFooter } from './portal-footer';
import { PortalHeader } from './portal-header';
import { PortalSkeleton } from './portal-skeleton';
import { StatusTab } from './status-tab';
import { TracksTab } from './tracks-tab';

export function PortalApp() {
  const [data, setData] = useState<PortalData | null>(null);
  const [errors, setErrors] = useState<string[]>([]);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    setLoading(true);
    setErrors([]);
    try {
      const resp = await fetch('/api/portal');
      const json: PortalData = await resp.json();
      if (!resp.ok || !json.ok) {
        const list =
          json.errors && json.errors.length > 0 ? json.errors : [`HTTP ${resp.status}`];
        setErrors(list);
        setData(null);
      } else {
        setData(json);
      }
    } catch (err) {
      setErrors([`Сеть: ${(err as Error).message}`]);
      setData(null);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const failed = !loading && !data && errors.length > 0;
  const ready = data && data.ok;
  const repoUrl = data?.project?.repo ?? '';

  return (
    <div className="flex min-h-screen flex-col bg-zinc-950 text-stone-200">
      <PortalHeader data={data} loading={loading} />

      <main className="mx-auto w-full max-w-6xl flex-1 px-4 sm:px-6">
        {loading && !data ? (
          <PortalSkeleton />
        ) : failed ? (
          <PortalError errors={errors} onRetry={() => void load()} />
        ) : ready ? (
          <motion.div
            initial={{ opacity: 0, y: 12 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.35, ease: 'easeOut' }}
          >
            <KpiRow tracks={data.tracks} milestones={data.milestones} />
            <MilestoneStrip milestones={data.milestones} />

            <Tabs defaultValue="tracks" className="mt-8">
              <TabsList className="h-10 rounded-lg border border-stone-800 bg-zinc-900/70 p-1">
                <TabsTrigger
                  value="tracks"
                  className="min-h-8 gap-1.5 rounded-md px-3 text-xs font-medium text-stone-400 data-[state=active]:border-stone-700 data-[state=active]:bg-zinc-800 data-[state=active]:text-stone-50 sm:text-sm"
                >
                  <AudioLines aria-hidden="true" className="size-4 text-amber-400" />
                  Дорожки
                  <span className="hidden font-mono text-[10px] text-stone-500 sm:inline">
                    {data.tracks.length}
                  </span>
                </TabsTrigger>
                <TabsTrigger
                  value="bugs"
                  className="min-h-8 gap-1.5 rounded-md px-3 text-xs font-medium text-stone-400 data-[state=active]:border-stone-700 data-[state=active]:bg-zinc-800 data-[state=active]:text-stone-50 sm:text-sm"
                >
                  <ClipboardList aria-hidden="true" className="size-4 text-amber-400" />
                  Реестр аудита
                  <span className="hidden font-mono text-[10px] text-stone-500 sm:inline">
                    {data.bugs?.items?.length ?? 0}
                  </span>
                </TabsTrigger>
                <TabsTrigger
                  value="status"
                  className="min-h-8 gap-1.5 rounded-md px-3 text-xs font-medium text-stone-400 data-[state=active]:border-stone-700 data-[state=active]:bg-zinc-800 data-[state=active]:text-stone-50 sm:text-sm"
                >
                  <Activity aria-hidden="true" className="size-4 text-amber-400" />
                  Статус
                </TabsTrigger>
              </TabsList>

              <TabsContent value="tracks">
                <TracksTab tracks={data.tracks} repoUrl={repoUrl} />
              </TabsContent>
              <TabsContent value="bugs">
                {data.bugs ? (
                  <BugsTab bugs={data.bugs} repoUrl={repoUrl} />
                ) : (
                  <p className="pt-4 text-sm text-stone-500">
                    Реестр находок недоступен.
                  </p>
                )}
              </TabsContent>
              <TabsContent value="status">
                {data.status ? (
                  <StatusTab status={data.status} />
                ) : (
                  <p className="pt-4 text-sm text-stone-500">
                    Снимок состояния недоступен.
                  </p>
                )}
              </TabsContent>
            </Tabs>
          </motion.div>
        ) : null}

        <div className="min-h-12" aria-hidden="true" />
      </main>

      <PortalFooter />
    </div>
  );
}
