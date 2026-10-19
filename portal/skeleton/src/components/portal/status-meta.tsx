/**
 * Визуальные словари статусов: цвета сегментов, бейджей и иконки.
 * Палитра xianxia: zinc/stone фон, amber-золото акценты, emerald = готово,
 * rose = проблемы. Синие и индиго цвета запрещены и не используются.
 */

import {
  Bug,
  Check,
  CheckCircle2,
  Circle,
  CircleDashed,
  CircleDot,
  ExternalLink,
  PackageOpen,
  PauseCircle,
  Search,
} from 'lucide-react';

import type { StageStatus, TrackStatus } from '@/lib/portal/types';

export interface StatusMeta {
  label: string;
  /** Классы цветного сегмента шкалы этапов. */
  segment: string;
  /** Иконка статуса. */
  icon: typeof CheckCircle2;
  iconClass: string;
  /** Классы текстового бейджа. */
  badge: string;
}

const STAGE_META: Record<StageStatus, StatusMeta> = {
  done: {
    label: 'выполнено',
    segment: 'bg-emerald-500/90 border border-emerald-400/50',
    icon: CheckCircle2,
    iconClass: 'text-emerald-400',
    badge: 'border-emerald-500/40 bg-emerald-500/10 text-emerald-300',
  },
  in_progress: {
    label: 'в работе',
    segment: 'bg-amber-500 border border-amber-300/50 animate-pulse',
    icon: CircleDot,
    iconClass: 'text-amber-400',
    badge: 'border-amber-500/50 bg-amber-500/15 text-amber-300',
  },
  planned: {
    label: 'запланировано',
    segment: 'bg-zinc-950 border border-zinc-600',
    icon: Circle,
    iconClass: 'text-zinc-400',
    badge: 'border-zinc-700 bg-transparent text-zinc-300',
  },
  deferred: {
    label: 'отложено',
    segment: 'bg-rose-500/90 border border-rose-400/50',
    icon: PauseCircle,
    iconClass: 'text-rose-400',
    badge: 'border-rose-500/50 bg-rose-500/10 text-rose-300',
  },
  not_started: {
    label: 'не начато',
    segment: 'bg-zinc-800/70 border border-zinc-800',
    icon: CircleDashed,
    iconClass: 'text-stone-500',
    badge: 'border-stone-700 bg-transparent text-stone-400',
  },
};

export function stageMeta(status: StageStatus | string): StatusMeta {
  return STAGE_META[status as StageStatus] ?? STAGE_META.not_started;
}

/* ── Вехи ───────────────────────────────────────────────────────────────── */

export const MILESTONE_META: Record<
  string,
  { label: string; segment: string }
> = {
  done: {
    label: 'выполнена',
    segment: 'bg-emerald-500/80 border-emerald-400/60 hover:bg-emerald-400',
  },
  in_progress: {
    label: 'в работе',
    segment: 'bg-amber-500/90 border-amber-400/60 hover:bg-amber-400',
  },
  not_started: {
    label: 'не начата',
    segment: 'bg-zinc-950/40 border-zinc-700 hover:border-zinc-500',
  },
};

export function milestoneMeta(status: string): { label: string; segment: string } {
  return MILESTONE_META[status] ?? MILESTONE_META.not_started;
}

/* ── Треки ──────────────────────────────────────────────────────────────── */

const TRACK_META: Record<TrackStatus, { label: string; badge: string }> = {
  complete: {
    label: 'трек завершён',
    badge: 'border-emerald-500/40 bg-emerald-500/10 text-emerald-300',
  },
  in_progress: {
    label: 'в работе',
    badge: 'border-amber-500/40 bg-amber-500/10 text-amber-300',
  },
  foundation: {
    label: 'фундамент',
    badge: 'border-stone-500/50 bg-stone-500/10 text-stone-300',
  },
  not_started: {
    label: 'не начат',
    badge: 'border-zinc-700 bg-transparent text-zinc-400',
  },
};

export function trackMeta(status: TrackStatus | string): {
  label: string;
  badge: string;
} {
  return TRACK_META[status as TrackStatus] ?? TRACK_META.not_started;
}

/* ── Реестр аудита: статусы, серьёзность, типы ──────────────────────────── */

export interface BugMeta {
  label: string;
  badge: string;
  icon?: typeof Check;
}

const BUG_STATUS_META: Record<string, BugMeta> = {
  new: {
    label: 'новое',
    badge: 'border-zinc-700 bg-zinc-800/60 text-zinc-300',
  },
  analyzing: {
    label: 'анализ',
    badge: 'border-amber-500/50 bg-amber-500/10 text-amber-300',
  },
  planned: {
    label: 'запланировано',
    badge: 'border-stone-500/60 bg-transparent text-stone-300',
  },
  in_progress: {
    label: 'исправляется',
    badge: 'border-amber-400 bg-amber-500 text-zinc-950',
  },
  fixed: {
    label: 'исправлено',
    badge: 'border-emerald-500/60 bg-emerald-500/10 text-emerald-300',
  },
  verified: {
    label: 'подтверждено',
    badge: 'border-emerald-400 bg-emerald-500/15 text-emerald-300',
    icon: Check,
  },
  deferred: {
    label: 'отложено',
    badge: 'border-rose-500/50 bg-rose-500/10 text-rose-300',
  },
};

export function bugStatusMeta(status: string): BugMeta {
  return BUG_STATUS_META[status] ?? { label: status, badge: 'border-zinc-700 text-zinc-300' };
}

const SEVERITY_META: Record<string, { label: string; badge: string }> = {
  P1: {
    label: 'P1 · блокирует опыт',
    badge: 'border-rose-500/60 bg-rose-500/15 text-rose-300',
  },
  P2: {
    label: 'P2 · заметно',
    badge: 'border-amber-500/60 bg-amber-500/15 text-amber-300',
  },
  P3: {
    label: 'P3 · полировка',
    badge: 'border-zinc-600 bg-zinc-800/60 text-zinc-300',
  },
};

export function severityMeta(sev: string): { label: string; badge: string } {
  return (
    SEVERITY_META[sev] ?? { label: sev, badge: 'border-zinc-600 text-zinc-300' }
  );
}

const BUG_TYPE_META: Record<string, { label: string; badge: string; icon: typeof Bug }> = {
  bug: {
    label: 'баг',
    badge: 'border-rose-500/40 text-rose-300/90',
    icon: Bug,
  },
  gap: {
    label: 'разрыв',
    badge: 'border-amber-500/40 text-amber-200/90',
    icon: PackageOpen,
  },
  verify: {
    label: 'верификация',
    badge: 'border-stone-500/50 text-stone-300/90',
    icon: Search,
  },
};

export function bugTypeMeta(type: string): {
  label: string;
  badge: string;
  icon: typeof Bug;
} {
  return (
    BUG_TYPE_META[type] ?? {
      label: type,
      badge: 'border-zinc-600 text-zinc-300',
      icon: ExternalLink,
    }
  );
}

/* ── Чипы волн ──────────────────────────────────────────────────────────── */

export const WAVE_CHIP =
  'border-amber-500/30 bg-amber-500/[0.07] text-amber-200/90';
