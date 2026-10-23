#nullable enable
// Создано: 2026-10-23 — ST13-001 (Этап-13 аудита): grid-based A* pathfinding.
// Канон: docs/docs_v2/04_entities/NPC_AI_SYSTEM.md §6 — «Grid-based
// pathfinding: 1 тайл = 1 тик. Никакого NavMesh... Алгоритм поиска пути:
// A* на сетке тайлов. Стоимость: 1 за прямой шаг, ~1.4 за диагональ.
// Препятствия: тайлы с пометкой blocked» — «канонические и обязательны
// к реализации» (футер документа). Прежде NPCMovementService.MoveToward
// писал позицию по прямой без проверки проходимости — NPC проходили
// сквозь препятствия.
//
// Engine-agnostic (чистый C#, Modules-слой): проходимость — через
// ITileService.IsWalkable (тот же контракт, что у AnimalService).
// Ограничения для предсказуемости и производительности:
//   - потолок раскрываемых узлов MaxNodes (нет пути/далеко → null);
//   - детерминированный tie-break (f, then g, then insertion order) —
//     порядок перечисления словарей не влияет на результат;
//   - zero-GC hot path: массивы-пулы переиспользуются между запросами.
using System;
using System.Collections.Generic;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.NPC
{
    /// <summary>
    /// A* на тайловой сетке (8-направлений, octile-эвристика) + проверка
    /// прямой видимости (supercover-Bresenham, углы не срезаются).
    /// Используется NPCMovementService; пригоден для тестов напрямую.
    /// </summary>
    public sealed class NPCPathfinder
    {
        private readonly ITileService? _tiles;

        /// <summary>Потолок раскрываемых узлов на один поиск (анти-всплеск:
        /// несуществующий путь через полкарты не должен лочить кадр).</summary>
        public const int MaxNodes = 4000;

        /// <summary>Стоимость диагонального шага (§6.1: ~1.4).</summary>
        private const float DiagCost = 1.41421356f;

        public NPCPathfinder(ITileService? tiles)
        {
            _tiles = tiles;
        }

        public bool IsAvailable => _tiles != null;

        public bool IsWalkableTile(Position2D tile)
            => _tiles != null && _tiles.IsWalkable(tile.X, tile.Y);

        // ====================================================================
        //  Проверка прямой (быстрый путь без A*)
        // ====================================================================

        /// <summary>
        /// Supercover-Bresenham: все тайлы отрезка (включая концы) проходимы?
        /// Диагональный шаг требует проходимости ОБЕИХ ортогональных соседей
        /// (угол между двумя стенами не срезается).
        /// </summary>
        public bool LineWalkable(Position2D a, Position2D b)
        {
            if (_tiles == null) return false;
            int x0 = a.X, y0 = a.Y, x1 = b.X, y1 = b.Y;
            if (!_tiles.IsWalkable(x0, y0) || !_tiles.IsWalkable(x1, y1)) return false;
            int dx = Math.Abs(x1 - x0), dy = Math.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;
            int x = x0, y = y0;
            while (x != x1 || y != y1)
            {
                int e2 = err * 2;
                if (e2 > -dy && e2 < dx)
                {
                    if (!_tiles.IsWalkable(x + sx, y) || !_tiles.IsWalkable(x, y + sy)) return false;
                    x += sx; y += sy; err -= dy; err += dx;
                }
                else if (e2 > -dy) { x += sx; err -= dy; }
                else { y += sy; err += dx; }
                if ((x != x1 || y != y1) && !_tiles.IsWalkable(x, y)) return false;
            }
            return true;
        }

        // ====================================================================
        //  A*
        // ====================================================================

        // Пулы (переиспользование между вызовами — zero-GC hot path).
        private readonly int[] _heap = new int[MaxNodes];
        private int _heapSize;
        private readonly Dictionary<int, int> _nodeIndex = new(MaxNodes);
        private readonly float[] _g = new float[MaxNodes];
        private readonly float[] _f = new float[MaxNodes];
        private readonly int[] _parent = new int[MaxNodes];
        private readonly Position2D[] _pos = new Position2D[MaxNodes];
        private int _nodeCount;
        private long _searchStamp;
        private readonly Dictionary<int, long> _stamp = new(MaxNodes);

        /// <summary>
        /// Найти путь по тайлам от start к goal (включая goal, без start).
        /// null — пути нет (в т.ч. цель/старт непроходимы, потолок узлов).
        /// Детерминирован: равные f разрешаются по g, затем по порядку
        /// создания узла (не по хешу словаря).
        /// </summary>
        public List<Position2D>? FindPath(Position2D start, Position2D goal)
        {
            if (_tiles == null) return null;
            if (start == goal) return new List<Position2D>();
            if (!_tiles.IsWalkable(goal.X, goal.Y))
            {
                // Цель внутри препятствия: ближайший проходимый сосед 8-связности.
                var near = NearestWalkable(goal);
                if (near == null) return null;
                goal = near.Value;
                if (start == goal) return new List<Position2D>();
            }
            if (!_tiles.IsWalkable(start.X, start.Y)) return null;
            if (LineWalkable(start, goal))
            {
                var direct = new List<Position2D> { goal };
                return direct;
            }

            _searchStamp++;
            _nodeCount = 0;
            _heapSize = 0;
            _nodeIndex.Clear();

            int startNode = NewNode(start, 0f, Heuristic(start, goal), -1);
            HeapPush(startNode);

            int expanded = 0;
            int goalNode = -1;
            while (_heapSize > 0)
            {
                int cur = HeapPop();
                var curPos = _pos[cur];
                if (curPos == goal) { goalNode = cur; break; }
                if (++expanded > MaxNodes) break; // анти-всплеск

                for (int i = 0; i < 8; i++)
                {
                    int nx = curPos.X + Dx[i];
                    int ny = curPos.Y + Dy[i];
                    if (!_tiles.IsWalkable(nx, ny)) continue;
                    bool diag = i >= 4;
                    if (diag
                        && (!_tiles.IsWalkable(curPos.X + Dx[i], curPos.Y)
                            || !_tiles.IsWalkable(curPos.X, curPos.Y + Dy[i])))
                        continue; // угол не срезаем
                    var npos = new Position2D(nx, ny);
                    float stepCost = diag ? DiagCost : 1f;
                    float ng = _g[cur] + stepCost;
                    int key = ny * 65536 + nx;
                    if (_stamp.TryGetValue(key, out var s) && s == _searchStamp)
                    {
                        int existing = _nodeIndex[key];
                        if (ng + 1e-4f >= _g[existing]) continue;
                        _g[existing] = ng;
                        _f[existing] = ng + Heuristic(npos, goal);
                        _parent[existing] = cur;
                        HeapUpdate(existing);
                    }
                    else
                    {
                        int node = NewNode(npos, ng, ng + Heuristic(npos, goal), cur);
                        _stamp[key] = _searchStamp;
                        _nodeIndex[key] = node;
                        HeapPush(node);
                    }
                }
            }

            if (goalNode < 0) return null;
            // Реконструкция (reverse → прямой порядок).
            var result = new List<Position2D>();
            int n = goalNode;
            while (n >= 0 && _parent[n] >= 0)
            {
                result.Add(_pos[n]);
                n = _parent[n];
            }
            result.Reverse();
            // Упрощение: убираем коллинеарные промежуточные вейпоинты
            // (меньше словарных проверок при следовании; сегменты уже
            // гарантированно проходимы соседними шагами A*).
            return Simplify(result);
        }

        private List<Position2D> Simplify(List<Position2D> path)
        {
            if (path.Count <= 2) return path;
            var simple = new List<Position2D> { path[0] };
            int i = 1;
            while (i < path.Count - 1)
            {
                // Склейка, пока направление не меняется (8-направлений).
                int dx0 = Math.Sign(path[i].X - simple[simple.Count - 1].X);
                int dy0 = Math.Sign(path[i].Y - simple[simple.Count - 1].Y);
                int dx1 = Math.Sign(path[i + 1].X - path[i].X);
                int dy1 = Math.Sign(path[i + 1].Y - path[i].Y);
                if (dx0 == dx1 && dy0 == dy1) { i++; continue; }
                simple.Add(path[i]);
                i++;
            }
            simple.Add(path[path.Count - 1]);
            return simple;
        }

        private Position2D? NearestWalkable(Position2D tile)
        {
            for (int r = 1; r <= 2; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Math.Abs(dx) != r && Math.Abs(dy) != r) continue;
                        if (_tiles!.IsWalkable(tile.X + dx, tile.Y + dy))
                            return new Position2D(tile.X + dx, tile.Y + dy);
                    }
            return null;
        }

        private static readonly int[] Dx = { 1, -1, 0, 0, 1, 1, -1, -1 };
        private static readonly int[] Dy = { 0, 0, 1, -1, 1, -1, 1, -1 };

        private float Heuristic(Position2D a, Position2D b)
        {
            int dx = Math.Abs(b.X - a.X), dy = Math.Abs(b.Y - a.Y);
            // octile
            return (dx > dy)
                ? (dx - dy) + DiagCost * dy
                : (dy - dx) + DiagCost * dx;
        }

        // === Узлы ===
        private int NewNode(Position2D p, float g, float f, int parent)
        {
            int idx = _nodeCount++;
            _pos[idx] = p;
            _g[idx] = g;
            _f[idx] = f;
            _parent[idx] = parent;
            return idx;
        }

        // === Binary heap по (f, g, insertion) — детерминированный порядок ===
        private void HeapPush(int node)
        {
            _heap[_heapSize++] = node;
            int i = _heapSize - 1;
            while (i > 0)
            {
                int p = (i - 1) >> 1;
                if (Better(_heap[i], _heap[p])) { (_heap[i], _heap[p]) = (_heap[p], _heap[i]); i = p; }
                else break;
            }
        }

        private int HeapPop()
        {
            int top = _heap[0];
            _heap[0] = _heap[--_heapSize];
            int i = 0;
            while (true)
            {
                int l = i * 2 + 1, r = l + 1, best = i;
                if (l < _heapSize && Better(_heap[l], _heap[best])) best = l;
                if (r < _heapSize && Better(_heap[r], _heap[best])) best = r;
                if (best == i) break;
                (_heap[i], _heap[best]) = (_heap[best], _heap[i]);
                i = best;
            }
            return top;
        }

        private void HeapUpdate(int node)
        {
            int i = Array.IndexOf(_heap, node, 0, _heapSize);
            if (i < 0) { HeapPush(node); return; }
            while (i > 0)
            {
                int p = (i - 1) >> 1;
                if (Better(_heap[i], _heap[p])) { (_heap[i], _heap[p]) = (_heap[p], _heap[i]); i = p; }
                else break;
            }
        }

        /// <summary>Детерминированное сравнение: меньше f; при равных —
        /// меньше g (ближе к старту — стабильнее путь); при равных —
        /// раньше создан.</summary>
        private bool Better(int a, int b)
        {
            if (_f[a] != _f[b]) return _f[a] < _f[b];
            if (_g[a] != _g[b]) return _g[a] < _g[b];
            return a < b;
        }
    }
}
