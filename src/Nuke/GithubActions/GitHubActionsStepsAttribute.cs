﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Humanizer;
using JetBrains.Annotations;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.CI.GitHubActions.Configuration;
using Nuke.Common.Execution;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Utilities;
using Nuke.Common.Utilities.Collections;

namespace Rocket.Surgery.Nuke.GithubActions
{

    [PublicAPI]
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class GitHubActionsStepsAttribute : ChainedConfigurationAttributeBase
    {
        private readonly string _name;
        private readonly GitHubActionsImage[] _images;

        public GitHubActionsStepsAttribute(
            string name,
            GitHubActionsImage image,
            params GitHubActionsImage[] images)
        {
            _name = name;
            _images = new[] { image }.Concat(images).ToArray();
        }

        private string ConfigurationFile => NukeBuild.RootDirectory / ".github" / "workflows" / $"{_name}.yml";

        public string[] InvokedTargets { get; set; } = new string[0];
        public string[] Parameters { get; set; } = new string[0];

        public override string IdPostfix => _name;
        public override HostType HostType => HostType.GitHubActions;
        public override IEnumerable<string> GeneratedFiles => new[] { ConfigurationFile };
        public override IEnumerable<string> RelevantTargetNames => InvokedTargets;
        // public override IEnumerable<string> IrrelevantTargetNames => new string[0];

        public GitHubActionsTrigger[] On { get; set; } = new GitHubActionsTrigger[0];
        public string[] OnPushBranches { get; set; } = new string[0];
        public string[] OnPushTags { get; set; } = new string[0];
        public string[] OnPushIncludePaths { get; set; } = new string[0];
        public string[] OnPushExcludePaths { get; set; } = new string[0];
        public string[] OnPullRequestBranches { get; set; } = new string[0];
        public string[] OnPullRequestTags { get; set; } = new string[0];
        public string[] OnPullRequestIncludePaths { get; set; } = new string[0];
        public string[] OnPullRequestExcludePaths { get; set; } = new string[0];
        public string OnCronSchedule { get; set; }

        public string[] ImportSecrets { get; set; } = new string[0];
        public string ImportGitHubTokenAs { get; set; }

        public string[] Enhancements { get; set; }

        public override CustomFileWriter CreateWriter()
        {
            return new CustomFileWriter(ConfigurationFile, indentationFactor: 2, commentPrefix: "#");
        }

        public override ConfigurationEntity GetConfiguration(
            NukeBuild build,
            IReadOnlyCollection<ExecutableTarget> relevantTargets
        )
        {


            var buildcmd = Path.ChangeExtension(NukeBuild.RootDirectory.GlobFiles("build.ps1", "build.sh")
                        .Select(x => NukeBuild.RootDirectory.GetUnixRelativePathTo(x))
                        .FirstOrDefault()
                        .NotNull("Must have a build script of build.ps1 or build.sh"), ".ps1");
            var steps = new List<GitHubActionsStep> {
                            new CheckoutStep("Checkout"),
                            // new SetupDotNetStep("Install .NET Core Sdk"),
                            new RunStep("Install Nuke Global Tool") {
                                Run = "dotnet tool install Nuke.GlobalTool"
                            }
                        };

            var stepParameters = GetParameters(build).Select(z => $"--{z.Name.ToLowerInvariant()} '${{{{ env.{z.Name.ToUpperInvariant()} }}}}'")
               .ToArray()
               .JoinSpace();

            var lookupTable = new LookupTable<ExecutableTarget, string[]>();
            foreach (var (execute, targets) in relevantTargets
                .Select(x => (ExecutableTarget: x, Targets: GetInvokedTargets(x).ToArray()))
                .ForEachLazy(x => lookupTable.Add(x.ExecutableTarget, x.Targets))
            )
            {
                steps.Add(new RunStep(execute.Name.Humanize(LetterCasing.Title))
                {
                    Run = $"nuke {targets.JoinSpace()} --skip {stepParameters}".TrimEnd()
                });
            }

            var config = new RocketSurgeonGitHubActionsConfiguration()
            {
                Name = _name,
                DetailedTriggers = GetTriggers().ToList(),
                Jobs = new List<RocketSurgeonsGithubActionsJob> {
                    new RocketSurgeonsGithubActionsJob("Build")
                    {
                        Steps = steps,
                        Images = _images,
                    }
                }
            };

            if (Enhancements?.Any() == true)
            {
                foreach (var method in Enhancements.Join(build.GetType().GetMethods(), z => z, z => z.Name, (_, e) => e))
                {
                    config = method.IsStatic
                        ? method.Invoke(null, new object[] { config }) as RocketSurgeonGitHubActionsConfiguration ?? config
                        : method.Invoke(build, new object[] { config }) as RocketSurgeonGitHubActionsConfiguration ?? config;
                }
            }

            return config;
        }

        protected virtual IEnumerable<GithubActionsNukeParameter> GetParameters(NukeBuild build)
        {
            var parameters =
                build.GetType().GetMembers(
                        BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public |
                        BindingFlags.FlattenHierarchy
                    )
                   .Where(x => x.GetCustomAttribute<ParameterAttribute>() != null);
            foreach (var parameter in parameters)
            {
                if (Parameters.Any(
                    z => z.Equals(parameter.Name, StringComparison.OrdinalIgnoreCase) || z.Equals(
                        parameter.GetCustomAttribute<ParameterAttribute>().Name,
                        StringComparison.OrdinalIgnoreCase
                    )
                ))
                {
                    var value = parameter.GetValue(build);
                    if (value is AbsolutePath)
                    {
                        value = null;
                    }

                    yield return new GithubActionsNukeParameter()
                    {
                        Name = parameter.GetCustomAttribute<ParameterAttribute>().Name ?? parameter.Name,
                        Default = value?.ToString() ?? "",
                    };
                }
            }
        }

        protected virtual IEnumerable<(string key, string value)> GetImports()
        {
            string GetSecretValue(string secret) => $"${{{{ secrets.{secret} }}}}";

            if (ImportGitHubTokenAs != null)
                yield return (ImportGitHubTokenAs, GetSecretValue("GITHUB_TOKEN"));

            foreach (var secret in ImportSecrets)
                yield return (secret, GetSecretValue(secret));
        }

        protected virtual IEnumerable<GitHubActionsDetailedTrigger> GetTriggers()
        {
            if (OnPushBranches.Length > 0 ||
                OnPushTags.Length > 0 ||
                OnPushIncludePaths.Length > 0 ||
                OnPushExcludePaths.Length > 0)
            {
                yield return new RocketSurgeonGitHubActionsVcsTrigger
                {
                    Kind = GitHubActionsTrigger.Push,
                    Branches = OnPushBranches,
                    Tags = OnPushTags,
                    IncludePaths = OnPushIncludePaths,
                    ExcludePaths = OnPushExcludePaths
                };
            }

            if (OnPullRequestBranches.Length > 0 ||
                OnPullRequestTags.Length > 0 ||
                OnPullRequestIncludePaths.Length > 0 ||
                OnPullRequestExcludePaths.Length > 0)
            {
                yield return new RocketSurgeonGitHubActionsVcsTrigger
                {
                    Kind = GitHubActionsTrigger.PullRequest,
                    Branches = OnPullRequestBranches,
                    Tags = OnPullRequestTags,
                    IncludePaths = OnPullRequestIncludePaths,
                    ExcludePaths = OnPullRequestExcludePaths
                };
            }

            if (OnCronSchedule != null)
                yield return new GitHubActionsScheduledTrigger { Cron = OnCronSchedule };
        }
    }
}