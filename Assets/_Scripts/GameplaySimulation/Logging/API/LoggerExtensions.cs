using System;
using Logging.Contract;

namespace Logging.API
{
    public static class LoggerExtensions_Logger
    {
        public static void Trace(this ILogger logger, string message, string category = null)
        {
            logger.Log(LogLevel.Trace, message, category);
        }

        public static void Debug(this ILogger logger, string message, string category = null)
        {
            logger.Log(LogLevel.Debug, message, category);
        }

        public static void Info(this ILogger logger, string message, string category = null)
        {
            logger.Log(LogLevel.Info, message, category);
        }

        public static void Warning(this ILogger logger, string message, string category = null)
        {
            logger.Log(LogLevel.Warning, message, category);
        }

        public static void Error(this ILogger logger, string message, Exception exception = null, string category = null)
        {
            logger.Log(LogLevel.Error, message, category, exception);
        }

        public static void Critical(this ILogger logger, string message, Exception exception = null, string category = null)
        {
            logger.Log(LogLevel.Critical, message, category, exception);
        }

        public static void Log(this ILogger logger, LogLevel level, string message, string category = null, Exception exception = null)
        {
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));

            if (!logger.IsEnabled(level))
                return;

            LogEntry entry = new LogEntry(level, category, message, exception);
            logger.Log(in entry);
        }
    }
}
