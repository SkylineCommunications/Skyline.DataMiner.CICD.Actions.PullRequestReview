# Skyline.DataMiner.CICD.Tools.PullRequestReviewValidator

## Overview

The **Skyline.DataMiner.CICD.Tools.PullRequestReviewValidator** tool validates that a pull request has
gone through the review process required before it can be merged:

- **CR** – an approving review from an organization member who is **not** part of the docs review team.
- **QA** – a **second, distinct** approving review from another organization member who is **not**
  part of the docs review team.
- **Docs** – only required when the pull request changes a file under a documentation folder (by
  default, any path containing `CatalogInformation`); requires an approving review from a member of
  the docs review team (by default the `TEAM-DOCS-REVIEWERS` team).

Only the **latest** review submitted by each person counts, mirroring GitHub's own approval semantics:
if someone approves and later requests changes, that person's approval no longer counts until they
approve again. The pull request author's own reviews are never counted.

The tool exits with code `0` when every applicable gate has passed, and `1` otherwise, so it can be
used directly as a required status check in a repository ruleset. It also prints a summary table to
the console and, when run inside a GitHub Actions workflow, writes:

- `GITHUB_OUTPUT` — `cr-passed`, `qa-passed`, `docs-required`, `docs-approved`, `overall-passed`
  (each `true`/`false`), so later steps can react to individual gates.
- `GITHUB_STEP_SUMMARY` — a Markdown table shown in the workflow run summary.

## Installation & Usage

### Prerequisites

Ensure you have [.NET](https://dotnet.microsoft.com/download) installed to run the tool.

A GitHub **PAT** (or GitHub App installation token) with `read:org` access is required. The default
`secrets.GITHUB_TOKEN` provided in GitHub Actions does **not** have enough permissions to check
organization or team membership, so it cannot be used here.

### Installation

Install the tool via the terminal:

```bash
dotnet tool install -g Skyline.DataMiner.CICD.Tools.PullRequestReviewValidator
```

### Running the Tool

Execute the tool using the following command and options:

```bash
pr-review-validate --github-token "your_token" --github-repository "owner/repo" --pr-number 123
```

### Command Options

- `--github-token` (required): A PAT or GitHub App token with `read:org` access.
- `--github-repository` (required): The GitHub repository in the format `owner/repo`.
- `--pr-number` (required): The pull request number to validate.
- `--docs-team` (optional): The slug of the GitHub team whose members may approve documentation
  changes. Defaults to `TEAM-DOCS-REVIEWERS`.
- `--docs-path-pattern` (optional): A path segment that, when present in a changed file's path, marks
  the pull request as requiring a docs approval. Defaults to `CatalogInformation`.
- `--debug` (optional): Enable debug logging for detailed output.

## Example Usage in a GitHub Workflow

> **Note:** the snippet below assumes the tool has been published to NuGet. See the repository's
> [`examples/pr-review-workflow.yml`](../examples/pr-review-workflow.yml) for a ready-to-copy workflow.

```yaml
name: PR Review Validation

on:
  pull_request:
    types: [opened, reopened, synchronize, ready_for_review]
  pull_request_review:
    types: [submitted, edited, dismissed]

jobs:
  validate-pr-review:
    name: Validate CR / QA / Docs approvals
    runs-on: ubuntu-latest
    steps:
      - name: Install .NET Tool
        run: dotnet tool install -g Skyline.DataMiner.CICD.Tools.PullRequestReviewValidator --prerelease

      - name: Validate pull request reviews
        run: |
          pr-review-validate \
            --github-token "${{ secrets.PR_REVIEW_VALIDATOR_TOKEN }}" \
            --github-repository "${{ github.repository }}" \
            --pr-number "${{ github.event.pull_request.number }}"
```

`secrets.PR_REVIEW_VALIDATOR_TOKEN` must be a PAT or GitHub App token with `read:org` access,
configured as an organization or repository secret. Add the job name above (`Validate CR / QA / Docs
approvals`) as a required status check in the target branch's ruleset to block merging until CR, QA
and (when applicable) Docs have all been approved.
