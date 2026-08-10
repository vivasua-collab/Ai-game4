# Индексация спрайтов проекта — Cultivation World Simulator

**Дата создания:** 2026-04-13 14:18:00 UTC  
**Всего спрайтов:** 133 PNG файла  
**Источник:** Code + PNG (meta файлы НЕ в окружении, на локальном ПК)  
**Редактировано:** 2025-05-01

---

## 📎 Связанные документы

| Документ | Путь | Описание |
|----------|------|----------|
| **EQUIPMENT_SPRITES_AUDIT.md** | `UnityProject/Legacy/docs_audit_docs/EQUIPMENT_SPRITES_AUDIT.md` | Аудит недостающих спрайтов экипировки, план генерации icon + equipped |
| **EQUIPPED_SPRITES_DRAFT.md** | `UnityProject/docs_temp/EQUIPPED_SPRITES_DRAFT.md` | Черновик системы equipped-спрайтов (архитектура Overlay, спецификация, код) |
| **CharacterSpriteMirroring.md** | `UnityProject/docs_temp/CharacterSpriteMirroring.md` | Зеркалирование спрайтов персонажей (flipX / scale), интеграция с PlayerController |

---

## 1. 🟩 Terrain Tiles (Тайлы поверхности) — 10 спрайтов

**Путь:** `Assets/Sprites/Tiles/`  
**Генератор:** `TileSpriteGenerator.cs` → `Assets/Scripts/Tile/Editor/TileSpriteGenerator.cs`  
**ScriptableObject:** `TerrainTile.cs` → `Assets/Scripts/Tile/TerrainTile.cs`  
**Enum:** `TerrainType` (в `TileEnums.cs`)  
**Размер:** 64×64 px, Perlin noise вариация

| # | Имя файла | TerrainType | Цвет | Описание |
|---|-----------|-------------|------|----------|
| 1 | terrain_grass.png | Grass (1) | Зелёный (0.4, 0.7, 0.3) | Трава, базовая проходимость |
| 2 | terrain_dirt.png | Dirt (2) | Коричневый (0.6, 0.4, 0.2) | Земля, базовая проходимость |
| 3 | terrain_stone.png | Stone (3) | Серый (0.5, 0.5, 0.55) | Камень, базовая проходимость |
| 4 | terrain_water_shallow.png | Water_Shallow (4) | Голубой полупрозрач. (0.3, 0.5, 0.8, α=0.8) | Мелкая вода, замедление |
| 5 | terrain_water_deep.png | Water_Deep (5) | Тёмно-синий полупрозрач. (0.2, 0.3, 0.7, α=0.9) | Глубокая вода, требует навык |
| 6 | terrain_sand.png | Sand (6) | Жёлтый (0.9, 0.85, 0.6) | Песок, небольшое замедление |
| 7 | terrain_snow.png | Snow (7) | Белый (0.95, 0.95, 1.0) | Снег, замедление |
| 8 | terrain_ice.png | Ice (8) | Ледяной (0.7, 0.85, 0.95) + блики | Лёд, скольжение |
| 9 | terrain_lava.png | Lava (9) | Красный (0.9, 0.3, 0.05) + яркие прожилки | Лава, урон |
| 10 | terrain_void.png | Void (10) | Чёрный (0.1, 0.1, 0.1) | Пустота, непроходимо |

> ⚠️ .asset файлы создаются Phase 14 FullSceneBuilder только для 7 типов (Grass..Void).  
> Snow, Ice, Lava — спрайты есть, но .asset ещё не генерируются.

---

## 2. 🌲 Object Tiles (Объекты на тайлах) — 7 спрайтов

**Путь:** `Assets/Sprites/Tiles/`  
**Генератор:** `TileSpriteGenerator.cs`  
**ScriptableObject:** `ObjectTile.cs` → `Assets/Scripts/Tile/ObjectTile.cs`  
**Enum:** `TileObjectType` (в `TileEnums.cs`)  
**Размер:** 64×64 px, прозрачный фон + фигура

