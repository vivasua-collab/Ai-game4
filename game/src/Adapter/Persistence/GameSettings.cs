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
// перезапуски). Загрузка — при старте сцен (MainMenu._Ready, GameWorld._Ready,
// EnsureLoaded внутри Set*); сохранение — сразу при изменении.
//
// ST17-004 (Этап-17 аудита, 24.10): безопасный JSON-разбор вместо буквальных
// Contains-шаблонов ("cheatsEnabled": false не совпадал с валидным компактным
// "cheatsEnabled":false — префикс Stage17-сима H1 FAIL). Контракты:
//   • разбор через Godot Json.ParseString — любые отступы/переносы/порядок/
//     неизвестные поля/компактная запись; ключи — регистронезависимо
//     (легаси-толерантность оригинального OrdinalIgnoreCase сохранена, H4);
//   • повреждённый/не-JSON файл → диагностика + канонические дефолты,
//     БЕЗ крэша (H3); значения не-bool → пропуск ключа с диагностикой;
//   • _loaded выставляется ТОЛЬКО после успешного завершения Load():
//     повреждённая/неудавшаяся загрузка ретраебельна — позже записанный
//     валидный файл подхватывается следующим EnsureLoaded (H7);
//   • Set* гарантируют загрузку ДО записи: изменение настройки до первой
//     загрузки больше НЕ затирает чужие сохранённые пользовательские
//     значения дефолтами (H6);
//   • «файла нет» = нормальное первое включение: дефолты финальны, ретраев
//     не требуется (FileExists не молотит на каждом чтении свойств).
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

        /// <summary>
        /// 2026-09-21 (R30-П4, репорт плейтеста): «Пауза при открытых окнах».
        /// true (по умолчанию) — модальные окна (инвентарь/книга/культивация
        /// и т.д.) ставят мир на паузу (Old School планирование).
        /// false — время ТЕЧЁТ, пока игрок изучает техники/инвентарь
        /// (можно промотать реген Ци/баффы, как просил игрок: «вдруг игрок
        /// захочет промотать время, пока изучает»). Индикатор ⏸ честно
        /// не зажигается — мир жив.
        /// </summary>
        public static bool PauseOnModalWindows { get; private set; } = true;

        /// <summary>
        /// П6 (репорт 21.09, «заметил подлагивания — нужен счётчик FPS»):
        /// показывать FPS-счётчик (левый-верхний угол HUD, F3 — тумбл).
        /// По умолчанию false (не мешает в обычной игре; включается для
        /// диагностики лагов — игрок сам решает, когда смотреть).
        /// </summary>
        public static bool ShowFpsCounter { get; private set; } = false;

        /// <summary>
        /// Гарантировать загрузку (idempotent, безопасно звать часто).
        /// ST17-004: флаг _loaded — только после УСПЕШНОЙ загрузки:
        /// неудавшаяся (повреждённый файл/ошибка чтения) остаётся
        /// ретраебельной — следующий вызов повторит попытку.
        /// </summary>
        public static void EnsureLoaded()
        {
            if (_loaded) return;
            if (Load()) _loaded = true;
        }

        /// <summary>Установить и сохранить флаг чит-меню.</summary>
        public static void SetCheatsEnabled(bool enabled)
        {
            // ST17-004: загрузить сохранённые значения ДО записи — иначе
            // Set до первой загрузки молча перезаписал бы файл дефолтами.
            EnsureLoaded();
            CheatsEnabled = enabled;
            Save();
        }

        /// <summary>Установить и сохранить флаг индикации врагов (R18-1).</summary>
        public static void SetShowEnemyVitals(bool show)
        {
            EnsureLoaded();
            ShowEnemyVitals = show;
            Save();
        }

        /// <summary>Установить и сохранить тумблер паузы окон (R30-П4).</summary>
        public static void SetPauseOnModalWindows(bool pause)
        {
            EnsureLoaded();
            PauseOnModalWindows = pause;
            Save();
        }

        /// <summary>Установить и сохранить тумблер FPS-счётчика (R30-П6).</summary>
        public static void SetShowFpsCounter(bool show)
        {
            EnsureLoaded();
            ShowFpsCounter = show;
            Save();
        }

        /// <summary>
        /// Загрузить настройки из user://settings.json.
        /// Возвращает true, если загрузку можно считать завершённой
        /// (файл отсутствует = первое включение, дефолты финальны, ИЛИ файл
        /// успешно разобран). false — ретраебельная неудача (ошибка чтения/
        /// повреждённый JSON): _loaded НЕ выставляется, следующий
        /// EnsureLoaded повторит попытку.
        /// </summary>
        private static bool Load()
        {
            try
            {
                if (!FileAccess.FileExists(SettingsPath)) return true;
                using var f = FileAccess.Open(SettingsPath, FileAccess.ModeFlags.Read);
                if (f == null) return false; // ошибка чтения — ретраебельно
                return ApplyParsedFlags(f.GetAsText());
            }
            catch (Exception e)
            {
                GD.Print($"[GameSettings] Load failed: {e.Message} — using defaults (retryable)");
                return false;
            }
        }

        /// <summary>
        /// ST17-004: применить флаги из JSON-текста настроек. Безопасный
        /// разбор (Godot Json): компактная запись/отступы/переносы/порядок/
        /// неизвестные поля — всё валидный JSON и всё разбирается. Ключи
        /// сопоставляются регистронезависимо (легаси-толерантность исходного
        /// OrdinalIgnoreCase-парсера). Не-bool значения пропускаются с
        /// диагностикой. Возвращает false, если текст не является JSON-объектом.
        /// </summary>
        private static bool ApplyParsedFlags(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                GD.Print("[GameSettings] Load: пустой файл — используя дефолты");
                return false;
            }

            var parsed = Json.ParseString(json);
            if (parsed.VariantType != Variant.Type.Dictionary)
            {
                GD.Print("[GameSettings] Load: файл не является JSON-объектом — используя дефолты (retryable)");
                return false;
            }

            var dict = parsed.AsGodotDictionary();
            ReadFlag(dict, "cheatsEnabled", v => CheatsEnabled = v);
            ReadFlag(dict, "showEnemyVitals", v => ShowEnemyVitals = v);
            ReadFlag(dict, "pauseOnModalWindows", v => PauseOnModalWindows = v);
            ReadFlag(dict, "showFpsCounter", v => ShowFpsCounter = v);
            return true;
        }

        /// <summary>
        /// Прочитать bool-флаг из словаря: регистронезависимый ключ (первое
        /// совпадение), пропуск не-bool значений с диагностикой, отсутствие
        /// ключа = сохранить текущее (дефолт) значение.
        /// </summary>
        private static void ReadFlag(Godot.Collections.Dictionary dict, string key, Action<bool> apply)
        {
            foreach (var k in dict.Keys)
            {
                if (k.VariantType != Variant.Type.String) continue;
                if (!string.Equals(k.AsString(), key, StringComparison.OrdinalIgnoreCase)) continue;

                var v = dict[k];
                if (v.VariantType == Variant.Type.Bool)
                {
                    apply(v.AsBool());
                }
                else
                {
                    GD.Print($"[GameSettings] Load: '{key}' не bool ({v.VariantType}) — пропущен, значение по умолчанию");
                }
                return;
            }
        }

        // ── QA-хуки Этапа-17 (ST17-004): тестовый контракт БЕЗ изменения
        // поведения — Stage17InitConfigStaminaTruthSimDebug управляет
        // статическим состоянием настроек между проверками.

        /// <summary>QA: сбросить флаг загрузки и вернуть канонические дефолты.</summary>
        internal static void ResetForQa()
        {
            _loaded = false;
            CheatsEnabled = true;
            ShowEnemyVitals = true;
            PauseOnModalWindows = true;
            ShowFpsCounter = false;
        }

        /// <summary>QA: применить флаги из произвольного JSON-текста.</summary>
        internal static bool LoadTextForQa(string json) => ApplyParsedFlags(json);

        /// <summary>Факт «настройки считаны» (QA: порядок EnsureLoaded/ретраи).</summary>
        internal static bool IsLoadedForQa => _loaded;

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
                f.StoreString($"{{\n  \"cheatsEnabled\": {(CheatsEnabled ? "true" : "false")},\n  \"showEnemyVitals\": {(ShowEnemyVitals ? "true" : "false")},\n  \"pauseOnModalWindows\": {(PauseOnModalWindows ? "true" : "false")},\n  \"showFpsCounter\": {(ShowFpsCounter ? "true" : "false")}\n}}\n");
            }
            catch (Exception e)
            {
                GD.Print($"[GameSettings] Save failed: {e.Message}");
            }
        }
    }
}
