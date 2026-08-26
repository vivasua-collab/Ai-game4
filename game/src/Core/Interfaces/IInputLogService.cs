#nullable enable
// Создано: 2026-05-19 — Input Logging System: сервис логирования ввода
// Центральное хранилище логов ввода. Чистый C#, не зависит от Unity.
// Хранит ограниченный кольцевой буфер записей для отображения в UI.
using System.Collections.Generic;

using CultivationGame.Core;
using CultivationGame.Core.Data;
namespace CultivationGame.Core.Interfaces
{
    /// <summary>
    /// Запись в логе ввода.
    /// </summary>
    public struct InputLogEntry
    {
        /// <summary>Тип записи: Key (нажатие клавиши) или Action (результат)</summary>
        public InputLogEntryType Type;

        /// <summary>Название клавиши или действия</summary>
        public string Name;

        /// <summary>Описание (направление, результат)</summary>
        public string Description;

        /// <summary>Метка времени (frameCount)</summary>
        public int Frame;

        /// <summary>Секунды с начала игры</summary>
        public float Time;
    }

    /// <summary>
    /// Тип записи лога ввода.
    /// </summary>
    public enum InputLogEntryType
    {
        Key,
        Action
    }

    /// <summary>
    /// Интерфейс сервиса логирования ввода.
    /// Хранит кольцевой буфер записей о нажатиях клавиш и действиях.
    /// Подписывается на InputKeyEvent и InputActionEvent через MessagePipe.
    /// </summary>
    public interface IInputLogService
    {
        /// <summary>Все записи лога (кольцевой буфер, макс. Capacity)</summary>
        IReadOnlyList<InputLogEntry> Entries { get; }

        /// <summary>Текущее количество записей</summary>
        int Count { get; }

        /// <summary>Максимальная ёмкость буфера</summary>
        int Capacity { get; }

        /// <summary>Включено ли логирование</summary>
        bool IsEnabled { get; set; }

        /// <summary>Очистить все записи</summary>
        void Clear();

        /// <summary>Добавить запись о нажатии клавиши напрямую (без MessagePipe)</summary>
        void LogKey(string keyName, string description, int frame, float time);

        /// <summary>Добавить запись о действии напрямую (без MessagePipe)</summary>
        void LogAction(string actionName, string description, int frame, float time);
    }
}
