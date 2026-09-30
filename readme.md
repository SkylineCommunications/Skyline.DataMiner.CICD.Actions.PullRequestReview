# Skyline.DataMiner.CICD.Actions.PullRequestReview

This repository contains the **Skyline.DataMiner.CICD.Tools.PullRequestReviewValidator** .NET tool: a
command-line tool intended to be run from a GitHub Actions workflow to validate that a pull request
has gone through the review process required before it can be merged:

- **CR** – an approving review from an organization member who is not part of the docs review team.
- **QA** – a second, distinct approving review from another organization member who is not part of the
  docs review team.
- **Docs** – only required when the pull request changes a file under a documentation folder (by
  default, any path containing `CatalogInformation`); requires an approving review from a member of
  the docs review team.

The tool exits with code `0` when every applicable gate has passed, and `1` otherwise, so its
containing workflow job can be added as a required status check in a repository ruleset to block
merging until CR, QA and (when applicable) Docs have all been approved.

See:

- [`Skyline.DataMiner.CICD.Tools.PullRequestReviewValidator/README.md`](Skyline.DataMiner.CICD.Tools.PullRequestReviewValidator/README.md)
  for full usage documentation (CLI options, GitHub Actions outputs, required token permissions).
- [`examples/pr-review-workflow.yml`](examples/pr-review-workflow.yml) for a ready-to-copy example
  workflow that consumes the tool.
