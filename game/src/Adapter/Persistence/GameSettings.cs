#nullable enable
// Создано: 2026-09-03 — M2: рантайм-настройки игры (user://settings.json).
// Требование пользователя: чит-меню должно быть отключаемым в настройках.
//
// Хранение: user://settings.json (Godot user dir, вне git-репо, переживает
// перезапуски). Загрузка — при старте сцен (MainMenu._Ready, GameWorld._Ready);
// сохранение — сразу при изменении (SetCheatsEnabled).
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

        private static void Load()
        {
            try
            {
                if (!FileAccess.FileExists(SettingsPath)) return;
                using var f = FileAccess.Open(SettingsPath, FileAccess.ModeFlags.Read);
                if (f == null) return;
                string json = f.GetAsText();
                // Минимальный JSON-парс (один bool-флаг — без зависимостей):
                // ищем "cheatsEnabled": true/false
                if (json.Contains("\"cheatsEnabled\": false", StringComparison.OrdinalIgnoreCase))
                    CheatsEnabled = false;
                else if (json.Contains("\"cheatsEnabled\": true", StringComparison.OrdinalIgnoreCase))
                    CheatsEnabled = true;
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
                f.StoreString($"{{\n  \"cheatsEnabled\": {(CheatsEnabled ? "true" : "false")}\n}}\n");
            }
            catch (Exception e)
            {
                GD.Print($"[GameSettings] Save failed: {e.Message}");
            }
        }
    }
}
