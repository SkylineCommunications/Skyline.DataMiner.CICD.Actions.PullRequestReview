namespace Skyline.DataMiner.CICD.Tools.PullRequestReviewValidator.Models
{
    using System;

    /// <summary>
    /// Represents a single review submitted on a pull request.
    /// </summary>
    public class ReviewInfo
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ReviewInfo"/> class.
        /// </summary>
        /// <param name="username">The GitHub login of the reviewer.</param>
        /// <param name="state">The review state, e.g. "APPROVED", "CHANGES_REQUESTED", "COMMENTED", "DISMISSED".</param>
        /// <param name="submittedAt">The timestamp at which the review was submitted.</param>
        public ReviewInfo(string username, string state, DateTimeOffset submittedAt)
        {
            Username = username;
            State = state;
            SubmittedAt = submittedAt;
        }

        /// <summary>
        /// Gets the GitHub login of the reviewer.
        /// </summary>
        public string Username { get; }

        /// <summary>
        /// Gets the review state, e.g. "APPROVED", "CHANGES_REQUESTED", "COMMENTED", "DISMISSED".
        /// </summary>
        public string State { get; }

        /// <summary>
        /// Gets the timestamp at which the review was submitted.
        /// </summary>
        public DateTimeOffset SubmittedAt { get; }

        /// <summary>
        /// Gets a value indicating whether this review is an approval.
        /// </summary>
        public bool IsApproved => String.Equals(State, "APPROVED", StringComparison.OrdinalIgnoreCase);
    }
}
