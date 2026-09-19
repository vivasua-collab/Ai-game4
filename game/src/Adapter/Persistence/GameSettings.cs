#nullable enable
// Создано: 2026-09-03 — M2: рантайм-настройки игры (user://settings.json).
// Требование пользователя: чит-меню должно быть отключаемым в настройках.
//
// 2026-09-19 (R18-1): + ShowEnemyVitals — глобальный тумблер индикации
// врагов (HP-бары над врагами + всплывающие цифры урона над НЕ-игроком).
// Требование пользователя: «на высокой сложности игры не будет индикации
// как урона, так и жизни противников» — подготовка системы сложности.
// Урон ПО игроку (цифры над игроком, индикатор направления) НЕ гейтится —
// это обратная связь о состоянии самого игрока.
//
// Хранение: user://settings.json (Godot user dir, вне git-репо, переживает
// перезапуски). Загрузка — при старте сцен (MainMenu._Ready, GameWorld._Ready);
// сохранение — сразу при изменении (SetCheatsEnabled / SetShowEnemyVitals).
//
// ПРИНЦИП: DEBUG-сборка компилирует CheatPanel (#if DEBUG), но РАНТАЙМ-доступ
// к нему гейтится CheatsEnabled — позволяет играть в dev-сборке «без читов».
// Release-сборка не содержит панель физически (#if DEBUG) — двойная защита.
using System;
using Godot;

namespace CultivationGame.Adapter.Persistence
{
    /// <summary>
    /// Рантайм-настройки игры. Статический класс: настройки глобальны для
    /// сессии, читаются из user://settings.json.
    /// </summary>
    public static class GameSettings
    {
        private const string SettingsPath = "user://settings.json";

        private static bool _loaded;

        /// <summary>
        /// Доступ к чит-меню (F2). По умолчанию true — dev-сборка для
        /// разработки и тестирования. Игрок может отключить в настройках
        /// главного меню.
        /// </summary>
        public static bool CheatsEnabled { get; private set; } = true;

        /// <summary>
        /// 2026-09-19 (R18-1): показывать индикацию врагов — HP-бары над
        /// врагами (NPC + животные) и всплывающие цифры урона над ними.
        /// По умолчанию true. Планируемая высокая сложность выключит это
        /// (никакой информации о состоянии противника).
        /// </summary>
        public static bool ShowEnemyVitals { get; private set; } = true;

        /// <summary>Гарантировать загрузку (idempotent, безопасно звать часто).</summary>
        public static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            Load();
        }

        /// <summary>Установить и сохранить флаг чит-меню.</summary>
        public static void SetCheatsEnabled(bool enabled)
        {
            CheatsEnabled = enabled;
            Save();
        }

        /// <summary>Установить и сохранить флаг индикации врагов (R18-1).</summary>
        public static void SetShowEnemyVitals(bool show)
        {
            ShowEnemyVitals = show;
            Save();
        }

        private static void Load()
        {
            try
            {
                if (!FileAccess.FileExists(SettingsPath)) return;
                using var f = FileAccess.Open(SettingsPath, FileAccess.ModeFlags.Read);
                if (f == null) return;
                string json = f.GetAsText();
                // Минимальный JSON-парс (флаги — без зависимостей):
                // ищем "cheatsEnabled"/"showEnemyVitals": true/false
                if (json.Contains("\"cheatsEnabled\": false", StringComparison.OrdinalIgnoreCase))
                    CheatsEnabled = false;
                else if (json.Contains("\"cheatsEnabled\": true", StringComparison.OrdinalIgnoreCase))
                    CheatsEnabled = true;
                if (json.Contains("\"showEnemyVitals\": false", StringComparison.OrdinalIgnoreCase))
                    ShowEnemyVitals = false;
                else if (json.Contains("\"showEnemyVitals\": true", StringComparison.OrdinalIgnoreCase))
                    ShowEnemyVitals = true;
            }
            catch (Exception e)
            {
                GD.Print($"[GameSettings] Load failed: {e.Message} — using defaults");
            }
        }

        private static void Save()
        {
            try
            {
                using var f = FileAccess.Open(SettingsPath, FileAccess.ModeFlags.Write);
                if (f == null)
                {
                    GD.Print("[GameSettings] Save failed: cannot open user://settings.json");
                    return;
                }
                f.StoreString($"{{\n  \"cheatsEnabled\": {(CheatsEnabled ? "true" : "false")},\n  \"showEnemyVitals\": {(ShowEnemyVitals ? "true" : "false")}\n}}\n");
            }
            catch (Exception e)
            {
                GD.Print($"[GameSettings] Save failed: {e.Message}");
            }
        }
    }
}
