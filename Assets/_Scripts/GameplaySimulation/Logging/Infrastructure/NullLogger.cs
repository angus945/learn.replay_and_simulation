using Logging.API;
using Logging.Contract;

namespace Logging.Infrastructure
{
    public sealed class NullLogger : ILogger
    {
        public static readonly NullLogger Instance = new NullLogger();

        private NullLogger()
        {
        }

        public bool IsEnabled(LogLevel level)
        {
            return false;
        }

        public void Log(in LogEntry entry)
        {
        }
    }
}
