namespace Skyline.DataMiner.CICD.Tools.PullRequestReviewValidator
{
    /// <summary>
    /// Default values used by the tool when no explicit option is provided.
    /// </summary>
    internal static class Constants
    {
        /// <summary>
        /// The default slug of the GitHub team whose members are allowed to approve documentation changes.
        /// </summary>
        public const string DefaultDocsTeamSlug = "TEAM-DOCS-REVIEWERS";

        /// <summary>
        /// The default path segment that, when present in a changed file's path, marks the pull request as
        /// requiring a documentation approval.
        /// </summary>
        public const string DefaultDocsPathPattern = "CatalogInformation";
    }
}
