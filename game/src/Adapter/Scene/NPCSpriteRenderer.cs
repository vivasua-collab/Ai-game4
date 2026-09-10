#nullable enable
// Создано: 2026-08-22 — NPC_COMBAT_PREP Phase 1: рендер NPC.
// NPCSpriteRenderer — Godot Node2D that draws colored circles for each
// spawned human NPC. Re-queries NPCService every frame so NPCs are drawn
// at their current position (NPCMovementService updates positions per tick).
// Источник: docs/docs_v2/09_workflow/NPC_COMBAT_PREP.md §Phase 1
// Редактировано: 2026-08-25 — Phase 7: HP-бар над NPC при повреждении
// (IBodyDataProvider.GetCurrentHealth/GetMaxHealth; бар скрыт, пока HP полный).
// Редактировано: 2026-09-10 R15 — overlay оружия в руке NPC (hand-спрайт
// из WeaponVisualCatalog; кэш npcId→itemId, перескан 0.5с — NPC не
// публикует EquipmentChangedEvent; flip по направлению движения с
// гистерезисом, DrawSetTransform-зеркалирование §16).
using Godot;
using System.Collections.Generic;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;
using CultivationGame.Adapter.Di;

namespace CultivationGame.Adapter.Scene;

/// <summary>
/// Renders spawned NPCs as colored circles on the world map. Color encodes
/// the NPC role (merchant=cold, cultivator=violet, guard=blue, passerby=grey).
/// ZIndex = RenderLayer.Objects (3) — same as animals, below the player (4).
/// Phase 7: рисует HP-бар над раненым NPC (текущий/полный RedHP).
/// R15: рисует overlay оружия в основной руке (WeaponVisualCatalog).
/// </summary>
public partial class NPCSpriteRenderer : Node2D
{
    [Inject] private INPCService? _npcService;
    [Inject] private IBodyDataProvider? _bodyProvider;
    // R15: экипировка NPC (per-entity провайдер; SetEquipment — NPCAssemblyService).
    [Inject] private IEquipmentDataProvider? _equipmentProvider;

    private int _tilePixels;
    private readonly List<string> _idSnapshot = new();

    // === R15: оружие в руке NPC ===
    // Кэш npcId → itemId экипированного оружия (перескан 0.5с в _PhysicsProcess:
    // NPC-экип не публикует EquipmentChangedEvent — SetEquipment мутует провайдер).
    private readonly Dictionary<string, string?> _npcWeaponIds = new();
    // Кэш itemId → hand-текстура (генерация один раз; ключи каталога — для QA).
    private readonly Dictionary<string, Texture2D> _weaponTexCache = new();
    private readonly Dictionary<string, string> _weaponKeyCache = new();
    // Facing: npcId → смотрит влево; обновление по дельте позиции с
    // гистерезисом (движение дёргается по тикам — обновляем только при
    // значимом |dx|, направление сохраняется между кадрами).
    private readonly Dictionary<string, bool> _npcFacingLeft = new();
    private readonly Dictionary<string, float> _npcLastX = new();
    private float _weaponRescanCooldown;

    private static readonly Color OutlineColour = new(0.05f, 0.04f, 0.02f, 0.85f);
    private static readonly Color ShadowColour = new(0f, 0f, 0f, 0.30f);

