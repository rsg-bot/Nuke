using Nuke.Common;
using Nuke.Common.Tools.GitVersion;
using static Nuke.Common.IO.PathConstruction;
using Nuke.Common.Tooling;
using Newtonsoft.Json.Linq;
using Nuke.Common.Tools.MSBuild;

namespace Rocket.Surgery.Nuke
{
    /// <summary>
    /// Custom msbuild helper extensions
    /// </summary>
    public static class ToolSettingsExtensions
    {
        /// <summary>
        /// Configures binary logging for MSBuild
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="path"></param>
        /// <param name="imports"></param>
        public static T SetBinaryLogger<T>(this T settings, AbsolutePath path) where T : ToolSettings
        {
            var existingArgs = settings.ArgumentConfigurator;
            return settings.SetArgumentConfigurator(args =>
                existingArgs(args).Add($"/bl:{path};ProjectImports={(NukeBuild.IsLocalBuild ? MSBuildBinaryLogImports.None : MSBuildBinaryLogImports.Embed)}"));
        }

        /// <summary>
        /// Configures binary logging for MSBuild
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="path"></param>
        /// <param name="imports"></param>
        public static T SetBinaryLogger<T>(this T settings, AbsolutePath path, MSBuildBinaryLogImports imports) where T : ToolSettings
        {
            var existingArgs = settings.ArgumentConfigurator;
            return settings.SetArgumentConfigurator(args =>
                existingArgs(args).Add($"/bl:{path};ProjectImports={imports}"));
        }

        /// <summary>
        /// Configures a file logger for MSBuild
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="path"></param>
        public static T SetFileLogger<T>(this T settings, AbsolutePath path) where T : ToolSettings
        {
            var existingArgs = settings.ArgumentConfigurator;
            MSBuildVerbosity verbosity = MSBuildVerbosity.Normal;
            if (VerbosityMapping.Mappings.Contains(typeof(MSBuildVerbosity)))
            {
                foreach (var mapping in VerbosityMapping.Mappings[typeof(MSBuildVerbosity)])
                {
                    if (mapping.Verbosity == NukeBuild.Verbosity)
                    {
                        verbosity = (MSBuildVerbosity)mapping.MappedVerbosity;
                    }
                }
            }
            return settings.SetArgumentConfigurator(args =>
                existingArgs(args).Add($"/fileLogger /fileloggerparameters:ShowTimestamp;Verbosity={verbosity};LogFile=\"{path}\""));
        }

        /// <summary>
        /// Ensures all the gitversion values are available as environment values (GITVERISON_*)
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="gitVersion"></param>
        public static T SetGitVersionEnvironment<T>(this T settings, GitVersion gitVersion) where T : ToolSettings
        {
            foreach (var item in JObject.FromObject(gitVersion))
            {
                var key = $"gitversion_{item.Key}".ToUpperInvariant();
                if (settings.EnvironmentVariables.TryGetValue(key, out _)) continue;
                settings = settings.AddEnvironmentVariable(key, item.Value.ToString());
            }
            return settings;
        }
    }
}
