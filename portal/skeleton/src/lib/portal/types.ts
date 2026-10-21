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

/* ── Карта центральной шины (portal/data/bus_map.json) ─────────────────── */

/** Публикация события в шину (EventBus) с указанием файлов-источников. */
export interface BusEventRef {
  event: string;
  publishers: string[];
  debug_only?: boolean;
}

/** Узел карты шины: модуль / Entry / Adapter-слой. */
export interface BusNode {
  id: string;
  kind: 'module' | 'entry' | 'adapter' | (string & {});
  layer: string;
  order: number;
  description: string;
  debug: boolean;
  files: number;
  publishes: string[];
  subscribes: string[];
  publishes_files?: Record<string, string[]>;
  subscribes_files?: Record<string, string[]>;
  provides: Array<{
    service: string;
    impl: string;
    kind: 'service' | 'self' | 'instance' | 'config' | (string & {});
    note?: string;
  }>;
  requires: Array<{
    service: string;
    via: string[];
    kind: 'service' | 'config' | (string & {});
  }>;
}

export interface BusChecks {
  /** События публикуются, но продакшн-подписчиков нет. */
  events_no_subscriber: Array<{ event: string; publishers: string[]; debug_only: boolean }>;
  /** События подписаны, но никто не публикует. */
  events_no_publisher: Array<{ event: string; subscribers: string[] }>;
  /** Контракты, не используемые ни в publish, ни в subscribe. */
  contracts_unused: string[];
  /** Структуры-сообщения вне Core/Messaging/Contracts. */
  contracts_local_unscoped: Array<{ event: string; origin: string }>;
  /** Требования без регистрации (не резолвится). */
  services_unresolved: Array<{ service: string; required_by: string[]; kind?: string }>;
  /** Регистрации без потребителей (кладут в шину, никто не забирает). */
  services_unconsumed: Array<{ service: string; provided_by: string }>;
}

export interface BusMapFile {
  schema_version?: number;
  updated?: string;
  generated_by?: string;
  update_policy?: string;
  source_head?: string;
  hub?: {
    id: string;
    title: string;
    channels: Array<{
      id: string;
      title: string;
      role: string;
      contracts_total?: number;
      services_total?: number;
      groups?: Array<{ file: string; count: number }>;
    }>;
  };
  nodes: BusNode[];
  checks: BusChecks;
  totals: {
    modules: number;
    nodes: number;
    contracts_defined: number;
    events_published: number;
    events_subscribed: number;
    publish_edges: number;
    subscribe_edges: number;
    provide_edges: number;
    require_edges: number;
    services_provided: number;
    services_required: number;
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
  busMap: BusMapFile | null;
  errors: string[];
}
