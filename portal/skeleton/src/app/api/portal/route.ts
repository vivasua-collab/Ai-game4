/**
 * Единственный API портала: GET /api/portal.
 *
 * Тонкий каркас: в коде НИКАКИХ данных — три JSON-файла читаются с диска
 * (git-репозиторий aigame4) при каждом запросе. Без prisma, без БД, без кэша.
 * Каждый источник обёрнут в try/catch: портал деградирует информативно
 * (ok:false + errors[]), а не падает 500-й.
 */

import fs from 'fs';
import { NextResponse } from 'next/server';

import type {
  BugsFile,
  BusMapFile,
  Milestone,
  ProjectInfo,
  StatusFile,
  Track,
} from '@/lib/portal/types';

/** Абсолютные пути источников (переживают любой cwd dev-сервера). */
const PROJECT_MAP_PATH =
  '/home/z/my-project/aigame4/docs/docs_v2/00_overview/project_map.json';
const BUGS_PATH = '/home/z/my-project/aigame4/portal/data/bugs.json';
const STATUS_PATH = '/home/z/my-project/aigame4/portal/data/status.json';
/** Карта шины — снимок «по требованию» (gen_bus_map.py); отсутствие не ломает портал. */
const BUS_MAP_PATH = '/home/z/my-project/aigame4/portal/data/bus_map.json';

/** Чтение JSON с изоляцией ошибок. null → источник недоступен. */
function readJson<T>(path: string, errors: string[]): T | null {
  try {
    const raw = fs.readFileSync(path, 'utf8');
    return JSON.parse(raw) as T;
  } catch (err) {
    errors.push(`${path}: ${(err as Error).message}`);
    return null;
  }
}

interface ProjectMapFile {
  version?: string;
  updated?: string;
  project?: ProjectInfo;
  milestones?: Milestone[];
  tracks?: Track[];
}

export async function GET() {
  const errors: string[] = [];

  const map = readJson<ProjectMapFile>(PROJECT_MAP_PATH, errors);
  const bugs = readJson<BugsFile>(BUGS_PATH, errors);
  const status = readJson<StatusFile>(STATUS_PATH, errors);
  const busMap = readJson<BusMapFile>(BUS_MAP_PATH, errors);

  const ok = map !== null && bugs !== null && status !== null;

  return NextResponse.json(
    {
      ok,
      project: map?.project ?? null,
      milestones: map?.milestones ?? [],
      tracks: map?.tracks ?? [],
      bugs,
      status,
      busMap,
      errors,
    },
    { headers: { 'Cache-Control': 'no-store' } },
  );
}
