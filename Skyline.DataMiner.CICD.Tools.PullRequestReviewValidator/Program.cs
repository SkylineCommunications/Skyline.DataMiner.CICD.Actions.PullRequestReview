namespace Skyline.DataMiner.CICD.Tools.PullRequestReviewValidator
{
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;

    using Microsoft.Extensions.Logging;

    using Serilog;

    /// <summary>
    /// Validates that a pull request has the required CR, QA and (conditional) Docs approvals.
    /// </summary>
    public static class Program
    {
        /*
         * Design guidelines for command line tools: https://learn.microsoft.com/en-us/dotnet/standard/commandline/syntax#design-guidance
         */

        /// <summary>
        /// Code that will be called when running the tool.
        /// </summary>
        /// <param name="args">Extra arguments.</param>
        /// <returns>0 if all required gates passed, 1 otherwise (or if an unexpected error occurred).</returns>
        public static async Task<int> Main(string[] args)
        {
            var isDebug = new Option<bool>(
            name: "--debug",
            description: "Indicates the tool should write out debug logging.")
            {
                IsRequired = false,
            };

            isDebug.SetDefaultValue(false);

            var githubToken = new Option<string>(
                name: "--github-token",
                description: "A PAT (or GitHub App token) with 'read:org' access. The default secrets.GITHUB_TOKEN does NOT have enough permissions to check org/team membership.")
            {
                IsRequired = true
            };

            var githubRepository = new Option<string>(
            name: "--github-repository",
            description: "The github.repository (owner/repo).")
            {
                IsRequired = true
            };

            var pullRequestNumber = new Option<int>(
            name: "--pr-number",
            description: "The pull request number to validate.")
            {
                IsRequired = true
            };

            var docsTeam = new Option<string>(
            name: "--docs-team",
            description: "(optional) The slug of the GitHub team whose members may approve documentation changes.")
            {
                IsRequired = false
            };

            docsTeam.SetDefaultValue(Constants.DefaultDocsTeamSlug);

            var docsPathPattern = new Option<string>(
            name: "--docs-path-pattern",
            description: "(optional) A path segment that, when present in a changed file's path, marks the pull request as requiring a docs approval.")
            {
                IsRequired = false
            };

            docsPathPattern.SetDefaultValue(Constants.DefaultDocsPathPattern);

            var rootCommand = new RootCommand("Validates that a pull request has the required CR, QA and (conditional) Docs approvals.")
            {
                isDebug,
                githubToken,
                githubRepository,
                pullRequestNumber,
                docsTeam,
                docsPathPattern
            };

            rootCommand.SetHandler(Process, isDebug, githubToken, githubRepository, pullRequestNumber, docsTeam, docsPathPattern);

            return await rootCommand.InvokeAsync(args);
        }

        private static async Task<int> Process(bool isDebug, string githubToken, string githubRepository, int pullRequestNumber, string docsTeam, string docsPathPattern)
        {
            try
            {
                // Set up logging
                var logConfig = new LoggerConfiguration().WriteTo.Console();
                logConfig.MinimumLevel.Is(isDebug ? Serilog.Events.LogEventLevel.Debug : Serilog.Events.LogEventLevel.Information);
                var seriLog = logConfig.CreateLogger();

                using var loggerFactory = LoggerFactory.Create(builder => builder.AddSerilog(seriLog));
                var logger = loggerFactory.CreateLogger("Skyline.DataMiner.CICD.Tools.PullRequestReviewValidator");

                try
                {
                    var organization = githubRepository.Split('/')[0];

                    using var httpClient = new HttpClient();
                    IGitHubService gitHubService = new GitHubService(httpClient, logger, githubToken, githubRepository);

                    var author = await gitHubService.GetPullRequestAuthorAsync(pullRequestNumber);
                    var reviews = await gitHubService.GetReviewsAsync(pullRequestNumber);
                    var changedFiles = await gitHubService.GetChangedFilesAsync(pullRequestNumber);
                    var docsTeamMembers = await gitHubService.GetTeamMembersAsync(organization, docsTeam);

                    // Only need to resolve org membership for people who actually submitted a review.
                    var reviewers = reviews.Select(r => r.Username).Where(u => !String.IsNullOrEmpty(u)).Distinct(StringComparer.OrdinalIgnoreCase);
                    var organizationMembers = new List<string>();
                    foreach (var reviewer in reviewers)
                    {
                        if (await gitHubService.IsOrganizationMemberAsync(organization, reviewer))
                        {
                            organizationMembers.Add(reviewer);
                        }
                    }

                    var evaluator = new PrReviewEvaluator();
                    var result = evaluator.Evaluate(author, reviews, organizationMembers, docsTeamMembers, changedFiles, docsPathPattern);

                    var outputWriter = new OutputWriter(logger);
                    outputWriter.Write(result);

                    return result.OverallPassed ? 0 : 1;
                }
                catch (Exception e)
                {
                    logger.LogError("Exception during Process Run: {Exception}", e);
                    return 1;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Exception on Logger Creation: {e}");
                return 1;
            }
        }
    }
}
