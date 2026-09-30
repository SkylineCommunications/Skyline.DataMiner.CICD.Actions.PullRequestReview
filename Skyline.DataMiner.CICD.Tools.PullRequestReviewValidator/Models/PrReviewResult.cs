namespace Skyline.DataMiner.CICD.Tools.PullRequestReviewValidator.Models
{
    /// <summary>
    /// Represents the aggregated result of validating a pull request's CR, QA and Docs approval gates.
    /// </summary>
    public class PrReviewResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PrReviewResult"/> class.
        /// </summary>
        /// <param name="cr">The result of the CR (code review) gate.</param>
        /// <param name="qa">The result of the QA gate.</param>
        /// <param name="docs">The result of the Docs gate.</param>
        public PrReviewResult(ReviewGateResult cr, ReviewGateResult qa, ReviewGateResult docs)
        {
            Cr = cr;
            Qa = qa;
            Docs = docs;
        }

        /// <summary>
        /// Gets the result of the CR (code review) gate.
        /// </summary>
        public ReviewGateResult Cr { get; }

        /// <summary>
        /// Gets the result of the QA gate.
        /// </summary>
        public ReviewGateResult Qa { get; }

        /// <summary>
        /// Gets the result of the Docs gate.
        /// </summary>
        public ReviewGateResult Docs { get; }

        /// <summary>
        /// Gets a value indicating whether all required gates passed.
        /// </summary>
        public bool OverallPassed => Cr.Passed && Qa.Passed && Docs.Passed;
    }
}
