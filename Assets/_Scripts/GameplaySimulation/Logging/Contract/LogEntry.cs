using System;

namespace Logging.Contract
{
    public readonly struct LogEntry
    {
        public readonly LogLevel Level;
        public readonly string Category;
        public readonly string Message;
        public readonly Exception Exception;

        public LogEntry(
            LogLevel level,
            string category,
            string message,
            Exception exception = null)
        {
            Level = level;
            Category = category ?? string.Empty;
            Message = message ?? string.Empty;
            Exception = exception;
        }
    }
}
