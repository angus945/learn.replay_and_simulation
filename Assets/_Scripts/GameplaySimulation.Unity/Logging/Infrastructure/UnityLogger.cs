using Logging.Contract;
using UnityEngine;
using GameplayLogger = Logging.API.ILogger;

namespace Logging.Unity.Infrastructure
{
    [System.Serializable]
    public sealed class UnityLogger : GameplayLogger
    {
        [SerializeField] LogLevel minimumLevel;
        private readonly Object context;

        public UnityLogger(LogLevel minimumLevel = LogLevel.Info, Object context = null)
        {
            this.minimumLevel = minimumLevel;
            this.context = context;
        }

        public bool IsEnabled(LogLevel level)
        {
            return level >= minimumLevel && level < LogLevel.None;
        }

        public void Log(in LogEntry entry)
        {
            if (!IsEnabled(entry.Level))
                return;

            string message = FormatMessage(in entry);

            switch (entry.Level)
            {
                case LogLevel.Warning:
                    Debug.LogWarning(message, context);
                    break;
                case LogLevel.Error:
                case LogLevel.Critical:
                    Debug.LogError(message, context);
                    break;
                default:
                    Debug.Log(message, context);
                    break;
            }
        }

        private static string FormatMessage(in LogEntry entry)
        {
            string prefix = string.IsNullOrEmpty(entry.Category)
                ? $"[{entry.Level}]"
                : $"[{entry.Level}][{entry.Category}]";

            if (entry.Exception == null)
                return $"{prefix} {entry.Message}";

            return $"{prefix} {entry.Message}\n{entry.Exception}";
        }
    }
}
