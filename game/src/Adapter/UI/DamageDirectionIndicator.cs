#nullable enable
// Создано: 2026-09-04 — S5: off-screen индикатор направления атакующего.
//
// Проблема: игрок получает урон от NPC, находящегося ВНЕ видимой области
// (дальний бой, погоня за спиной) — тост «💥 −12 HP — Имя» не даёт понять,
// ГДЕ атакующий. В замесе игрок теряет ориентацию: «кто меня бьёт и откуда?».
//
// Решение: стрелка на краю экрана в направлении атакующего (пока тот вне
// кадра). Позиция/угол обновляются каждый физ-тик (NPC движется), TTL 2с
// после последнего удара. Когда атакующий появляется на экране — стрелка
// мгновенно убирается (источник виден глазами).
//
// Договорённость с GameWorldController (вся логика — там):
//   • ShowOrUpdate(npcId, point, angle) — новый удар: ПРОДЛЕВАЕТ TTL;
//   • UpdatePose(npcId, point, angle) — тик позы: TTL НЕ продлевает;
//   • FadeOut(npcId) — источник на экране/умер: удалить сразу;
//   • GetActiveIds() — снапшот для тика контроллера.
// Рендер: _Draw — полигон-стрелка + обводка, alpha = fade по TTL.
using Godot;
using System.Collections.Generic;

namespace CultivationGame.Adapter.UI;

/// <summary>
/// HUD-слой: стрелки направления атакующих вне экрана (S5, 2026-09-04).
/// Чистый рендерер: геометрия/цвет/анимация — здесь, логика источника —
/// в GameWorldController (позиции NPC/игрока/камеры).
/// </summary>
public partial class DamageDirectionIndicator : Control
{
    private sealed class DirEntry
    {
        public Vector2 Point;      // экранная точка (центр стрелки)
        public float AngleRad;     // направление ОТ игрока К атакующему
        public float TimeLeft;     // TTL (только ShowOrUpdate продлевает)
    }

    private const float TotalTtl = 2.0f;
    private const float FadeTail = 0.6f;  // последние сек — затухание

    private readonly Dictionary<string, DirEntry> _entries = new();

    public int ActiveCount => _entries.Count;

    public DamageDirectionIndicator()
    {
        Name = "DamageDirectionIndicator";
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    }

    /// <summary>Новый урон от npcId: показать/продлить стрелку (точка+угол).</summary>
    public void ShowOrUpdate(string npcId, Vector2 screenPoint, float angleRad)
    {
        if (string.IsNullOrEmpty(npcId)) return;
        if (_entries.TryGetValue(npcId, out var e))
        {
            e.Point = screenPoint;
            e.AngleRad = angleRad;
            e.TimeLeft = TotalTtl;
        }
        else
        {
            _entries[npcId] = new DirEntry
            {
                Point = screenPoint,
                AngleRad = angleRad,
                TimeLeft = TotalTtl,
            };
        }
        QueueRedraw();
    }

    /// <summary>Тик позы (контроллер): сдвинуть стрелку БЕЗ продления TTL.</summary>
    public void UpdatePose(string npcId, Vector2 screenPoint, float angleRad)
    {
        if (string.IsNullOrEmpty(npcId)) return;
        if (_entries.TryGetValue(npcId, out var e))
        {
            e.Point = screenPoint;
            e.AngleRad = angleRad;
            QueueRedraw();
        }
    }

    /// <summary>Источник стал виден / умер — стрелка больше не нужна.</summary>
    public void FadeOut(string npcId)
    {
        if (string.IsNullOrEmpty(npcId)) return;
        if (_entries.Remove(npcId))
            QueueRedraw();
    }

    /// <summary>Снапшот id активных стрелок (для тика контроллера).</summary>
    public IReadOnlyList<string> GetActiveIds()
    {
        var list = new List<string>(_entries.Count);
        list.AddRange(_entries.Keys);
        return list;
    }

    /// <summary>QA: угол стрелки по npcId в градусах (null — нет стрелки).</summary>
    public float? AngleOf(string npcId)
        => _entries.TryGetValue(npcId, out var e)
            ? Mathf.RadToDeg(e.AngleRad)
            : null;

    public override void _Process(double delta)
    {
        if (_entries.Count == 0) return;

        bool changed = false;
        List<string>? expired = null;
        foreach (var kvp in _entries)
        {
            kvp.Value.TimeLeft -= (float)delta;
            if (kvp.Value.TimeLeft <= 0f)
                (expired ??= new List<string>()).Add(kvp.Key);
        }
        if (expired != null)
        {
            foreach (var id in expired) _entries.Remove(id);
            changed = true;
        }
        if (changed || _entries.Count > 0)
            QueueRedraw();
    }

    public override void _Draw()
    {
        foreach (var kvp in _entries)
        {
            var e = kvp.Value;
            float alpha = Mathf.Clamp(e.TimeLeft / FadeTail, 0f, 1f);
            var color = new Color(0.95f, 0.28f, 0.22f, alpha);

            // Стрелка (наконечник + хвост-выемка), повёрнутая на угол.
            // Вершины в локальных координатах (кончик +X, поворот через трансформ).
            var tip = new Vector2(16f, 0f);
            var backTop = new Vector2(-10f, 9f);
            var backMid = new Vector2(-4f, 0f);
            var backBot = new Vector2(-10f, -9f);

            float cos = Mathf.Cos(e.AngleRad);
            float sin = Mathf.Sin(e.AngleRad);
            Vector2 Rot(Vector2 v) => new(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);

            var points = new Vector2[]
            {
                e.Point + Rot(tip),
                e.Point + Rot(backTop),
                e.Point + Rot(backMid),
                e.Point + Rot(backBot),
            };
            DrawPolygon(points, new[] { color });

            // Обводка для контраста на любом фоне.
            DrawPolyline(points, new Color(0.05f, 0.02f, 0.02f, 0.8f * alpha), 1.5f, true);
        }
    }
}
