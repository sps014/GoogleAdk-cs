# How to contribute

We'd love to accept your patches and contributions to this project.

-   [How to contribute](#how-to-contribute)
-   [Before you begin](#before-you-begin)
    -   [Review our community guidelines](#review-our-community-guidelines)
-   [Contribution workflow](#contribution-workflow)
    -   [Finding Issues to Work On](#finding-issues-to-work-on)
    -   [Requirement for PRs](#requirement-for-prs)
    -   [Large or Complex Changes](#large-or-complex-changes)
    -   [Testing Requirements](#testing-requirements)
    -   [Unit Tests](#unit-tests)
    -   [Manual End-to-End (E2E) Tests](#manual-end-to-end-e2e-tests)
    -   [Documentation](#documentation)
    -   [Development Setup](#development-setup)
    -   [Code reviews](#code-reviews)

## Before you begin

### Review our community guidelines

This project follows
[Google's Open Source Community Guidelines](https://opensource.google/conduct/).

### Code reviews

All submissions, including submissions by project members, require review. We
use GitHub pull requests for this purpose. Consult
[GitHub Help](https://help.github.com/articles/about-pull-requests/) for more
information on using pull requests.

## Contribution workflow

### Finding Issues to Work On

-   Browse issues labeled **`good first issue`** (newcomer-friendly) or **`help wanted`** (general contributions).
-   For other issues, please kindly ask before contributing to avoid duplication.

### Requirement for PRs

-   All PRs, other than small documentation or typo fixes, should have an Issue associated. If a relevant issue doesn't exist, please create one first or you may instead describe the bug or feature directly within the PR description, following the structure of our issue templates.
-   Small, focused PRs. Keep changes minimal—one concern per PR.
-   For bug fixes or features, please provide logs or screenshots after the fix is applied to help reviewers better understand the fix.
-   Please include a `testing plan` section in your PR to describe how you will test. This will save time for PR review. See `Testing Requirements` section for more details.

### Large or Complex Changes

For substantial features or architectural revisions:

-   Open an Issue First: Outline your proposal, including design considerations and impact.
-   Gather Feedback: Discuss with maintainers and the community to ensure alignment and avoid duplicate work.

### Testing Requirements

To maintain code quality and prevent regressions, all code changes must include comprehensive tests and verifiable end-to-end (E2E) evidence.

#### Unit Tests

Please add or update unit tests for your change. Please include a summary of passed `dotnet test` results.

Requirements for unit tests:

-   **Coverage:** Cover new features, edge cases, error conditions, and typical use cases.
-   **Location:** Add or update tests in the `tests/GoogleAdk.Core.Tests/` or `tests/GoogleAdk.E2e.Tests/` projects.
-   **Framework:** Use `xUnit`. Tests should be:
    -   Fast and isolated.
    -   Written clearly with descriptive names.
    -   Free of external dependencies (use mocks or fixtures as needed).
-   **Quality:** Aim for high readability and maintainability.

#### Manual End-to-End (E2E) Tests

Manual E2E tests ensure integrated flows work as intended. Your tests should cover all scenarios.

Depending on your change:

-   **ADK Web:**
    -   Use `AdkServer.RunAsync` to verify UI functionality.
    -   Capture and attach relevant screenshots demonstrating the UI/UX changes or outputs.
    -   Label screenshots clearly in your PR description.

-   **Runner:**
    -   Provide the testing setup (e.g., agent configuration, tools).
    -   Execute a sample application (like those in `samples/`) to reproduce workflows.
    -   Include the console output showing test results.

### Documentation

For any changes that impact user-facing documentation (guides, API reference, tutorials), please update the Markdown files in the `docs/` folder. We use MkDocs to build the documentation.

## Development Setup

1.  **Clone the repository:**

    ```shell
    gh repo clone sps014/GoogleAdk-cs
    cd GoogleAdk-cs/GoogleAdk
    ```

2.  **Prerequisites:**

    Ensure you have the .NET SDK installed (e.g., .NET 10.0 preview).

3.  **Restore dependencies:**

    ```shell
    dotnet restore GoogleAdk.slnx
    ```

4.  **Build the solution:**

    ```shell
    dotnet build GoogleAdk.slnx
    ```

5.  **Run unit tests:**

    ```shell
    dotnet test GoogleAdk.slnx
    ```

6.  **Code Formatting:**

    Ensure your code complies with standard C# formatting rules before opening a PR:

    ```shell
    dotnet format GoogleAdk.slnx
    ```

7.  **Test Local Packages:**

    If you need to test the packages locally in another application, you can pack them into a local NuGet directory:

    ```shell
    dotnet pack GoogleAdk.slnx -o ./nupkgs
    ```

    Then, reference the `./nupkgs` directory as a local package source in your target application.

## Vibe Coding

If you want to contribute by leveraging vibe coding, the `AGENTS.md` file (if available in this repository) can be used as context to your LLM to ensure generated code follows our project structure and architectural patterns.