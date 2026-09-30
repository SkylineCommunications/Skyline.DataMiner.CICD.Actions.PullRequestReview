namespace Skyline.DataMiner.CICD.Tools.PullRequestReviewValidator.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Represents the outcome of a single approval gate (CR, QA or Docs).
    /// </summary>
    public class ReviewGateResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ReviewGateResult"/> class.
        /// </summary>
        /// <param name="name">The name of the gate, e.g. "CR", "QA" or "Docs".</param>
        /// <param name="isRequired">Indicates whether this gate is required for this pull request.</param>
        /// <param name="passed">Indicates whether the gate passed.</param>
        /// <param name="approvers">The reviewers whose approval satisfied this gate.</param>
        /// <param name="reason">A human-readable explanation of the outcome.</param>
        public ReviewGateResult(string name, bool isRequired, bool passed, IReadOnlyList<string> approvers, string reason)
        {
            Name = name;
            IsRequired = isRequired;
            Passed = passed;
            Approvers = approvers;
            Reason = reason;
        }

        /// <summary>
        /// Gets the name of the gate, e.g. "CR", "QA" or "Docs".
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets a value indicating whether this gate is required for this pull request.
        /// </summary>
        public bool IsRequired { get; }

        /// <summary>
        /// Gets a value indicating whether the gate passed. A non-required gate always passes.
        /// </summary>
        public bool Passed { get; }

        /// <summary>
        /// Gets the reviewers whose approval satisfied this gate.
        /// </summary>
        public IReadOnlyList<string> Approvers { get; }

        /// <summary>
        /// Gets a human-readable explanation of the outcome.
        /// </summary>
        public string Reason { get; }
    }
}
