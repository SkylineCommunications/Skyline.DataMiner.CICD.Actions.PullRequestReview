namespace Skyline.DataMiner.CICD.Tools.PullRequestReviewValidator
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Skyline.DataMiner.CICD.Tools.PullRequestReviewValidator.Models;

    /// <summary>
    /// Contains the pure, side-effect free logic used to determine whether a pull request satisfies the
    /// CR, QA and Docs approval gates. Kept separate from <see cref="GitHubService"/> so it can be unit
    /// tested without performing any HTTP calls.
    /// </summary>
    internal class PrReviewEvaluator
    {
        /// <summary>
        /// Evaluates the CR, QA and Docs gates for a pull request.
        /// </summary>
        /// <param name="prAuthor">The GitHub login of the pull request's author. Their own reviews are ignored.</param>
        /// <param name="reviews">All reviews submitted on the pull request.</param>
        /// <param name="organizationMembers">The GitHub logins of the reviewers who are members of the organization.</param>
        /// <param name="docsTeamMembers">The GitHub logins of the members of the docs review team.</param>
        /// <param name="changedFiles">The paths of the files changed by the pull request.</param>
        /// <param name="docsPathPattern">The path segment that marks a changed file as documentation, e.g. "CatalogInformation".</param>
        /// <returns>The aggregated result of the CR, QA and Docs gates.</returns>
        public PrReviewResult Evaluate(
            string prAuthor,
            IReadOnlyList<ReviewInfo> reviews,
            IReadOnlyCollection<string> organizationMembers,
            IReadOnlyCollection<string> docsTeamMembers,
            IReadOnlyList<string> changedFiles,
            string docsPathPattern)
        {
            reviews ??= Array.Empty<ReviewInfo>();
            changedFiles ??= Array.Empty<string>();

            var orgMembers = new HashSet<string>(organizationMembers ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            var docsTeam = new HashSet<string>(docsTeamMembers ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);

            var latestApprovers = GetLatestApprovers(prAuthor, reviews);

            var docsApprovers = latestApprovers.Where(docsTeam.Contains).ToList();
            var codeApprovers = latestApprovers
                                 .Where(u => !docsTeam.Contains(u) && orgMembers.Contains(u))
                                 .ToList();

            var cr = EvaluateCr(codeApprovers);
            var qa = EvaluateQa(codeApprovers);
            var docs = EvaluateDocs(changedFiles, docsPathPattern, docsApprovers);

            return new PrReviewResult(cr, qa, docs);
        }

        private static List<string> GetLatestApprovers(string prAuthor, IReadOnlyList<ReviewInfo> reviews)
        {
            // Keep only the latest review per user (mirrors GitHub's own approval semantics: a later
            // "changes requested" supersedes an earlier approval from the same person).
            var latestPerUser = new Dictionary<string, ReviewInfo>(StringComparer.OrdinalIgnoreCase);

            foreach (var review in reviews)
            {
                if (String.IsNullOrEmpty(review.Username))
                {
                    continue;
                }

                if (!String.IsNullOrEmpty(prAuthor) && String.Equals(review.Username, prAuthor, StringComparison.OrdinalIgnoreCase))
                {
                    // A PR author's own review (e.g. a self-comment) never counts as an approval.
                    continue;
                }

                if (!latestPerUser.TryGetValue(review.Username, out var existing) || review.SubmittedAt >= existing.SubmittedAt)
                {
                    latestPerUser[review.Username] = review;
                }
            }

            return latestPerUser.Values
                                 .Where(r => r.IsApproved)
                                 .Select(r => r.Username)
                                 .ToList();
        }

        private static ReviewGateResult EvaluateCr(IReadOnlyList<string> codeApprovers)
        {
            var passed = codeApprovers.Count >= 1;
            var approvers = passed ? new[] { codeApprovers[0] } : Array.Empty<string>();
            var reason = passed
                ? $"Approved by '{approvers[0]}'."
                : "Requires an approving review from an organization member who is not part of the docs review team.";

            return new ReviewGateResult("CR", isRequired: true, passed, approvers, reason);
        }

        private static ReviewGateResult EvaluateQa(IReadOnlyList<string> codeApprovers)
        {
            var passed = codeApprovers.Count >= 2;
            var approvers = passed ? new[] { codeApprovers[1] } : Array.Empty<string>();
            var reason = passed
                ? $"Approved by '{approvers[0]}'."
                : "Requires a second, distinct approving review from an organization member who is not part of the docs review team.";

            return new ReviewGateResult("QA", isRequired: true, passed, approvers, reason);
        }

        private static ReviewGateResult EvaluateDocs(IReadOnlyList<string> changedFiles, string docsPathPattern, IReadOnlyList<string> docsApprovers)
        {
            var isRequired = !String.IsNullOrEmpty(docsPathPattern) &&
                              changedFiles.Any(f => f?.IndexOf(docsPathPattern, StringComparison.OrdinalIgnoreCase) >= 0);

            if (!isRequired)
            {
                return new ReviewGateResult("Docs", isRequired: false, passed: true, Array.Empty<string>(), "No changes detected under a documentation folder; docs approval is not required.");
            }

            var passed = docsApprovers.Count >= 1;
            var approvers = passed ? new[] { docsApprovers[0] } : Array.Empty<string>();
            var reason = passed
                ? $"Approved by '{approvers[0]}'."
                : "Documentation changes detected; requires an approving review from a member of the docs review team.";

            return new ReviewGateResult("Docs", isRequired: true, passed, approvers, reason);
        }
    }
}
