namespace Skyline.DataMiner.CICD.Tools.PullRequestReviewValidator
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using Skyline.DataMiner.CICD.Tools.PullRequestReviewValidator.Models;

    /// <summary>
    /// Provides services for retrieving the data (from GitHub) required to validate a pull request's
    /// review state: the PR author, its reviews, its changed files, and org/team membership.
    /// </summary>
    internal interface IGitHubService
    {
        /// <summary>
        /// Retrieves the GitHub login of the pull request's author.
        /// </summary>
        /// <param name="pullRequestNumber">The pull request number.</param>
        /// <returns>A task representing the asynchronous operation, containing the author's GitHub login.</returns>
        Task<string> GetPullRequestAuthorAsync(int pullRequestNumber);

        /// <summary>
        /// Retrieves all reviews submitted on the pull request.
        /// </summary>
        /// <param name="pullRequestNumber">The pull request number.</param>
        /// <returns>A task representing the asynchronous operation, containing the list of reviews.</returns>
        Task<IReadOnlyList<ReviewInfo>> GetReviewsAsync(int pullRequestNumber);

        /// <summary>
        /// Retrieves the paths of all files changed by the pull request.
        /// </summary>
        /// <param name="pullRequestNumber">The pull request number.</param>
        /// <returns>A task representing the asynchronous operation, containing the list of changed file paths.</returns>
        Task<IReadOnlyList<string>> GetChangedFilesAsync(int pullRequestNumber);

        /// <summary>
        /// Retrieves the GitHub logins of all members of the given team.
        /// </summary>
        /// <param name="organization">The GitHub organization that owns the team.</param>
        /// <param name="teamSlug">The slug of the team.</param>
        /// <returns>A task representing the asynchronous operation, containing the list of member logins.</returns>
        Task<IReadOnlyList<string>> GetTeamMembersAsync(string organization, string teamSlug);

        /// <summary>
        /// Checks whether the given user is a member of the organization.
        /// </summary>
        /// <param name="organization">The GitHub organization.</param>
        /// <param name="username">The GitHub login to check.</param>
        /// <returns>A task representing the asynchronous operation, containing <see langword="true"/> if the user is an organization member.</returns>
        Task<bool> IsOrganizationMemberAsync(string organization, string username);
    }
}
