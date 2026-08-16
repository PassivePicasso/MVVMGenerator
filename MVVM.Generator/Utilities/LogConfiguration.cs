using System.IO;

using Microsoft.CodeAnalysis.Diagnostics;

namespace MVVM.Generator.Utilities
{
    /// <summary>
    /// Resolves the opt-in log path from MSBuild properties.
    /// </summary>
    internal static class LogConfiguration
    {
        private const string LogPathProperty = "build_property.MVVMGeneratorLogPath";
        private const string ProjectDirectoryProperty = "build_property.MSBuildProjectDirectory";

        /// <summary>
        /// Returns an absolute log path, or null to leave logging disabled.
        /// A relative path with nothing to anchor it to yields null rather than
        /// resolving against the compiler's working directory.
        /// </summary>
        public static string? Resolve(AnalyzerConfigOptionsProvider provider)
        {
            var options = provider.GlobalOptions;

            if (!options.TryGetValue(LogPathProperty, out var logPath)) return null;
            if (string.IsNullOrWhiteSpace(logPath)) return null;

            if (Path.IsPathRooted(logPath)) return logPath;

            if (!options.TryGetValue(ProjectDirectoryProperty, out var projectDirectory)) return null;
            if (string.IsNullOrWhiteSpace(projectDirectory)) return null;

            return Path.GetFullPath(Path.Combine(projectDirectory, logPath));
        }
    }
}
