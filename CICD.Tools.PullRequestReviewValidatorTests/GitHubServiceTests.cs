namespace CICD.Tools.PullRequestReviewValidatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    using FluentAssertions;

    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using Serilog;

    using Skyline.DataMiner.CICD.Tools.PullRequestReviewValidator;

    /// <summary>
    /// A fake <see cref="HttpMessageHandler"/> that returns canned JSON responses based on the
    /// requested URL, so <see cref="GitHubService"/>'s JSON parsing logic can be unit tested without
    /// performing real HTTP calls.
    /// </summary>
    internal class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<string, HttpResponseMessage> _responder;

        public FakeHttpMessageHandler(Func<string, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_responder(request.RequestUri.ToString()));
        }

        public static HttpResponseMessage Json(string content) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json"),
        };
    }

    internal static class TestLoggerFactory
    {
        public static Microsoft.Extensions.Logging.ILogger CreateLogger()
        {
            var loggerConfig = new LoggerConfiguration().WriteTo.Console();
            var serilogLogger = loggerConfig.CreateLogger();
            using var loggerFactory = LoggerFactory.Create(builder => builder.AddSerilog(serilogLogger));
            return loggerFactory.CreateLogger("GitHubServiceTests");
        }
    }

    [TestClass]
    public class GitHubServiceTests
    {
        private static Microsoft.Extensions.Logging.ILogger CreateLogger() => TestLoggerFactory.CreateLogger();

        [TestMethod]
        public async Task GetPullRequestAuthorAsync_ParsesLogin()
        {
            var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Json("""{ "user": { "login": "octocat" } }"""));
            var service = new GitHubService(new HttpClient(handler), CreateLogger(), "token", "owner/repo");

            var author = await service.GetPullRequestAuthorAsync(1);

            author.Should().Be("octocat");
        }

        [TestMethod]
        public async Task GetReviewsAsync_ParsesReviewsAndSkipsGhostUsers()
        {
            var handler = new FakeHttpMessageHandler(url => url.Contains("page=2")
                ? FakeHttpMessageHandler.Json("[]")
                : FakeHttpMessageHandler.Json("""
                    [
                      { "user": { "login": "dev-a" }, "state": "APPROVED", "submitted_at": "2024-01-01T10:00:00Z" },
                      { "user": null, "state": "COMMENTED", "submitted_at": "2024-01-01T11:00:00Z" }
                    ]
                    """));
            var service = new GitHubService(new HttpClient(handler), CreateLogger(), "token", "owner/repo");

            var reviews = await service.GetReviewsAsync(1);

            reviews.Should().ContainSingle();
            reviews[0].Username.Should().Be("dev-a");
            reviews[0].IsApproved.Should().BeTrue();
        }

        [TestMethod]
        public async Task GetChangedFilesAsync_ParsesFilenames()
        {
            var handler = new FakeHttpMessageHandler(url => url.Contains("page=2")
                ? FakeHttpMessageHandler.Json("[]")
                : FakeHttpMessageHandler.Json("""
                    [
                      { "filename": "src/Program.cs" },
                      { "filename": "MyConnector/CatalogInformation/README.md" }
                    ]
                    """));
            var service = new GitHubService(new HttpClient(handler), CreateLogger(), "token", "owner/repo");

            var files = await service.GetChangedFilesAsync(1);

            files.Should().BeEquivalentTo(new[] { "src/Program.cs", "MyConnector/CatalogInformation/README.md" });
        }

        [TestMethod]
        public async Task GetTeamMembersAsync_ParsesLogins()
        {
            var handler = new FakeHttpMessageHandler(url => url.Contains("page=2")
                ? FakeHttpMessageHandler.Json("[]")
                : FakeHttpMessageHandler.Json("""
                    [
                      { "login": "docs-reviewer-1" },
                      { "login": "docs-reviewer-2" }
                    ]
                    """));
            var service = new GitHubService(new HttpClient(handler), CreateLogger(), "token", "owner/repo");

            var members = await service.GetTeamMembersAsync("acme", "team-docs-reviewers");

            members.Should().BeEquivalentTo(new[] { "docs-reviewer-1", "docs-reviewer-2" });
        }

        [TestMethod]
        public async Task IsOrganizationMemberAsync_NoContent_ReturnsTrue()
        {
            var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
            var service = new GitHubService(new HttpClient(handler), CreateLogger(), "token", "owner/repo");

            var isMember = await service.IsOrganizationMemberAsync("acme", "dev-a");

            isMember.Should().BeTrue();
        }

        [TestMethod]
        public async Task IsOrganizationMemberAsync_NotFound_ReturnsFalse()
        {
            var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
            var service = new GitHubService(new HttpClient(handler), CreateLogger(), "token", "owner/repo");

            var isMember = await service.IsOrganizationMemberAsync("acme", "external-contributor");

            isMember.Should().BeFalse();
        }
    }

    /// <summary>
    /// These tests require setting up a secret to run and will perform actual HTTP calls to GitHub.
    /// Make sure you provide a valid GitHub PAT (with 'read:org' access), an org, a team slug and a pull
    /// request for these tests to succeed.
    ///
    /// Right-click on the project in Visual Studio and select "Manage User Secrets", then add the
    /// following JSON structure to store your secrets:
    ///
    /// <code>
    /// {
    ///   "GitHubToken": "&lt;YOUR_GITHUB_TOKEN&gt;",
    ///   "GitHubRepository": "&lt;OWNER/REPOSITORY&gt;",
    ///   "PullRequestNumber": "&lt;PR_NUMBER&gt;",
    ///   "Organization": "&lt;ORG&gt;",
    ///   "DocsTeamSlug": "&lt;TEAM_SLUG&gt;"
    /// }
    /// </code>
    /// </summary>
    [TestClass, Ignore]
    public class GitHubServiceIntegrationTests
    {
        private GitHubService _service;
        private string _organization;
        private int _pullRequestNumber;
        private string _docsTeamSlug;

        [TestInitialize]
        public void Setup()
        {
            var config = new ConfigurationBuilder()
                .AddUserSecrets<GitHubServiceIntegrationTests>()
                .AddEnvironmentVariables()
                .Build();

            var githubToken = config["GitHubToken"];
            var githubRepository = config["GitHubRepository"];
            _pullRequestNumber = Int32.Parse(config["PullRequestNumber"] ?? "0");
            _organization = config["Organization"];
            _docsTeamSlug = config["DocsTeamSlug"];

            if (String.IsNullOrEmpty(githubToken) || String.IsNullOrEmpty(githubRepository))
            {
                throw new InvalidOperationException("GitHubToken and GitHubRepository must be provided in User Secrets.");
            }

            _service = new GitHubService(new HttpClient(), TestLoggerFactory.CreateLogger(), githubToken, githubRepository);
        }

        [TestMethod]
        public async Task GetPullRequestAuthorAsyncTest()
        {
            var author = await _service.GetPullRequestAuthorAsync(_pullRequestNumber);

            author.Should().NotBeNullOrEmpty();
        }

        [TestMethod]
        public async Task GetReviewsAsyncTest()
        {
            var reviews = await _service.GetReviewsAsync(_pullRequestNumber);

            reviews.Should().NotBeNull();
        }

        [TestMethod]
        public async Task GetChangedFilesAsyncTest()
        {
            var files = await _service.GetChangedFilesAsync(_pullRequestNumber);

            files.Should().NotBeNull();
        }

        [TestMethod]
        public async Task GetTeamMembersAsyncTest()
        {
            var members = await _service.GetTeamMembersAsync(_organization, _docsTeamSlug);

            members.Should().NotBeNull();
        }
    }
}
