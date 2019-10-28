using Nuke.Common;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitVersion;
using static Nuke.Common.IO.PathConstruction;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using static Nuke.Common.IO.FileSystemTasks;
using Nuke.Common.Tools.MSBuild;
using Nuke.Common.Tools.VSTest;
using Nuke.Common.Tools.VSWhere;
using Nuke.Common.IO;
using Nuke.Common.Utilities.Collections;
using System.IO;
using System.Linq;

namespace Rocket.Surgery.Nuke.DotNetCore
{
    /// <summary>
    /// Base build plan for .NET Core based applications
    /// </summary>
    public abstract class DotNetCoreBuild : RocketBoosterBuild
    {
        /// <summary>
        /// Core target that can be used to trigger all targets for this build
        /// </summary>
        public Target DotNetCore => _ => _;

        /// <summary>
        /// This will ensure that all local dotnet tools are installed
        /// </summary>
        public Target DotnetToolRestore => _ => _
           .After(Clean)
           .Before(Build)
#if NETSTANDARD2_1
           .DependentFor(DotNetCore)
#endif
           .Unlisted()
           .Executes(() => DotNet("tool restore"));

        /// <summary>
        /// dotnet restore
        /// </summary>
        public Target Restore => _ => _
            .DependentFor(DotNetCore)
#if NETSTANDARD2_1
            .DependsOn(DotnetToolRestore)
#endif
            .DependsOn(Clean)
            .Executes(() =>
            {
                DotNetRestore(s => s
                    .SetProjectFile(Solution)
                    .SetDisableParallel(true)
                    .SetBinaryLogger(LogsDirectory / "restore.binlog")
                    .SetFileLogger(LogsDirectory / "restore.log")
                    .SetGitVersionEnvironment(GitVersion)
                );
            });

        /// <summary>
        /// dotnet build
        /// </summary>
        public Target Build => _ => _
            .DependsOn(Restore)
            .DependentFor(DotNetCore)
            .Executes(() =>
            {
                DotNetBuild(s => s
                    .SetProjectFile(Solution)
                    .SetBinaryLogger(LogsDirectory / "build.binlog")
                    .SetFileLogger(LogsDirectory / "build.log")
                    .SetGitVersionEnvironment(GitVersion)
                    .SetConfiguration(Configuration)
                    .EnableNoRestore());
            });

        /// <summary>
        /// dotnet test
        /// </summary>
        public Target Test => _ => _
            .After(Build)
            .DependentFor(DotNetCore)
            .DependentFor(Pack)
            .DependentFor(Generate_Code_Coverage_Reports)
            .Triggers(Generate_Code_Coverage_Reports)
            .OnlyWhenDynamic(() => TestDirectory.GlobFiles("**/*.csproj").Count > 0)
            .WhenSkipped(DependencyBehavior.Execute)
            .Executes(async () =>
           {
               DotNetTest(s => s
                   .SetProjectFile(Solution)
                   .SetBinaryLogger(LogsDirectory / "test.binlog")
                   .SetFileLogger(LogsDirectory / "test.log")
                   .SetGitVersionEnvironment(GitVersion)
                   .SetConfiguration("Debug")
                   .EnableNoRestore()
                   .SetLogger($"trx")
                   .SetProperty("CollectCoverage", "true")
                   // DeterministicSourcePaths being true breaks coverlet!
                   .SetProperty("DeterministicSourcePaths", "false")
                   .SetProperty("CoverageDirectory", CoverageDirectory)
                   .SetResultsDirectory(TestResultsDirectory)
               );

               foreach (var coverage in TestResultsDirectory.GlobFiles("**/*.cobertura.xml"))
               {
                   CopyFileToDirectory(coverage, CoverageDirectory, FileExistsPolicy.OverwriteIfNewer);
               }
           });

        /// <summary>
        /// dotnet pack
        /// </summary>
        public Target Pack => _ => _
            .DependsOn(Build)
            .DependentFor(DotNetCore)
            .Executes(() =>
            {
                DotNetPack(s => s
                    .SetProject(Solution)
                    .SetBinaryLogger(LogsDirectory / "pack.binlog")
                    .SetFileLogger(LogsDirectory / "pack.log")
                    .SetGitVersionEnvironment(GitVersion)
                    .SetConfiguration(Configuration)
                    .EnableNoRestore()
                    .EnableNoBuild()
                    .SetOutputDirectory(NuGetPackageDirectory));
            });
    }
}
