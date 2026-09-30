namespace Skyline.DataMiner.CICD.Tools.PullRequestReviewValidator
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Net;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading.Tasks;

    using Microsoft.Extensions.Logging;

    using Skyline.DataMiner.CICD.Tools.PullRequestReviewValidator.Models;

    /// <summary>
    /// Provides services for retrieving the data (from GitHub) required to validate a pull request's
    /// review state: the PR author, its reviews, its changed files, and org/team membership.
    /// </summary>
    internal class GitHubService : IGitHubService
    {
        private const int PageSize = 100;

        private readonly HttpClient _httpClient;
        private readonly ILogger _logger;
        private readonly string _token;
        private readonly string _repositoryRoot;

        /// <summary>
        /// Initializes a new instance of the <see cref="GitHubService"/> class.
        /// </summary>
        /// <param name="httpClient">The HttpClient instance used for making requests to GitHub's API.</param>
        /// <param name="logger">The logger instance for logging messages.</param>
        /// <param name="token">The GitHub API token used for authorization.</param>
        /// <param name="githubRepository">The GitHub repository in the format 'owner/repo'.</param>
        public GitHubService(HttpClient httpClient, ILogger logger, string token, string githubRepository)
        {
            _httpClient = httpClient;
            _logger = logger;
            _token = token;
            _repositoryRoot = $"https://api.github.com/repos/{githubRepository}";
        }

        /// <inheritdoc/>
        public async Task<string> GetPullRequestAuthorAsync(int pullRequestNumber)
        {
            var requestUrl = $"{_repositoryRoot}/pulls/{pullRequestNumber}";
            using var document = await GetJsonAsync(requestUrl);
            if (document == null)
            {
                return null;
            }

            if (document.RootElement.TryGetProperty("user", out var user) &&
                user.ValueKind == JsonValueKind.Object &&
                user.TryGetProperty("login", out var login))
            {
                return login.GetString();
            }

            _logger.LogWarning("Could not find the author of pull request #{PullRequestNumber}.", pullRequestNumber);
            return null;
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<ReviewInfo>> GetReviewsAsync(int pullRequestNumber)
        {
            var results = new List<ReviewInfo>();

            await foreach (var element in GetPagedJsonAsync($"{_repositoryRoot}/pulls/{pullRequestNumber}/reviews"))
            {
                if (!element.TryGetProperty("user", out var user) ||
                    user.ValueKind != JsonValueKind.Object ||
                    !user.TryGetProperty("login", out var loginProp))
                {
                    // Reviews from deleted/ghost users have a null "user"; skip them.
                    continue;
                }

                var login = loginProp.GetString();
                var state = element.TryGetProperty("state", out var stateProp) ? stateProp.GetString() : null;
                var submittedAt = element.TryGetProperty("submitted_at", out var submittedAtProp) &&
                                   submittedAtProp.ValueKind == JsonValueKind.String
                    ? DateTimeOffset.Parse(submittedAtProp.GetString(), CultureInfo.InvariantCulture)
                    : DateTimeOffset.MinValue;

                results.Add(new ReviewInfo(login, state, submittedAt));
            }

            return results;
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<string>> GetChangedFilesAsync(int pullRequestNumber)
        {
            var results = new List<string>();

            await foreach (var element in GetPagedJsonAsync($"{_repositoryRoot}/pulls/{pullRequestNumber}/files"))
            {
                if (element.TryGetProperty("filename", out var filename))
                {
                    results.Add(filename.GetString());
                }
            }

            return results;
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<string>> GetTeamMembersAsync(string organization, string teamSlug)
        {
            var results = new List<string>();

            await foreach (var element in GetPagedJsonAsync($"https://api.github.com/orgs/{organization}/teams/{teamSlug}/members"))
            {
                if (element.TryGetProperty("login", out var login))
                {
                    results.Add(login.GetString());
                }
            }

            return results;
        }

        /// <inheritdoc/>
        public async Task<bool> IsOrganizationMemberAsync(string organization, string username)
        {
            var requestUrl = $"https://api.github.com/orgs/{organization}/members/{username}";
            using var request = CreateRequest(requestUrl);
            using var response = await _httpClient.SendAsync(request);

            if (response.StatusCode == HttpStatusCode.NoContent)
            {
                return true;
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return false;
            }

            _logger.LogWarning(
                "Unexpected status code {StatusCode} while checking organization membership for '{Username}'.",
                response.StatusCode,
                username);
            return false;
        }

        private HttpRequestMessage CreateRequest(string requestUrl)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            request.Headers.Add("Authorization", $"Bearer {_token}");
            request.Headers.Add("User-Agent", "PullRequestReviewValidator");
            request.Headers.Add("Accept", "application/vnd.github+json");
            request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
            return request;
        }

        private async Task<JsonDocument> GetJsonAsync(string requestUrl)
        {
            using var request = CreateRequest(requestUrl);
            using var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "GitHub request to '{RequestUrl}' failed: {StatusCode} - {ReasonPhrase}",
                    requestUrl,
                    response.StatusCode,
                    response.ReasonPhrase);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            return JsonDocument.Parse(content);
        }

        private async IAsyncEnumerable<JsonElement> GetPagedJsonAsync(string requestUrl)
        {
            var page = 1;
            while (true)
            {
                var pagedUrl = $"{requestUrl}{(requestUrl.Contains('?') ? '&' : '?')}per_page={PageSize}&page={page}";
                using var document = await GetJsonAsync(pagedUrl);
                if (document == null)
                {
                    yield break;
                }

                var itemCount = 0;
                foreach (var element in document.RootElement.EnumerateArray())
                {
                    itemCount++;
                    yield return element.Clone();
                }

                if (itemCount < PageSize)
                {
                    yield break;
                }

                page++;
            }
        }
    }
}