    public override void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null)
        {
            ContainerAdapter.InjectProperties(this, container);
        }
        _tilePixels = GameConstants.TILE_PIXELS;
        ZIndex = (int)RenderLayer.Objects;
        GD.Print("[NPCSpriteRenderer] Ready");
    }

    public override void _PhysicsProcess(double delta)
    {
        // R15: перескан оружия NPC (0.5с) — дёшево: GetEquipped по всем ID.
        _weaponRescanCooldown -= (float)delta;
        if (_weaponRescanCooldown <= 0f)
        {
            _weaponRescanCooldown = 0.5f;
            RescanNpcWeapons();
        }
        QueueRedraw();
    }

    /// <summary>
    /// R15: пересканировать WeaponMain всех живых NPC → кэш id→itemId.
    /// Текстура резолвится в _Draw один раз (по itemId, отдельный кэш).
    /// </summary>
    private void RescanNpcWeapons()
    {
        if (_npcService == null) return;
        _npcWeaponIds.Clear();
        if (_equipmentProvider == null) return;
        foreach (var id in _npcService.GetAllNPCIds())
        {
            if (!_npcService.IsAlive(id)) continue;
            var weapon = _equipmentProvider.GetEquipped(id, EquipmentSlot.WeaponMain);
            _npcWeaponIds[id] = weapon?.ItemId;
        }
    }

    // Cached sprites per role.
    private readonly Dictionary<NPCRole, Texture2D> _spriteCache = new();

    public override void _Draw()
    {
        if (_npcService == null) return;

        _idSnapshot.Clear();
        foreach (var id in _npcService.GetAllNPCIds())
            _idSnapshot.Add(id);

        float halfTile = _tilePixels * 0.5f;

        foreach (var id in _idSnapshot)
        {
            var npc = _npcService.GetNPC(id);
            if (npc == null || !_npcService.IsAlive(id)) continue;

            float cx = npc.Position.X * _tilePixels + halfTile;
            float cy = npc.Position.Y * _tilePixels + halfTile;

            // Get or create sprite for this role.
            if (!_spriteCache.TryGetValue(npc.Role, out var tex))
            {
                tex = ProceduralSpriteGenerator.CreateNPCSprite(npc.Role);
                _spriteCache[npc.Role] = tex;
            }

            // Draw sprite centered on tile.
            float spriteSize = tex.GetWidth();
            var pos = new Vector2(cx - spriteSize / 2f, cy - spriteSize / 2f);
            DrawTexture(tex, pos);

            // R15: overlay оружия в руке — hand-спрайт поверх тела.
            DrawNpcWeapon(id, cx, cy, spriteSize);

            // Phase 7: HP-бар над NPC — только если повреждён (полный HP не рисуем,
            // чтобы не засорять HUD в мирное время). Высота полосы — над спрайтом.
            // 2026-09-04 S1 (VLM-аудит): + имя и уровень NPC над баром —
            // информативность боя (видно КТО ранен и его силу).
            if (_bodyProvider != null)
            {
                int hp = _bodyProvider.GetCurrentHealth(id);
                int maxHp = _bodyProvider.GetMaxHealth(id);
                if (maxHp > 0 && hp < maxHp)
                {
                    float barTop = cy - spriteSize / 2f - 8f;
                    DrawNpcHealthBar(cx, barTop, hp, maxHp);

                    var st = _npcService.GetNPCState(id);
                    if (st != null)
                    {
                        int lvl = (int)st.CultivationLevel;
                        string nameText = lvl > 0
                            ? $"{st.DisplayName} · L{lvl} · {hp}HP"
                            : $"{st.DisplayName} · {hp}HP";
                        DrawNpcNamePlate(cx, barTop, nameText);
                    }
                }
            }
        }
    }

    /// <summary>
    /// R15: overlay оружия NPC. Топ-левел hand-текстуры = топ-левел тела +
    /// HandOffset (зеркалирование — ТОЛЬКО X). Facing: гистерезис — флип
    /// обновляется при |Δx| ≥ 0.05 тайла, направление сохраняется (движение
    /// дёргается по тикам). Зеркалирование содержимого — DrawSetTransform
    /// (-1,1) вокруг вертикальной оси через cx (SPRITE_CATALOG §16).
    /// </summary>
    private void DrawNpcWeapon(string npcId, float cx, float cy, float spriteSize)
    {
        if (_equipmentProvider == null) return;
        if (!_npcWeaponIds.TryGetValue(npcId, out var itemId) || string.IsNullOrEmpty(itemId))
            return;

        // Facing-гистерезис по позиции (позиции — в тайлах).
        var npc = _npcService?.GetNPC(npcId);
        if (npc != null)
        {
            if (_npcLastX.TryGetValue(npcId, out var lastX))
            {
                float dx = npc.Position.X - lastX;
                if (dx > 0.05f) _npcFacingLeft[npcId] = false;
                else if (dx < -0.05f) _npcFacingLeft[npcId] = true;
            }
            _npcLastX[npcId] = npc.Position.X;
        }
        bool facingLeft = _npcFacingLeft.TryGetValue(npcId, out var left) && left;

        // Текстура: кэш по itemId (генерация ленивая, через каталог).
        if (!_weaponTexCache.TryGetValue(itemId!, out var handTex))
        {
            var cachedWeapon = _equipmentProvider.GetEquipped(npcId, EquipmentSlot.WeaponMain);
            var visuals = WeaponVisualCatalog.Resolve(cachedWeapon);
            if (visuals == null)
            {
                // ItemId есть, но предмет не резолвится (не оружие) — negative-кэш.
                _weaponTexCache[itemId!] = null!;
                _weaponKeyCache[itemId!] = "";
                return;
            }
            handTex = visuals.Hand;
            _weaponTexCache[itemId!] = handTex;
            _weaponKeyCache[itemId!] = visuals.Key;
        }
        if (handTex == null) return; // negative-кэш

        // Позиция: топ-левел тела + HandOffset (зеркалирование только X).
        var offWeapon = _equipmentProvider.GetEquipped(npcId, EquipmentSlot.WeaponMain);
        string classId = offWeapon != null ? WeaponVisualCatalog.WeaponClassOf(offWeapon) : WeaponVisualCatalog.DefaultClass;
        var off = WeaponVisualCatalog.HandOffset(classId);
        var topLeft = new Vector2(cx - spriteSize / 2f + off.X, cy - spriteSize / 2f + off.Y);

        if (facingLeft)
        {
            // Зеркало вокруг оси x=cx: рисуем в ТОЙ ЖЕ позиции, transform
            // отразит и положение, и содержимое (x' = 2cx − x).
            DrawSetTransform(new Vector2(2f * cx, 0f), 0f, new Vector2(-1f, 1f));
            DrawTexture(handTex, topLeft);
            DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
        }
        else
        {
            DrawTexture(handTex, topLeft);
        }
    }

    // === R15: QA-доступ (GODOT_WEAPONVIS_DEBUG) ===

    /// <summary>QA: ключ hand-текстуры оружия NPC (null — нет оружия/не рисуется).</summary>
    public string? NPCWeaponTextureIdOf(string npcId)
    {
        return _npcWeaponIds.TryGetValue(npcId, out var itemId)
            && !string.IsNullOrEmpty(itemId)
            && _weaponKeyCache.TryGetValue(itemId!, out var key)
            && !string.IsNullOrEmpty(key)
            ? key
            : null;
    }

    /// <summary>QA: число NPC с видимым overlay оружия.</summary>
    public int NPCWeaponVisibleCount
    {
        get
        {
            int n = 0;
            foreach (var kvp in _npcWeaponIds)
            {
                if (string.IsNullOrEmpty(kvp.Value)) continue;
                if (_weaponKeyCache.TryGetValue(kvp.Value!, out var key) && !string.IsNullOrEmpty(key))
                    n++;
            }
            return n;
        }
    }

    /// <summary>
    /// Phase 7: компактный HP-бар (48×5 px): тёмная подложка + заливка по ratio.
    /// Цвет: зелёный > 50%, жёлтый > 25%, красный ниже (семантика BodyStatusPanel).
    /// </summary>
    private void DrawNpcHealthBar(float cx, float top, int hp, int maxHp)
    {
        const float barWidth = 48f;
        const float barHeight = 5f;
        float ratio = maxHp > 0 ? (float)hp / maxHp : 0f;

        // Подложка.
        DrawRect(new Rect2(cx - barWidth / 2f, top, barWidth, barHeight),
            new Color(0.05f, 0.04f, 0.02f, 0.8f));

        // Заливка.
        var fillColour = ratio > 0.5f
            ? new Color(0.30f, 0.75f, 0.30f)
            : ratio > 0.25f
                ? new Color(0.85f, 0.75f, 0.25f)
                : new Color(0.85f, 0.25f, 0.20f);
        float fillWidth = barWidth * ratio;
        if (fillWidth > 0.5f)
            DrawRect(new Rect2(cx - barWidth / 2f, top, fillWidth, barHeight), fillColour);

        // Тонкая рамка.
        DrawRect(new Rect2(cx - barWidth / 2f, top, barWidth, barHeight),
            new Color(0f, 0f, 0f, 0.5f), false, 1f);
    }

    /// <summary>
    /// 2026-09-04 S1: нейм-плейт над HP-баром NPC: «Имя · L{уровень} · {hp}HP».
    /// Рисуется только вместе с HP-баром (NPC повреждён → бой идёт).
    /// Центрируется над баром (паттерн FormationVisualRenderer), тень для
    /// читаемости на любом фоне.
    /// </summary>
    private void DrawNpcNamePlate(float cx, float barTop, string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        var font = ThemeDB.FallbackFont;
        const int fontSize = 10;
        const float plateWidth = 96f;
        var pos = new Vector2(cx - plateWidth / 2f, barTop - 4f);

        // Тень (контраст на любом фоне) + основной текст.
        DrawString(font, pos + new Vector2(1, 1), text,
            HorizontalAlignment.Center, plateWidth, fontSize,
            new Color(0, 0, 0, 0.75f));
        DrawString(font, pos, text,
            HorizontalAlignment.Center, plateWidth, fontSize,
            new Color(0.95f, 0.9f, 0.8f));
    }

    private static Color GetColourForRole(NPCRole role) => role switch
    {
        NPCRole.Merchant   => new Color(0.20f, 0.55f, 0.60f), // teal
        NPCRole.Cultivator => new Color(0.55f, 0.30f, 0.70f), // violet
        NPCRole.Guard      => new Color(0.25f, 0.40f, 0.75f), // blue
        NPCRole.Elder      => new Color(0.75f, 0.60f, 0.25f), // gold
        NPCRole.Monster    => new Color(0.60f, 0.20f, 0.20f), // red
        NPCRole.Enemy      => new Color(0.70f, 0.35f, 0.15f), // orange
        _                  => new Color(0.62f, 0.62f, 0.58f), // passerby grey
    };
}
