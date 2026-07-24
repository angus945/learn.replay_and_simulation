using SimulationCore.Logging.Contract;

namespace SimulationCore.Logging.API
{
    public interface ILogger
    {
        bool IsEnabled(LogLevel level);
        void Log(in LogEntry entry);
    }
}
