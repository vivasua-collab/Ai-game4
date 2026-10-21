'use client';

/**
 * Радиальная SVG-схема центральной шины: хаб Core (EventBus + DI-контейнер)
 * в центре, узлы (17 модулей + Entry + Adapter) по кольцу.
 *
 * Каждый узел связан с хабом четырьмя направленными рёбрами:
 *   pub      (узел → шина)   события: Publish        — amber, сплошная
 *   sub      (шина → узел)   события: Subscribe      — emerald, сплошная
 *   provides (узел → шина)   сервисы: Register       — amber, пунктир
 *   requires (шина → узел)   сервисы: Resolve/Inject — emerald, пунктир
 *
 * Толщина ребра ~ числу событий/сервисов. Наведение подсвечивает связку узла,
 * клик выбирает узел для панели деталей (клик по хабу — сводка каналов).
 */

import { memo } from 'react';

import type { BusNode } from '@/lib/portal/types';

/* ── геометрия ────────────────────────────────────────────────────────── */

const W = 960;
const H = 680;
const CX = W / 2;
const CY = H / 2;
const HUB_R = 62;
const RING_RX = 392;
const RING_RY = 262;
const NODE_GAP = 38; // отступ начала ребра от центра узла (box перекрывает)
const HUB_GAP = HUB_R + 6;

const SHORT_LABELS: Record<string, string> = {
  'Adapter.UI': 'A.UI',
  'Adapter.Scene': 'A.Scene',
  'Adapter.Scene.Debug': 'QA Debug',
  'Adapter.Input': 'A.Input',
  'Adapter.Persistence': 'A.Persist',
};

export function shortNodeLabel(id: string): string {
  return SHORT_LABELS[id] ?? id;
}

interface Placed {
  node: BusNode;
  x: number;
  y: number;
  label: string;
  boxW: number;
}

function place(nodes: BusNode[]): Placed[] {
  const n = nodes.length;
  return nodes.map((node, i) => {
    const angle = -Math.PI / 2 + (i * 2 * Math.PI) / n;
    const x = CX + RING_RX * Math.cos(angle);
    const y = CY + RING_RY * Math.sin(angle);
    const label = shortNodeLabel(node.id);
    const boxW = Math.max(56, label.length * 7.4 + 18);
    return { node, x, y, label, boxW };
  });
}

function edgeWidth(count: number): number {
  return 1.2 + Math.min(count, 14) * 0.22;
}

/**
 * Ребро узел↔хаб со смещением перпендикуляра. dir=+1 (узел→хаб) | -1 (хаб→узел).
 * Возвращает path d.
 */
function edgePath(p: Placed, offset: number, dir: 1 | -1): string {
  const dx = CX - p.x;
  const dy = CY - p.y;
  const len = Math.hypot(dx, dy) || 1;
  const ux = dx / len;
  const uy = dy / len;
  const px = -uy;
  const py = ux;
  const sx = p.x + ux * NODE_GAP + px * offset;
  const sy = p.y + uy * NODE_GAP + py * offset;
  const ex = CX - ux * HUB_GAP + px * offset;
  const ey = CY - uy * HUB_GAP + py * offset;
  return dir === 1
    ? `M ${sx.toFixed(1)} ${sy.toFixed(1)} L ${ex.toFixed(1)} ${ey.toFixed(1)}`
    : `M ${ex.toFixed(1)} ${ey.toFixed(1)} L ${sx.toFixed(1)} ${sy.toFixed(1)}`;
}

interface EdgeSpec {
  key: string;
  d: string;
  color: 'amber' | 'emerald';
  dash: boolean;
  width: number;
  title: string;
}

function edgesFor(p: Placed): EdgeSpec[] {
  const n = p.node;
  const specs: EdgeSpec[] = [];
  if (n.publishes.length > 0) {
    specs.push({
      key: 'pub',
      d: edgePath(p, -16, 1),
      color: 'amber',
      dash: false,
      width: edgeWidth(n.publishes.length),
      title: `${n.id} → шина: Publish ×${n.publishes.length}`,
    });
  }
  if (n.subscribes.length > 0) {
    specs.push({
      key: 'sub',
      d: edgePath(p, -5, -1),
      color: 'emerald',
      dash: false,
      width: edgeWidth(n.subscribes.length),
      title: `шина → ${n.id}: Subscribe ×${n.subscribes.length}`,
    });
  }
  if (n.provides.length > 0) {
    specs.push({
      key: 'prov',
      d: edgePath(p, 5, 1),
      color: 'amber',
      dash: true,
      width: edgeWidth(n.provides.length),
      title: `${n.id} → DI: Register ×${n.provides.length}`,
    });
  }
  if (n.requires.length > 0) {
    specs.push({
      key: 'req',
      d: edgePath(p, 16, -1),
      color: 'emerald',
      dash: true,
      width: edgeWidth(n.requires.length),
      title: `DI → ${n.id}: Resolve/[Inject] ×${n.requires.length}`,
    });
  }
  return specs;
}

const STROKE: Record<'amber' | 'emerald', string> = {
  amber: '#fbbf24',
  emerald: '#34d399',
};

/* ── компонент ─────────────────────────────────────────────────────────── */

export interface BusDiagramProps {
  nodes: BusNode[];
  selectedId: string | null; // 'Core' | id узла | null
  hoverId: string | null;
  onHover: (id: string | null) => void;
  onSelect: (id: string) => void;
}

