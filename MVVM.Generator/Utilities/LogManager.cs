// LogManager.cs
using System;
using System.Diagnostics;

namespace MVVM.Generator.Utilities
{
    public static class LogManager
    {
        private static readonly object _lock = new();
        private static bool _initialized;
        private static readonly string LogFile = "mvvm-generator.log";

        public static TraceSource Logger { get; } = new("MVVMGenerator", SourceLevels.All);

        public static void Initialize()
        {
            lock (_lock)
            {
                if (_initialized) return;

                Trace.AutoFlush = true;
                Logger.Listeners.Clear();
                Logger.Listeners.Add(new DefaultTraceListener());
                Logger.Listeners.Add(new TextWriterTraceListener(LogFile));

                foreach (TraceListener listener in Logger.Listeners)
                {
                    listener.TraceOutputOptions = TraceOptions.DateTime | TraceOptions.ThreadId;
                }

                _initialized = true;
            }
        }

        public static void Log(string message, TraceEventType eventType = TraceEventType.Information)
        {
            if (!_initialized) Initialize();
            Logger.TraceEvent(eventType, 0, message);
        }

        public static void LogError(string message, Exception? ex = null)
        {
            var fullMessage = ex == null ? message : $"{message} Exception: {ex}";
            Log(fullMessage, TraceEventType.Error);
        }
    }
}