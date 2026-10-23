#nullable enable
// Этап 4 внедрения ЦИ (2026-08-23): FormationRegistry — реестр определений
// формаций (аналог TechniqueRegistry). FormationService.FindFormationData
// смотрит сюда ПЕРЕД legacy-хардкодом. Генератор регистрирует свои формации.
//
// Этап-15 аудита (23.10, ST15-003/ST15-008): реестр получил полный
// жизненный цикл — Remove (реальная очистка дублей Dedup.Clean), Clear и
// контракт IWorldResettable: WorldDomainResetCoordinator (ResolveAll по
// фактическому типу инстанса) теперь очищает реестр при пересборке мира —
// формации прошлого мира больше не переживают world-transition.
// Safe-register: коллизия Id с ДРУГИМ контентом не затирает существующую
// запись (детерминированная защита от подмены; сигнатура локальная —
// Formation→Generator-импорт создал бы цикл модулей).
using System.Collections.Generic;
using System.Globalization;
using CultivationGame.Core.Data; // FormationData (аудит-1 A-2: перенесён в Core.Data)
using CultivationGame.Core.Interfaces; // IWorldResettable (Этап-15)

namespace CultivationGame.Modules.Formation
{
    /// <summary>
    /// Реестр формаций: id → FormationData. Потокобезопасность не требуется
    /// (однопоточная sim). Генератор формаций регистрирует результаты,
    /// сервис/визуализатор читают.
    /// </summary>
    public class FormationRegistry : IWorldResettable
    {
        private readonly Dictionary<string, FormationData> _formations = new();

        /// <summary>
        /// Зарегистрировать формацию. Повторная регистрация ТОГО ЖЕ
        /// определения (одинаковая сигнатура контента) — идемпотентная
        /// замена (контракт детерминизма: тот же seed → тот же объект).
        /// Коллизия Id с ДРУГИМ контентом — НЕ подменяет существующую
        /// запись: первая остаётся, новая отвергается с диагностикой
        /// (ST15-005: тихая потеря данных запрещена).
        /// </summary>
        public bool Register(FormationData data)
        {
            if (data == null || string.IsNullOrEmpty(data.Id)) return false;
            if (_formations.TryGetValue(data.Id, out var existing)
                && Signature(existing) != Signature(data))
            {
                System.Console.WriteLine($"[FormationRegistry] Register: id='{data.Id}' уже занят формацией с ДРУГИМ контентом — " +
                                        "новая запись отвергнута (защита от подмены, ST15-005)");
                return false;
            }
            _formations[data.Id] = data; // перерегистрация = замена (id детерминирован)
            return true;
        }

        public FormationData? Get(string id)
        {
            return _formations.TryGetValue(id, out var data) ? data : null;
        }

        public IReadOnlyCollection<FormationData> GetAll()
        {
            return _formations.Values;
        }

        /// <summary>Количество зарегистрированных формаций.</summary>
        public int Count => _formations.Count;

        /// <summary>
        /// Этап-15 (ST15-003): удалить формацию по id — реальная очистка
        /// дублей DeduplicationService.Clean(FormationRegistry).
        /// </summary>
        public bool Remove(string id)
        {
            return !string.IsNullOrEmpty(id) && _formations.Remove(id);
        }

        /// <summary>Этап-15 (ST15-008): очистить реестр (пересборка мира).</summary>
        public void Clear()
        {
            _formations.Clear();
        }

        /// <summary>
        /// Этап-15 (ST15-008): IWorldResettable — реестр world-scoped.
        /// WorldDomainResetCoordinator (NewGame phase 0 / LoadGame
        /// pre-Restore) очищает его наравне с прочими доменами: формации
        /// генерируются заново для каждого мира.
        /// </summary>
        public void ResetWorld()
        {
            Clear();
            System.Console.WriteLine("[FormationRegistry] ResetWorld: реестр формаций очищен (world-scoped, Этап-15 ST15-008)");
        }

        /// <summary>
        /// Сигнатура контента формации (механическая идентичность для
        /// safe-register). Локальная копия без импорта DeduplicationService:
        /// Formation→Generator создал бы цикл модулей (Generator уже
        /// зависит от Formation). Культура-независима (invariant), полная
        /// точность float (R). Синхронизирована с Fingerprint(FormationData)
        /// по составу полей (ST15-004).
        /// </summary>
        private static string Signature(FormationData f)
        {
            var sb = new System.Text.StringBuilder(128);
            sb.Append($"T:{f.FormationType}|S:{f.Size}|Sh:{f.Shape}|E:{f.Element}|L:{f.RequiredLevel}|");
            sb.Append($"R:{f.EffectRadiusMeters}|Eff:{f.Effects.Count}");
            foreach (var e in f.Effects)
                sb.Append($"|{e.EffectType}:{e.TargetStat}:{e.Value.ToString("R", CultureInfo.InvariantCulture)}:{e.ControlType}:{e.TargetTag}");
            return sb.ToString();
        }
    }
}