| # | Имя файла | TileObjectType | Форма | Описание |
|---|-----------|---------------|-------|----------|
| 1 | obj_tree.png | Tree_Oak (100) | Ствол + треугольная крона | Дерево |
| 2 | obj_rock_small.png | Rock_Small (200) | Маленький эллипс | Малый камень |
| 3 | obj_rock_medium.png | Rock_Medium (201) | Средний эллипс + осколки | Средний камень |
| 4 | obj_bush.png | Bush (110) | 3 эллипса-куст | Куст |
| 5 | obj_chest.png | Chest (500) | Прямоугольник + крышка + замок | Сундук (интерактивный) |
| 6 | obj_ore_vein.png | OreVein (520) | Камень + золотистые вкрапления | Рудная жила (собираемый) |
| 7 | obj_herb.png | Herb (530) | Стебель + листья + цветок | Трава (собираемая) |

> ⚠️ .asset файлы создаются Phase 14 только для 5 типов (Tree, RockSmall, RockMedium, Bush, Chest).  
> OreVein, Herb — спрайты есть, но .asset ещё не генерируются.

---

## 3. 🧑 Player (Игрок) — 9 спрайтов

**Путь:** `Assets/Sprites/Characters/Player/` + `Assets/Sprites/player_sprite.png`  
**Назначение:** SpriteRenderer на объекте Player  
**📎 См. также:** `UnityProject/docs_temp/CharacterSpriteMirroring.md` — зеркалирование и интеграция с контроллером

| # | Имя файла | Описание |
|---|-----------|----------|
| 1 | player_sprite.png | Базовый спрайт игрока (корень Sprites/) |
| 2 | player_variant1_cultivator.png | Вариант: Культист |
| 3 | player_variant2_cultivator.png | Вариант: Культист 2 |
| 4 | player_variant3_warrior.png | Вариант: Воин |
| 5 | player_variant4_warrior.png | Вариант: Воин 2 |
| 6 | player_variant5_advanced.png | Вариант: Продвинутый |
| 7 | player_variant6_immortal.png | Вариант: Бессмертный |
| 8 | player_variant7_mysterious.png | Вариант: Таинственный |
| 9 | player_variant8_monk.png | Вариант: Монах |

---

## 4. 👤 NPC (Неигровые персонажи) — 18 спрайтов

**Путь:** `Assets/Sprites/Characters/NPC/`  
**Связанные классы:** NPCController, NPCGenerator, NPCPresetData, NPCVisual  
**📎 См. также:** `UnityProject/docs_temp/CharacterSpriteMirroring.md` — зеркалирование и интеграция с контроллером

### Маппинг NPCRole → спрайт (в NPCVisual.cs)

| NPCRole | Приоритетный спрайт | Вариации | Fallback |
|---------|--------------------|---------|----------|
| Monster | npc_monster_wild | npc_beast_cultivator | программный гуманоид |
| Guard | npc_guard | npc_guard_female | программный гуманоид |
| Merchant | npc_merchant | npc_merchant_female | программный гуманоид |
| Cultivator | npc_cultivator_master | npc_disciple_male, npc_disciple_female | программный гуманоид |
| Elder | npc_elder_master | npc_village_elder | программный гуманоид |
| Enemy | npc_enemy_demonic | npc_rival | программный гуманоид |
| Disciple | npc_disciple_male | npc_disciple_female | программный гуманоид |
| Passerby | npc_passerby | npc_villager_male, npc_villager_female | программный гуманоид |

### Все NPC-спрайты

| # | Имя файла | Тип NPC | Новый |
|---|-----------|---------|-------|
| 1 | npc_monster_wild.png | Дикая тварь (Monster) | ✅ |
| 2 | npc_passerby.png | Прохожий (Passerby) | ✅ |
| 3 | npc_merchant_female.png | Торговец (жен.) | ✅ |
| 4 | npc_guard_female.png | Стражник (жен.) | ✅ |
| 5 | npc_cultivator_master.png | Мастер-культиватор | ✅ |
| 6 | npc_villager_female.png | Жительница деревни | ✅ |
| 7 | npc_guard.png | Стражник (муж.) | |
| 8 | npc_merchant.png | Торговец (муж.) | |
| 9 | npc_villager_male.png | Житель деревни (муж.) | |
| 10 | npc_disciple_male.png | Ученик (муж.) | |
| 11 | npc_disciple_female.png | Ученица (жен.) | |
| 12 | npc_elder_master.png | Старший мастер | |
| 13 | npc_village_elder.png | Старейшина деревни | |
| 14 | npc_enemy_demonic.png | Демонический враг | |
| 15 | npc_rival.png | Соперник | |
| 16 | npc_beast_cultivator.png | Звериный культиватор | |
| 17 | npc_rogue.png | Разбойник | |
| 18 | npc_fairy.png | Фея | |

