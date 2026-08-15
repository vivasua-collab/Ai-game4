#nullable enable
// Создано: 2026-05-09 — Phase 13: Typewriter-эффект для диалогов
// Переписан с нуля — корректная остановка таймера.
// Legacy DialogueSystem.typewriter_timer НЕ останавливался.
using System;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Interaction
{
    /// <summary>
    /// Typewriter-эффект для текста диалогов.
    /// Постепенно показывает текст символ за символом.
    ///
    /// ВАЖНО: В отличие от legacy, таймер КОРЕКТНО останавливается:
    /// - Stop() — полная остановка + сброс
    /// - CompleteImmediately() — досрочное отображение всего текста
    /// - IsComplete — признак завершения
    ///
    /// Tick() вызывается извне (из InteractionModule.ITickable.Tick).
    /// </summary>
    public class DialogueTypewriter
    {
        // === Состояние ===
        private string _fullText = "";
        private int _charsRevealed;
        private float _elapsed;
        private bool _isActive;
        private bool _isComplete;

        /// <summary>Скорость отображения (символов в секунду)</summary>
        public float Speed = 30f;

        /// <summary>Текущий отображаемый текст</summary>
        public string DisplayText
        {
            get
            {
                if (!_isActive && !_isComplete) return "";
                if (_isComplete) return _fullText;
                int count = Math.Min(_charsRevealed, _fullText.Length);
                return _fullText.Substring(0, count);
            }
        }

        /// <summary>Завершён ли эффект</summary>
        public bool IsComplete => _isComplete;

        /// <summary>Активен ли эффект</summary>
        public bool IsActive => _isActive;

        /// <summary>
        /// Начать typewriter-эффект для текста.
        /// </summary>
        public void StartText(string text)
        {
            _fullText = text ?? "";
            _charsRevealed = 0;
            _elapsed = 0f;
            _isActive = true;
            _isComplete = false;

            // Пустой текст — сразу завершён
            if (string.IsNullOrEmpty(_fullText))
            {
                _isComplete = true;
                _isActive = false;
            }
        }

        /// <summary>
        /// Обработка тика (вызывается из InteractionModule.Tick()).
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (!_isActive || _isComplete) return;

            _elapsed += deltaTime;

            // Рассчитать, сколько символов должно быть отображено
            int targetChars = (int)(_elapsed * Speed);
            _charsRevealed = Math.Min(targetChars, _fullText.Length);

            // Проверка завершения
            if (_charsRevealed >= _fullText.Length)
            {
                _charsRevealed = _fullText.Length;
                _isComplete = true;
                _isActive = false;
            }
        }

        /// <summary>
        /// Досрочно показать весь текст.
        /// Эффект завершается, но текст остаётся видимым.
        /// </summary>
        public void CompleteImmediately()
        {
            _charsRevealed = _fullText.Length;
            _isComplete = true;
            _isActive = false;
        }

        /// <summary>
        /// Полная остановка + сброс.
        /// Текст очищается.
        /// </summary>
        public void Stop()
        {
            _fullText = "";
            _charsRevealed = 0;
            _elapsed = 0f;
            _isActive = false;
            _isComplete = false;
        }
    }
}
