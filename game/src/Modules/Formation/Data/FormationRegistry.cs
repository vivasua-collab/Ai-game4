#nullable enable
// Этап 4 внедрения ЦИ (2026-08-23): FormationRegistry — реестр определений
// формаций (аналог TechniqueRegistry). FormationService.FindFormationData
// смотрит сюда ПЕРЕД legacy-хардкодом. Генератор регистрирует свои формации.
using System.Collections.Generic;
using CultivationGame.Core.Data; // FormationData (аудит-1 A-2: перенесён в Core.Data)

namespace CultivationGame.Modules.Formation
{
    /// <summary>
    /// Реестр формаций: id → FormationData. Потокобезопасность не требуется
    /// (однопоточная sim). Генератор формаций регистрирует результаты,
    /// сервис/визуализатор читают.
    /// </summary>
    public class FormationRegistry
    {
        private readonly Dictionary<string, FormationData> _formations = new();

        public void Register(FormationData data)
        {
            if (data == null || string.IsNullOrEmpty(data.Id)) return;
            _formations[data.Id] = data; // перерегистрация = замена (id детерминирован)
        }

        public FormationData? Get(string id)
        {
            return _formations.TryGetValue(id, out var data) ? data : null;
        }

        public IReadOnlyCollection<FormationData> GetAll()
        {
            return _formations.Values;
        }

        public int Count => _formations.Count;
    }
}