---

## 5. ⚔️ Equipment (Снаряжение) — 20 спрайтов

**Путь:** `Assets/Sprites/Equipment/`  
**ScriptableObject:** EquipmentData.cs  
**Генератор:** AssetGeneratorExtended.cs, WeaponGenerator.cs, ArmorGenerator.cs  
**📎 См. также:**  
- `UnityProject/Legacy/docs_audit_docs/EQUIPMENT_SPRITES_AUDIT.md` — аудит недостающих icon + equipped спрайтов  
- `UnityProject/docs_temp/EQUIPPED_SPRITES_DRAFT.md` — черновик системы equipped-спрайтов (архитектура Overlay Layering)

### Оружие (11)

| # | Имя файла | Тип |
|---|-----------|-----|
| 1 | weapon_sword_iron.png | Меч железный |
| 2 | weapon_sword_spirit.png | Меч духовный |
| 3 | weapon_greatsword_iron.png | Двуручный меч |
| 4 | weapon_dagger_iron.png | Кинжал |
| 5 | weapon_axe_iron.png | Топор |
| 6 | weapon_spear_iron.png | Копьё |
| 7 | weapon_bow_wood.png | Лук деревянный |
| 8 | weapon_crossbow_iron.png | Арбалет |
| 9 | weapon_staff_wood.png | Посох деревянный |
| 10 | weapon_staff_jade.png | Посох нефритовый |
| 11 | weapon_claws.png | Когти |

### Броня (9)

| # | Имя файла | Тип |
|---|-----------|-----|
| 1 | armor_robe_cloth.png | Мантия тканевая |
| 2 | armor_robe_spirit.png | Мантия духовная |
| 3 | armor_vest_leather.png | Жилет кожаный |
| 4 | armor_hood_cloth.png | Капюшон тканевый |
| 5 | armor_boots_leather.png | Сапоги кожаные |
| 6 | armor_gloves_leather.png | Перчатки кожаные |
| 7 | armor_chainmail.png | Кольчуга |
| 8 | armor_helmet_iron.png | Шлем железный |
| 9 | armor_greaves_iron.png | Поножи железные |
| 10 | armor_torso_iron.png | Нагрудник железный |

---

## 6. 📦 Items (Предметы) — 16 спрайтов

**Путь:** `Assets/Sprites/Items/`  
**ScriptableObject:** ItemData.cs, MaterialData.cs

### Расходуемые (7)

| # | Имя файла | Тип |
|---|-----------|-----|
| 1 | consumable_bread.png | Хлеб |
| 2 | consumable_meat.png | Мясо |
| 3 | consumable_healing_pill.png | Пилюля исцеления |
| 4 | consumable_qi_pill.png | Пилюля Ци |
| 5 | consumable_breakthrough_pill.png | Пилюля прорыва |
| 6 | consumable_antidote.png | Противоядие |
| 7 | consumable_scroll.png | Свиток |

### Материалы (8)

| # | Имя файла | Тип |
|---|-----------|-----|
| 1 | material_iron_ore.png | Железная руда |
| 2 | material_iron_ingot.png | Железный слиток |
| 3 | material_spirit_iron.png | Духовное железо |
| 4 | material_star_metal.png | Звёздный металл |
| 5 | material_leather.png | Кожа |
| 6 | material_cloth.png | Ткань |
| 7 | material_jade.png | Нефрит |
| 8 | material_spirit_stone.png | Духовный камень |

---

## 7. 🔥 Elements (Стихии) — 7 спрайтов

**Путь:** `Assets/Sprites/Elements/`  
**ScriptableObject:** ElementData.cs

| # | Имя файла | Стихия |
|---|-----------|--------|
| 1 | element_fire.png | Огонь |
| 2 | element_water.png | Вода |
| 3 | element_earth.png | Земля |
| 4 | element_air.png | Воздух |
| 5 | element_lightning.png | Молния |
| 6 | ~~element_poison.png~~ | ~~Яд~~ ⚠️ DEPRECATED — не входит в текущий набор стихий |
| 7 | element_void.png | Пустота |
| 8 | element_neutral.png | Нейтральная |

