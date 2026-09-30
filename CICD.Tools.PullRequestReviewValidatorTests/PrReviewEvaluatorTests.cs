namespace CICD.Tools.PullRequestReviewValidatorTests
{
    using System;
    using System.Collections.Generic;

    using FluentAssertions;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using Skyline.DataMiner.CICD.Tools.PullRequestReviewValidator;
    using Skyline.DataMiner.CICD.Tools.PullRequestReviewValidator.Models;

    [TestClass]
    public class PrReviewEvaluatorTests
    {
        private const string Author = "author-user";
        private const string DevA = "dev-a";
        private const string DevB = "dev-b";
        private const string DocsReviewer = "docs-reviewer";
        private const string NonOrgMember = "external-contributor";

        private readonly PrReviewEvaluator _evaluator = new();

        private static readonly IReadOnlyCollection<string> OrgMembers = new[] { DevA, DevB, DocsReviewer };
        private static readonly IReadOnlyCollection<string> DocsTeam = new[] { DocsReviewer };

        [TestMethod]
        public void Evaluate_NoReviews_CrAndQaFail_DocsNotRequired()
        {
            var result = _evaluator.Evaluate(Author, Array.Empty<ReviewInfo>(), OrgMembers, DocsTeam, Array.Empty<string>(), "CatalogInformation");

            result.Cr.Passed.Should().BeFalse();
            result.Qa.Passed.Should().BeFalse();
            result.Docs.IsRequired.Should().BeFalse();
            result.Docs.Passed.Should().BeTrue();
            result.OverallPassed.Should().BeFalse();
        }

        [TestMethod]
        public void Evaluate_OneApprovingOrgReview_CrPasses_QaFails()
        {
            var reviews = new[]
            {
                new ReviewInfo(DevA, "APPROVED", DateTimeOffset.UtcNow),
            };

            var result = _evaluator.Evaluate(Author, reviews, OrgMembers, DocsTeam, Array.Empty<string>(), "CatalogInformation");

            result.Cr.Passed.Should().BeTrue();
            result.Cr.Approvers.Should().ContainSingle().Which.Should().Be(DevA);
            result.Qa.Passed.Should().BeFalse();
            result.OverallPassed.Should().BeFalse();
        }

        [TestMethod]
        public void Evaluate_TwoDistinctApprovingOrgReviews_CrAndQaPass()
        {
            var reviews = new[]
            {
                new ReviewInfo(DevA, "APPROVED", DateTimeOffset.UtcNow.AddMinutes(-5)),
                new ReviewInfo(DevB, "APPROVED", DateTimeOffset.UtcNow),
            };

            var result = _evaluator.Evaluate(Author, reviews, OrgMembers, DocsTeam, Array.Empty<string>(), "CatalogInformation");

            result.Cr.Passed.Should().BeTrue();
            result.Qa.Passed.Should().BeTrue();
            result.OverallPassed.Should().BeTrue();
        }

        [TestMethod]
        public void Evaluate_SamePersonApprovingTwice_DoesNotSatisfyQa()
        {
            var reviews = new[]
            {
                new ReviewInfo(DevA, "APPROVED", DateTimeOffset.UtcNow.AddMinutes(-5)),
                new ReviewInfo(DevA, "APPROVED", DateTimeOffset.UtcNow),
            };

            var result = _evaluator.Evaluate(Author, reviews, OrgMembers, DocsTeam, Array.Empty<string>(), "CatalogInformation");

            result.Cr.Passed.Should().BeTrue();
            result.Qa.Passed.Should().BeFalse();
        }

        [TestMethod]
        public void Evaluate_LatestReviewSupersedesEarlierApproval()
        {
            var reviews = new[]
            {
                new ReviewInfo(DevA, "APPROVED", DateTimeOffset.UtcNow.AddMinutes(-5)),
                new ReviewInfo(DevA, "CHANGES_REQUESTED", DateTimeOffset.UtcNow),
                new ReviewInfo(DevB, "APPROVED", DateTimeOffset.UtcNow),
            };

            var result = _evaluator.Evaluate(Author, reviews, OrgMembers, DocsTeam, Array.Empty<string>(), "CatalogInformation");

            // DevA's approval was superseded by a later "changes requested", so only DevB's approval counts.
            result.Cr.Passed.Should().BeTrue();
            result.Cr.Approvers.Should().ContainSingle().Which.Should().Be(DevB);
            result.Qa.Passed.Should().BeFalse();
        }

        [TestMethod]
        public void Evaluate_PrAuthorSelfApproval_IsIgnored()
        {
            var reviews = new[]
            {
                new ReviewInfo(Author, "APPROVED", DateTimeOffset.UtcNow),
                new ReviewInfo(DevA, "APPROVED", DateTimeOffset.UtcNow),
            };

            var result = _evaluator.Evaluate(Author, reviews, OrgMembers, DocsTeam, Array.Empty<string>(), "CatalogInformation");

            result.Cr.Passed.Should().BeTrue();
            result.Cr.Approvers.Should().ContainSingle().Which.Should().Be(DevA);
            result.Qa.Passed.Should().BeFalse();
        }

        [TestMethod]
        public void Evaluate_NonOrgMemberApproval_DoesNotCountTowardsCrOrQa()
        {
            var reviews = new[]
            {
                new ReviewInfo(NonOrgMember, "APPROVED", DateTimeOffset.UtcNow.AddMinutes(-5)),
                new ReviewInfo(DevA, "APPROVED", DateTimeOffset.UtcNow),
            };

            var result = _evaluator.Evaluate(Author, reviews, OrgMembers, DocsTeam, Array.Empty<string>(), "CatalogInformation");

            result.Cr.Passed.Should().BeTrue();
            result.Cr.Approvers.Should().ContainSingle().Which.Should().Be(DevA);
            result.Qa.Passed.Should().BeFalse();
        }

        [TestMethod]
        public void Evaluate_DocsTeamApproval_DoesNotCountTowardsCrOrQa()
        {
            var reviews = new[]
            {
                new ReviewInfo(DocsReviewer, "APPROVED", DateTimeOffset.UtcNow.AddMinutes(-5)),
                new ReviewInfo(DevA, "APPROVED", DateTimeOffset.UtcNow),
            };

            var result = _evaluator.Evaluate(Author, reviews, OrgMembers, DocsTeam, Array.Empty<string>(), "CatalogInformation");

            result.Cr.Passed.Should().BeTrue();
            result.Cr.Approvers.Should().ContainSingle().Which.Should().Be(DevA);
            result.Qa.Passed.Should().BeFalse();
        }

        [TestMethod]
        public void Evaluate_NoDocsFolderChanged_DocsGateNotRequired()
        {
            var changedFiles = new[] { "src/Program.cs", "README.md" };

            var result = _evaluator.Evaluate(Author, Array.Empty<ReviewInfo>(), OrgMembers, DocsTeam, changedFiles, "CatalogInformation");

            result.Docs.IsRequired.Should().BeFalse();
            result.Docs.Passed.Should().BeTrue();
        }

        [TestMethod]
        public void Evaluate_DocsFolderChangedWithoutApproval_DocsGateFails()
        {
            var changedFiles = new[] { "MyConnector/CatalogInformation/README.md" };

            var result = _evaluator.Evaluate(Author, Array.Empty<ReviewInfo>(), OrgMembers, DocsTeam, changedFiles, "CatalogInformation");

            result.Docs.IsRequired.Should().BeTrue();
            result.Docs.Passed.Should().BeFalse();
            result.OverallPassed.Should().BeFalse();
        }

        [TestMethod]
        public void Evaluate_DocsFolderChangedWithDocsTeamApproval_DocsGatePasses()
        {
            var changedFiles = new[] { "MyConnector/CatalogInformation/README.md" };
            var reviews = new[]
            {
                new ReviewInfo(DocsReviewer, "APPROVED", DateTimeOffset.UtcNow),
                new ReviewInfo(DevA, "APPROVED", DateTimeOffset.UtcNow),
                new ReviewInfo(DevB, "APPROVED", DateTimeOffset.UtcNow),
            };

            var result = _evaluator.Evaluate(Author, reviews, OrgMembers, DocsTeam, changedFiles, "CatalogInformation");

            result.Docs.IsRequired.Should().BeTrue();
            result.Docs.Passed.Should().BeTrue();
            result.Docs.Approvers.Should().ContainSingle().Which.Should().Be(DocsReviewer);
            result.OverallPassed.Should().BeTrue();
        }

        [TestMethod]
        public void Evaluate_DocsFolderChangedWithNonDocsTeamApproval_DocsGateStillFails()
        {
            var changedFiles = new[] { "MyConnector/CatalogInformation/README.md" };
            var reviews = new[]
            {
                new ReviewInfo(DevA, "APPROVED", DateTimeOffset.UtcNow),
                new ReviewInfo(DevB, "APPROVED", DateTimeOffset.UtcNow),
            };

            var result = _evaluator.Evaluate(Author, reviews, OrgMembers, DocsTeam, changedFiles, "CatalogInformation");

            result.Docs.IsRequired.Should().BeTrue();
            result.Docs.Passed.Should().BeFalse();
            result.OverallPassed.Should().BeFalse();
        }
    }
}
