using System;
using System.Diagnostics;
using System.IO;

namespace MVVM.Generator.Utilities
{
    /// <summary>
    /// Opt-in trace logging. Disabled unless the consuming project sets an
    /// absolute log path, because a compiler process serves many projects and
    /// its working directory is not the project directory.
    /// </summary>
    public static class LogManager
    {
        private static readonly object Lock = new();
        private static string? _configuredPath;

        /// <summary>
        /// Guard call sites with this. Message interpolation happens at the
        /// call site and would otherwise run even with logging off.
        /// </summary>
        public static bool IsEnabled { get; private set; }

        private static TraceSource? Logger { get; set; }

        /// <summary>
        /// Applies the resolved log path. A null or unusable path leaves
        /// logging off rather than falling back to a relative file.
        /// </summary>
        public static void Configure(string? logFilePath)
        {
            lock (Lock)
            {
                if (string.Equals(_configuredPath, logFilePath, StringComparison.Ordinal)) return;

                _configuredPath = logFilePath;
                Reset();

                if (string.IsNullOrWhiteSpace(logFilePath)) return;
                if (!Path.IsPathRooted(logFilePath)) return;

                TryAttachListener(logFilePath!);
            }
        }

        public static void Log(string message, TraceEventType eventType = TraceEventType.Information)
        {
            if (Logger == null) return;

            Logger.TraceEvent(eventType, 0, message);

            // Flushed per message so a crashed build still leaves a usable log.
            // The previous implementation achieved this by setting the global
            // Trace.AutoFlush, which affected unrelated tracing in the host.
            Logger.Flush();
        }

        public static void LogError(string message, Exception? ex = null)
        {
            Log(ex == null ? message : $"{message} Exception: {ex}", TraceEventType.Error);
        }

        private static void Reset()
        {
            if (Logger != null)
            {
                foreach (TraceListener listener in Logger.Listeners)
                {
                    listener.Flush();
                    listener.Dispose();
                }
                Logger.Listeners.Clear();
            }

            Logger = null;
            IsEnabled = false;
        }

        private static void TryAttachListener(string logFilePath)
        {
            try
            {
                // The directory must already exist; creating it would mean an
                // analyzer doing file IO, which RS1035 bans.
                var source = new TraceSource("MVVMGenerator", SourceLevels.All);
                source.Listeners.Clear();

                var listener = new TextWriterTraceListener(logFilePath)
                {
                    TraceOutputOptions = TraceOptions.DateTime | TraceOptions.ThreadId,
                };
                source.Listeners.Add(listener);

                Logger = source;
                IsEnabled = true;
            }
            catch (Exception)
            {
                // A generator must not fail a build because logging could not start.
                Reset();
            }
        }
    }
}
