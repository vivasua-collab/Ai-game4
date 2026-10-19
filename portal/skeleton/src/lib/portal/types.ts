/**
 * Типы данных портала «Карта проекта Ai-game4».
 *
 * Каркас приложения — тонкий: все данные читаются в рантайме из JSON-файлов
 * git-репозитория aigame4 (см. /api/portal). Здесь описаны формы этих данных
 * ровно настолько, насколько их использует UI. Поля, которых может не быть,
 * помечены опциональными — схема JSON ведётся main-агентом и может расширяться.
 */

/** Значение «окна» (периода): массив из 1–2 дат, элементы могут быть null. */
export type WindowValue = (string | null)[];

/** Статус этапа трека (project_map.json → format.stage_status). */
export type StageStatus =
  | 'done'
  | 'in_progress'
  | 'planned'
  | 'deferred'
  | 'not_started';

/** Статус трека (project_map.json → format.track_status). */
export type TrackStatus = 'complete' | 'in_progress' | 'foundation' | 'not_started';

export interface Stage {
  id: string;
  title: string;
  status: StageStatus;
  window: WindowValue;
  proofs?: string[];
  note?: string;
}

export interface TrackStart {
  date?: string;
  milestone?: string;
}

export interface Track {
  id: string;
  group: string;
  number: number;
  title: string;
  goal: string;
  /** DoD — финальная цель трека. */
  final: string;
  status: TrackStatus;
  progress_pct: number;
  start?: TrackStart;
  stages: Stage[];
}

export interface Milestone {
  id: string;
  title: string;
  status: 'done' | 'in_progress' | 'not_started' | (string & {});
  window: WindowValue;
  summary: string;
}

export interface ProjectInfo {
  title: string;
  repo: string;
  engine?: string;
  vision?: string;
  architecture?: string;
  head_at_mapping?: string;
}

/* ── Реестр находок аудита (portal/data/bugs.json) ──────────────────────── */

export type BugType = 'bug' | 'gap' | 'verify' | (string & {});
export type BugSeverity = 'P1' | 'P2' | 'P3' | (string & {});
export type BugStatus =
  | 'new'
  | 'analyzing'
  | 'planned'
  | 'in_progress'
  | 'fixed'
  | 'verified'
  | 'deferred';

export interface BugHistoryEntry {
  date: string;
  event: string;
}

export interface BugItem {
  id: string;
  type: BugType;
  severity: BugSeverity;
  title: string;
  module: string;
  /** Ссылка на этап/трек карты проекта (project_map.json). */
  track_ref?: string;
  status: BugStatus;
  fix_wave: number;
  assignee?: string;
  found?: string;
  summary: string;
  analysis?: string;
  fix_plan?: string;
  progress_pct?: number;
  refs?: string[];
  history?: BugHistoryEntry[];
}

export interface BugsFile {
  version?: string;
  updated?: string;
  format?: {
    purpose?: string;
    type?: Record<string, string>;
    severity?: Record<string, string>;
    item_status?: Record<string, string>;
    fix_wave?: Record<string, string>;
  };
  items: BugItem[];
}

/* ── Снимок состояния (portal/data/status.json) ─────────────────────────── */

export interface StatusFile {
  version?: string;
  updated?: string;
  headline?: string;
  playtest?: { note?: string; window?: string };
  build?: {
    head?: string;
    last_verified?: string;
    result?: string;
    qa?: string;
  };
  focus?: string[];
  next?: string[];
  portal?: {
    born?: string;
    data_sources?: string[];
    restore?: string;
  };
}

/* ── Ответ API /api/portal ──────────────────────────────────────────────── */

export interface PortalData {
  ok: boolean;
  project: ProjectInfo | null;
  milestones: Milestone[];
  tracks: Track[];
  bugs: BugsFile | null;
  status: StatusFile | null;
  errors: string[];
}
