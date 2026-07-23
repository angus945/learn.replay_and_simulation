using Logging.Contract;

namespace Logging.API
{
    public interface ILogger
    {
        bool IsEnabled(LogLevel level);
        void Log(in LogEntry entry);
    }
}