---

## 8. ✨ Techniques (Техники) — 11 спрайтов

**Путь:** `Assets/Sprites/Techniques/`  
**ScriptableObject:** TechniqueData.cs

| # | Имя файла | Тип техники |
|---|-----------|-------------|
| 1 | technique_melee.png | Ближний бой |
| 2 | technique_ranged.png | Дальний бой |
| 3 | technique_defense.png | Защита |
| 4 | technique_healing.png | Исцеление |
| 5 | technique_cultivation.png | Культивация |
| 6 | technique_support.png | Поддержка |
| 7 | technique_movement.png | Перемещение |
| 8 | technique_poison.png | Яд |
| 9 | technique_curse.png | Проклятие |
| 10 | technique_sensory.png | Чувственное восприятие |
| 11 | technique_formation.png | Формация |

---

## 9. 🧘 Cultivation (Культивация) — 10 спрайтов

**Путь:** `Assets/Sprites/Cultivation/`  
**ScriptableObject:** CultivationLevelData.cs

| # | Имя файла | Уровень культивации |
|---|-----------|---------------------|
| 1 | cultivation_01_awakened_core.png | Пробуждённое ядро |
| 2 | cultivation_02_life_flow.png | Поток жизни |
| 3 | cultivation_03_internal_fire.png | Внутренний огонь |
| 4 | cultivation_04_body_spirit_union.png | Единение тела и духа |
| 5 | cultivation_05_heart_of_heaven.png | Сердце небес |
| 6 | cultivation_06_veil_breaker.png | Разрыв завесы |
| 7 | cultivation_07_eternal_ring.png | Вечное кольцо |
| 8 | cultivation_08_voice_of_heaven.png | Голос небес |
| 9 | cultivation_09_immortal_core.png | Бессмертное ядро |
| 10 | cultivation_10_ascension.png | Вознесение ⚠️ ПЛАНИРУЕТСЯ — не входит в текущую модель данных (1-9) |

---

## 10. 💥 Combat Effects (Боевые эффекты) — 12 спрайтов

**Путь:** `Assets/Sprites/Combat/TechniqueEffects/`  
**Связанные классы:** TechniqueEffect, TechniqueEffectFactory, ExpandingEffect, DirectionalEffect

| # | Имя файла | Эффект |
|---|-----------|--------|
| 1 | effect_fire_slash.png | Огненный разруб |
| 2 | effect_water_wave.png | Водяная волна |
| 3 | effect_earth_spike.png | Земляной шип |
| 4 | effect_air_blade.png | Воздушный клинок |
| 5 | effect_lightning_bolt.png | Молния |
| 6 | effect_void_rift.png | Разлом пустоты |
| 7 | effect_poison_cloud.png | Ядовитое облако |
| 8 | effect_qi_explosion.png | Взрыв Ци |
| 9 | effect_defense_barrier.png | Защитный барьер |
| 10 | effect_healing_aura.png | Исцеляющая аура |
| 11 | effect_formation_array.png | Массив формации |
| 12 | effect_mist_expanding.png | Расширяющийся туман |

---

## 11. 🌀 Orbital Weapons (Орбитальное оружие) — 8 спрайтов

**Путь:** `Assets/Sprites/Combat/OrbitalWeapons/`  
**Связанные классы:** OrbitalWeapon, OrbitalWeaponController

| # | Имя файла | Оружие |
|---|-----------|--------|
| 1 | orbital_sword_fire.png | Огненный меч |
| 2 | orbital_sword_cyan.png | Ледяной меч |
| 3 | orbital_dagger_purple.png | Фиолетовый кинжал |
| 4 | orbital_spear_green.png | Зелёное копьё |
| 5 | orbital_axe_golden.png | Золотой топор |
| 6 | orbital_staff_blue.png | Синий посох |
| 7 | orbital_fans_wind.png | Веера ветра |
| 8 | orbital_ring_golden.png | Золотое кольцо |

---

## 12. 🖼️ UI (Интерфейс) — 4 спрайта

