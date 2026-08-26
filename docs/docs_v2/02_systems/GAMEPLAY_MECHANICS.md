# Игровые механики (индекс)

> **Статус:** V2 — навигационный индекс.
> Этот файл — каталог ссылок на канонические документы механик. Подробное описание каждой механики — в соответствующем файле. Дублирование содержимого здесь не ведётся.

---

## Механики

| # | Механика | Канонический документ |
|---|----------|-----------------------|
| 1 | Предметы на земле (drop/pickup, trash zone) | [06_player/GROUND_ITEM_SYSTEM.md](../06_player/GROUND_ITEM_SYSTEM.md) |
| 2 | Глобальный вес (инвентарь + экипировка, штрафы скорости) | [06_player/INVENTORY_SYSTEM.md §17](../06_player/INVENTORY_SYSTEM.md) |
| 3 | Сборка тела (Body Assembly, dual HP) | [02_systems/BODY_SYSTEM.md](../02_systems/BODY_SYSTEM.md) |
| 4 | Простые животные (wolf, deer, rabbit) | [04_entities/ANIMALS.md](../04_entities/ANIMALS.md) |
| 5 | Процедурные спрайты (runtime-генерация, кэширование) | [07_ui/PROCEDURAL_SPRITES.md](../07_ui/PROCEDURAL_SPRITES.md) |
| 6 | Генератор экипировки «Матрёшка» | [06_player/EQUIPMENT_SYSTEM.md §2](../06_player/EQUIPMENT_SYSTEM.md) |
| 7 | Быстрые слоты пояса (Belt Quick Slots) | [06_player/INVENTORY_SYSTEM.md §7](../06_player/INVENTORY_SYSTEM.md) |
| 8 | Диалоги (Dialogue System) | [06_player/DIALOGUE_SYSTEM.md](../06_player/DIALOGUE_SYSTEM.md) |
| 9 | Боевая диспозиция NPC (Hostile/Friendly/Neutral/Merchant) | [04_entities/NPC_AI_SYSTEM.md](../04_entities/NPC_AI_SYSTEM.md) |
| 10 | Смерть и лут (NPCDeathEvent, лут на земле, respawn игрока) | [04_entities/DEATH_AND_LOOT.md](../04_entities/DEATH_AND_LOOT.md) |
| 11 | Детерминированный бой (ICombatRng, seed=12345) | [02_systems/COMBAT_SYSTEM.md §Детерминированность (Q5)](../02_systems/COMBAT_SYSTEM.md) |
| 12 | Защита EventBus от re-entrancy | [01_architecture/DI_AND_EVENTBUS.md §Защита от re-entrancy (Q13)](../01_architecture/DI_AND_EVENTBUS.md) |
| 13 | Духовное хранилище (Spirit Storage) | [06_player/INVENTORY_SYSTEM.md §5](../06_player/INVENTORY_SYSTEM.md) |
| 14 | Оптимизация рендеринга (viewport culling, NEAREST, 500×500) | [01_architecture/PERFORMANCE_STRATEGY.md](../01_architecture/PERFORMANCE_STRATEGY.md) |

---

## Примечание к оружию (фикс рассогласования с кодом)

В коде (EQUIPMENT_SYSTEM §10.2) канонический список подтипов оружия (7):
**dagger, sword, axe, spear, greatsword (двуручный меч), bow, staff.**

«Алебарда» в списке НЕ присутствует — это рассинхрон между старой документацией и кодом. Использовать исключительно «двуручный меч» (greatsword).

---

## Связанные индексы

- [README.md](../README.md) — оглавление всех документов V2.
- [01_architecture/MODULE_STRUCTURE.md](../01_architecture/MODULE_STRUCTURE.md) — модули и сервисы.
- [00_overview/GLOSSARY.md](../00_overview/GLOSSARY.md) — термины.
