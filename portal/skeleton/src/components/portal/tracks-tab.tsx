'use client';

/**
 * Вкладка «Дорожки»: группировка треков по track.group (тексты групп
 * приходят из JSON, в коде не хардкодятся). Каждая группа — секция
 * со сводкой и списком дорожек-строк.
 */

import { groupTracks, overallProgress } from '@/lib/portal/helpers';
import type { Track } from '@/lib/portal/types';
import { TrackLane } from './track-lane';

interface TracksTabProps {
  tracks: Track[];
  repoUrl: string;
}

export function TracksTab({ tracks, repoUrl }: TracksTabProps) {
  const groups = groupTracks(tracks);

  return (
    <div className="flex flex-col gap-7 pt-4">
      {groups.map(([group, list]) => {
        const stages = list.flatMap((t) => t.stages ?? []);
        const progress = overallProgress(stages);

        return (
          <section key={group} aria-label={`Группа треков: ${group}`}>
            <header className="flex flex-wrap items-baseline gap-x-3 gap-y-1">
              <h3 className="font-serif text-sm font-semibold text-stone-200">
                {group}
              </h3>
              <p className="text-[11px] text-stone-500">
                {list.length} {list.length === 1 ? 'дорожка' : 'дорожки'} ·{' '}
                <span className="tabular-nums text-amber-300/90">{progress}%</span>{' '}
                этапов выполнено
              </p>
            </header>
            <div className="mt-2.5 flex flex-col gap-2.5">
              {list.map((t) => (
                <TrackLane key={t.id} track={t} repoUrl={repoUrl} />
              ))}
            </div>
          </section>
        );
      })}
      {tracks.length === 0 ? (
        <p className="pt-4 text-sm text-stone-500">Дорожки не получены.</p>
      ) : null}
    </div>
  );
}
