namespace Skyline.DataMiner.CICD.Tools.PullRequestReviewValidator
{
    using System;
    using System.IO;
    using System.Text;

    using Microsoft.Extensions.Logging;

    using Skyline.DataMiner.CICD.Tools.PullRequestReviewValidator.Models;

    /// <summary>
    /// Writes the outcome of a <see cref="PrReviewResult"/> to the console, and, when running inside a
    /// GitHub Actions workflow, to <c>GITHUB_OUTPUT</c> (so the job's outputs can be consumed by later
    /// steps) and <c>GITHUB_STEP_SUMMARY</c> (for a human-readable job summary).
    /// </summary>
    internal class OutputWriter
    {
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="OutputWriter"/> class.
        /// </summary>
        /// <param name="logger">The logger instance for logging messages.</param>
        public OutputWriter(ILogger logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Writes the given result to the console, and (when available) to the <c>GITHUB_OUTPUT</c> and
        /// <c>GITHUB_STEP_SUMMARY</c> files.
        /// </summary>
        /// <param name="result">The result to write.</param>
        public void Write(PrReviewResult result)
        {
            WriteConsoleSummary(result);
            WriteGitHubOutput(result);
            WriteGitHubStepSummary(result);
        }

        private void WriteConsoleSummary(PrReviewResult result)
        {
            _logger.LogInformation("PR Review Validation Result:");
            LogGate(result.Cr);
            LogGate(result.Qa);
            LogGate(result.Docs);
            _logger.LogInformation("Overall: {Status}", result.OverallPassed ? "PASSED" : "FAILED");
        }

        private void LogGate(ReviewGateResult gate)
        {
            var status = !gate.IsRequired ? "SKIPPED (not required)" : gate.Passed ? "PASSED" : "FAILED";
            _logger.LogInformation("- {Name}: {Status} - {Reason}", gate.Name, status, gate.Reason);
        }

        private void WriteGitHubOutput(PrReviewResult result)
        {
            var path = Environment.GetEnvironmentVariable("GITHUB_OUTPUT");
            if (String.IsNullOrEmpty(path))
            {
                return;
            }

            var lines = new[]
            {
                $"cr-passed={ToBool(result.Cr.Passed)}",
                $"qa-passed={ToBool(result.Qa.Passed)}",
                $"docs-required={ToBool(result.Docs.IsRequired)}",
                $"docs-approved={ToBool(result.Docs.Passed)}",
                $"overall-passed={ToBool(result.OverallPassed)}",
            };

            File.AppendAllLines(path, lines);
        }

        private void WriteGitHubStepSummary(PrReviewResult result)
        {
            var path = Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY");
            if (String.IsNullOrEmpty(path))
            {
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("## PR Review Validation");
            sb.AppendLine();
            sb.AppendLine("| Gate | Required | Result | Details |");
            sb.AppendLine("|------|----------|--------|---------|");
            AppendRow(sb, result.Cr);
            AppendRow(sb, result.Qa);
            AppendRow(sb, result.Docs);
            sb.AppendLine();
            sb.AppendLine($"**Overall: {(result.OverallPassed ? "✅ PASSED" : "❌ FAILED")}**");

            File.AppendAllText(path, sb.ToString());
        }

        private static void AppendRow(StringBuilder sb, ReviewGateResult gate)
        {
            var result = !gate.IsRequired ? "➖ Not required" : gate.Passed ? "✅ Passed" : "❌ Failed";
            sb.AppendLine($"| {gate.Name} | {(gate.IsRequired ? "Yes" : "No")} | {result} | {gate.Reason} |");
        }

        private static string ToBool(bool value) => value ? "true" : "false";
    }
}