function nodeStroke(p: Placed, selected: boolean): string {
  if (selected) return '#fbbf24';
  switch (p.node.kind) {
    case 'module':
      return '#a16207';
    case 'entry':
      return '#57534e';
    default:
      return p.node.debug ? '#3f3f46' : '#44403c';
  }
}

function BusDiagramImpl({ nodes, selectedId, hoverId, onHover, onSelect }: BusDiagramProps) {
  const placed = place(nodes);
  const focus = hoverId ?? selectedId;
  const hubActive = focus === 'Core';
  const hubHovered = hoverId === 'Core';

  return (
    <svg
      viewBox={`0 0 ${W} ${H}`}
      role="img"
      aria-label="Схема взаимодействия модулей через центральную шину"
      className="h-auto w-full select-none"
      fontFamily="var(--font-geist-mono, monospace)"
    >
      <defs>
        <marker
          id="bus-arrow-amber"
          viewBox="0 0 8 8"
          refX="7"
          refY="4"
          markerWidth="6"
          markerHeight="6"
          orient="auto-start-reverse"
        >
          <path d="M 0 0 L 8 4 L 0 8 z" fill="#fbbf24" />
        </marker>
        <marker
          id="bus-arrow-emerald"
          viewBox="0 0 8 8"
          refX="7"
          refY="4"
          markerWidth="6"
          markerHeight="6"
          orient="auto-start-reverse"
        >
          <path d="M 0 0 L 8 4 L 0 8 z" fill="#34d399" />
        </marker>
      </defs>

      {/* рёбра */}
      {placed.map((p) => {
        const isFocus = focus === p.node.id;
        const dimmed = focus !== null && !isFocus && !hubActive;
        return edgesFor(p).map((e) => (
          <path
            key={`${p.node.id}-${e.key}`}
            d={e.d}
            fill="none"
            stroke={STROKE[e.color]}
            strokeWidth={e.width}
            strokeDasharray={e.dash ? '5 4' : undefined}
            markerEnd={`url(#bus-arrow-${e.color})`}
            opacity={dimmed ? 0.08 : isFocus || hubActive ? 0.95 : 0.5}
            className="transition-opacity duration-200"
          >
            <title>{e.title}</title>
          </path>
        ));
      })}

      {/* хаб */}
      <g
        onMouseEnter={() => onHover('Core')}
        onMouseLeave={() => onHover(null)}
        onClick={() => onSelect('Core')}
        className="cursor-pointer"
      >
        <title>Core — центральная шина: EventBus + DI-контейнер</title>
        <circle
          cx={CX}
          cy={CY}
          r={HUB_R + 8}
          fill="#0c0a09"
          stroke={hubActive ? '#fbbf24' : '#292524'}
          strokeWidth={hubHovered || hubActive ? 2 : 1}
          className="transition-all duration-150"
        />
        <circle
          cx={CX}
          cy={CY}
          r={HUB_R - 6}
          fill="#18181b"
          stroke={hubActive ? '#fbbf24' : '#a16207'}
          strokeWidth={hubActive ? 1.6 : 1.2}
          strokeDasharray="3 3"
        />
        <text
          x={CX}
          y={CY - 16}
          textAnchor="middle"
          fontSize="11"
          letterSpacing="1.5"
          fill="#d6d3d1"
        >
          ЦЕНТРАЛЬНАЯ
        </text>
        <text x={CX} y={CY - 3} textAnchor="middle" fontSize="11" letterSpacing="1.5" fill="#d6d3d1">
          ШИНА
        </text>
        <text x={CX} y={CY + 12} textAnchor="middle" fontSize="10" fill="#a8a29e">
          Core · хаб
        </text>
        <text x={CX} y={CY + 28} textAnchor="middle" fontSize="9.5" fill="#fbbf24">
          EventBus
        </text>
        <text x={CX} y={CY + 40} textAnchor="middle" fontSize="9.5" fill="#34d399">
          DI-контейнер
        </text>
      </g>

      {/* узлы */}
      {placed.map((p) => {
        const selected = selectedId === p.node.id;
        const dimmed = focus !== null && focus !== p.node.id && !hubActive;
        return (
          <g
            key={p.node.id}
            onMouseEnter={() => onHover(p.node.id)}
            onMouseLeave={() => onHover(null)}
            onClick={() => onSelect(p.node.id)}
            className="cursor-pointer"
            opacity={dimmed ? 0.45 : 1}
          >
            <title>
              {`${p.node.id} (${p.node.layer}) — публикаций ${p.node.publishes.length}, подписок ${p.node.subscribes.length}, предоставляет ${p.node.provides.length}, запрашивает ${p.node.requires.length}; файлов ${p.node.files}`}
            </title>
            <rect
              x={p.x - p.boxW / 2}
              y={p.y - 15}
              width={p.boxW}
              height={30}
              rx={7}
              fill={selected ? '#292524' : '#0c0a09'}
              stroke={nodeStroke(p, selected)}
              strokeWidth={selected ? 2 : 1.2}
              strokeDasharray={p.node.debug ? '4 3' : undefined}
              className="transition-all duration-150"
            />
            <text
              x={p.x}
              y={p.y + 4}
              textAnchor="middle"
              fontSize="11"
              fill={selected ? '#fef3c7' : p.node.debug ? '#a8a29e' : '#e7e5e4'}
            >
              {p.label}
            </text>
          </g>
        );
      })}
    </svg>
  );
}

export const BusDiagram = memo(BusDiagramImpl);
