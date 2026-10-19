/**
 * Чистые функции портала: форматирование, счётчики, группировка и сортировка.
 * Никаких данных в коде — всё считается поверх JSON, отданного /api/portal.
 */

import type {
  BugItem,
  BugStatus,
  Milestone,
  Stage,
  StageStatus,
  Track,
  WindowValue,
} from './types';

/* ── Форматирование ────────────────────────────────────────────────────── */

/** Окно периода → строка. [д] → «2026-08-15»; [a, b] → «a → b»; null → «…». */
export function formatWindow(w?: WindowValue | null): string {
  if (!w || w.length === 0) return 'сроки не определены';
  const [a, b] = w;
  if (b == null && (w.length === 1 || a == null)) {
    return a ?? 'сроки не определены';
  }
  if (w.length === 1) return a ?? '—';
  const from = a ?? '…';
  const to = b ?? '…';
  return `${from} → ${to}`;
}

/** ISO-время (2026-10-19T11:58:00Z) → «2026-10-19 11:58 UTC». */
export function formatUpdated(iso?: string | null): string | null {
  if (!iso) return null;
  const m = /^(\d{4}-\d{2}-\d{2})T(\d{2}:\d{2})/.exec(iso);
  if (!m) return iso;
  return `${m[1]} ${m[2]} UTC`;
}

/** Последнее обновление из доступных меток (bugs/status). */
export function latestUpdated(...isoseconds: (string | undefined | null)[]): string | null {
  const clean = isoseconds.filter((x): x is string => typeof x === 'string' && x.length > 0);
  if (clean.length === 0) return null;
  return formatUpdated(clean.reduce((acc, x) => (x > acc ? x : acc)));
}

/** Последний сегмент пути — для компактных чипов пруфов. */
export function baseName(path: string): string {
  const parts = path.split('/');
  return parts[parts.length - 1] || path;
}

/** Ссылка на файл в GitHub (blob/main) по пути внутри репозитория. */
export function repoFileUrl(repo: string, path: string): string {
  const clean = repo.replace(/\.git$/, '').replace(/\/$/, '');
  return `${clean}/blob/main/${path.replace(/^\//, '')}`;
}

/* ── Счётчики ──────────────────────────────────────────────────────────── */

export interface StageCounts {
  total: number;
  /** Итог без отложенных (знаменатель progress_rule). */
  counted: number;
  byStatus: Record<StageStatus, number>;
}

const STAGE_STATUSES: StageStatus[] = [
  'done',
  'in_progress',
  'planned',
  'deferred',
  'not_started',
];

/** Счётчики этапов по статусам. Считаются на клиенте из tracks. */
export function countStages(stages: Stage[]): StageCounts {
  const byStatus = Object.fromEntries(
    STAGE_STATUSES.map((s) => [s, 0]),
  ) as Record<StageStatus, number>;
  for (const s of stages) {
    if (byStatus[s.status] != null) byStatus[s.status] += 1;
  }
  const total = stages.length;
  return { total, counted: total - byStatus.deferred, byStatus };
}

/**
 * Общий прогресс = доля done-этапов от всех, кроме deferred
 * (правило progress_rule из project_map.json).
 */
export function overallProgress(stages: Stage[]): number {
  const { byStatus, counted } = countStages(stages);
  if (counted === 0) return 0;
  return Math.round((byStatus.done / counted) * 100);
}

/** Счётчики вех по статусам. */
export function countMilestones(ms: Milestone[]): Record<string, number> {
  const acc: Record<string, number> = {};
  for (const m of ms) acc[m.status] = (acc[m.status] ?? 0) + 1;
  return acc;
}

/* ── Группировка и сортировка ───────────────────────────────────────────── */

/** Группировка треков по track.group с сохранением порядка появления. */
export function groupTracks(tracks: Track[]): Array<[string, Track[]]> {
  const groups: Array<[string, Track[]]> = [];
  const index = new Map<string, Track[]>();
  for (const t of tracks) {
    const key = t.group ?? '—';
    let list = index.get(key);
    if (!list) {
      list = [];
      index.set(key, list);
      groups.push([key, list]);
    }
    list.push(t);
  }
  return groups;
}

/** Порядок приоритета серьёзности для сортировки реестра (P1 → P3). */
const SEVERITY_ORDER: Record<string, number> = { P1: 0, P2: 1, P3: 2 };

/** Канонический порядок статусов находок (жизненный цикл исправления). */
export const BUG_STATUS_ORDER: BugStatus[] = [
  'new',
  'analyzing',
  'planned',
  'in_progress',
  'fixed',
  'verified',
  'deferred',
];

/** Сортировка реестра: волна → серьёзность → id. */
export function sortBugs(items: BugItem[]): BugItem[] {
  return [...items].sort((a, b) => {
    if (a.fix_wave !== b.fix_wave) return a.fix_wave - b.fix_wave;
    const sa = SEVERITY_ORDER[a.severity] ?? 9;
    const sb = SEVERITY_ORDER[b.severity] ?? 9;
    if (sa !== sb) return sa - sb;
    return a.id.localeCompare(b.id);
  });
}

/* ── Прочее ─────────────────────────────────────────────────────────────── */

/**
 * Кастомный тонкий скроллбар (без правок globals.css — каркас
 * самодостаточен и переживает восстановление из skeleton).
 */
export const thinScrollClass =
  '[scrollbar-width:thin] [scrollbar-color:#3f3f46_transparent] ' +
  '[&::-webkit-scrollbar]:w-1.5 [&::-webkit-scrollbar]:h-1.5 ' +
  '[&::-webkit-scrollbar-track]:bg-transparent ' +
  '[&::-webkit-scrollbar-thumb]:rounded-full [&::-webkit-scrollbar-thumb]:bg-zinc-700 ' +
  'hover:[&::-webkit-scrollbar-thumb]:bg-zinc-600';