**Путь:** `Assets/Sprites/UI/`  
**Связанные классы:** HUDController, CultivationProgressBar, CharacterPanelUI

| # | Имя файла | Назначение |
|---|-----------|------------|
| 1 | ui_health.png | Иконка здоровья |
| 2 | ui_qi.png | Иконка Ци |
| 3 | ui_stamina.png | Иконка выносливости |
| 4 | ui_cultivation.png | Иконка культивации |

---

## Сводная статистика

| Категория | Количество | Путь |
|-----------|-----------|------|
| Terrain Tiles | 10 | Sprites/Tiles/ |
| Object Tiles | 7 | Sprites/Tiles/ |
| Player | 9 | Sprites/ + Sprites/Characters/Player/ |
| NPC | 18 | Sprites/Characters/NPC/ |
| Equipment | 20 | Sprites/Equipment/ |
| Items | 16 | Sprites/Items/ |
| Elements | 7 | Sprites/Elements/ |
| Techniques | 11 | Sprites/Techniques/ |
| Cultivation | 10 | Sprites/Cultivation/ |
| Combat Effects | 12 | Sprites/Combat/TechniqueEffects/ |
| Orbital Weapons | 8 | Sprites/Combat/OrbitalWeapons/ |
| UI | 4 | Sprites/UI/ |
| **ИТОГО** | **132** | |

---

## Покрытие Enum → Спрайты

### TerrainType (11 значений) → 10 спрайтов ✅ ПОЛНОЕ
- ✅ Grass, Dirt, Stone, Water_Shallow, Water_Deep, Sand, Snow, Ice, Lava, Void
- ➖ None (не нужен спрайт — отсутствие тайла)

### TileObjectType (21 значение) → 7 спрайтов ⚠️ ЧАСТИЧНОЕ (33%)
- ✅ Tree (→Tree_Oak), Rock_Small, Rock_Medium, Bush, Chest, OreVein, Herb
- ❌ Tree_Pine, Tree_Birch, Bush_Berry, Grass_Tall, Flower, Rock_Large, Boulder, Pond, Well, Wall_Wood, Wall_Stone, Door, Window, Shrine, Altar — спрайтов нет

### ElementData (7 записей) → 7 спрайтов ✅ ПОЛНОЕ
- ⚠️ element_poison.png DEPRECATED — не входит в текущий набор стихий

### CultivationLevelData (9 записей) → 10 спрайтов ⚠️ 1 ПЛАНИРУЕТСЯ
- ✅ Уровни 1-9
- ⚠️ cultivation_10_ascension.png — ПЛАНИРУЕТСЯ (не входит в текущую модель данных)

### TechniqueType (11 типов) → 11 спрайтов ✅ ПОЛНОЕ

### NPCRole (8 ролей) → 18 спрайтов ✅ ПОЛНОЕ
- ✅ Monster → npc_monster_wild + npc_beast_cultivator (вариация)
- ✅ Guard → npc_guard + npc_guard_female (вариация)
- ✅ Merchant → npc_merchant + npc_merchant_female (вариация)
- ✅ Cultivator → npc_cultivator_master + npc_disciple_male + npc_disciple_female (вариации)
- ✅ Elder → npc_elder_master + npc_village_elder (вариация)
- ✅ Enemy → npc_enemy_demonic + npc_rival (вариация)
- ✅ Disciple → npc_disciple_male + npc_disciple_female (вариация)
- ✅ Passerby → npc_passerby + npc_villager_male + npc_villager_female (вариации)
- ➕ Дополнительные (не привязаны к роли): npc_rogue, npc_fairy

---

## Примечания

1. **Meta файлы** — НЕ в окружении. На локальном ПК. Спрайты генерируются через код (TileSpriteGenerator), остальные — через AssetGenerator/AssetGeneratorExtended.
2. **TempPlayerSprite.png** — в корне `Assets/`, временный спрайт для Player.
3. **Качество** — Все тайл-спрайты процедурные (Perlin noise), не художественные. Подходят для прототипирования.
4. **Tile .asset покрытие** — Phase 14 FullSceneBuilder создаёт .asset только для 12 из 17 спрайтов (7 terrain + 5 object). Snow, Ice, Lava, OreVein, Herb не покрываются.
