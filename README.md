# Code Challenge — Make the claims regression suite reliable (30 minutes)

## The situation

You just joined the team that owns release quality for a healthcare claims exchange. This repository has a small version of its API, a staff page to review claims, and part of the release regression suite (C#, NUnit, Playwright for the page and HttpClient for the API).

The suite fails most nights, often in a different test each time, and the team reruns it until it is green. The release is next week. The team also suspects that some tests pass without checking what their name says.

You may use your usual AI assistant. Please share your screen and explain what you ask it and how you check what it gives you.

## Ticket QA-342 — Make the release suite trustworthy

1. Find which tests are flaky, and why. A rerun until green is not an answer.
2. Fix the tests so that the whole suite gives the same result every time it runs, in any order and in parallel.
3. Every test checks what its name says.
4. New rule from the security review: **a member can read only their own claims.** When a member signs in and requests another member's claims, the API must answer `403 Forbidden`. Staff can read any member's claims.

## Steps

1. **Diagnose (10 minutes).** Run the suite a few times. Tell us which tests are flaky, the cause of each, and what you would fix first. Do not change code yet.
2. **Fix (12 minutes).** Fix the tests and show that the suite now gives the same result every time.
3. **Extend (8 minutes).** Add an API test for rule 4. If your test finds a problem in the API, do not fix the API: tell us how you would report it.

## Rules

- Change the tests, not the API or the page (`api/`). If you think the API is wrong, say so.
- Do not delete or skip tests, do not add retries, and do not simply make the waits longer.
- Do not add new libraries.
- If you use an AI assistant, review every change it proposes before accepting it.

## Running the project

**With Docker (recommended).** You only need Docker Desktop. Clone the repository and run, from its folder:

```bash
docker compose run --rm tests                    # run the whole suite (starts the API the first time)
docker compose run --rm tests dotnet test --filter "Name~Search"      # run some tests only
docker compose restart api                       # reset the API data
```

The page is on http://localhost:5080 while the API runs. Edit the tests in your usual editor; each run picks up your changes. The first run downloads the image and the NuGet packages, which takes a few minutes.

**Without Docker.** With the .NET 8 SDK installed, in two terminals:

```bash
cd api && dotnet run --urls http://localhost:5080
cd tests && dotnet build && pwsh bin/Debug/net8.0/playwright.ps1 install chromium && dotnet test
```

The repository also works in a GitHub Codespace (Code → Codespaces).

## Files

- `tests/ClaimsApiTests.cs`: API tests.
- `tests/ClaimsPageTests.cs`: tests of the staff page (Playwright).
- `tests/TestSettings.cs`: API address, test users, getting a token, and how the suite runs in parallel.
- `api/Program.cs`: the API and its test data (in memory; it resets when the API restarts). Members `M-1001`, `M-1002` and `M-1003` sign in with password `member-local`; staff with `staff` / `staff-local`.
- `api/wwwroot/index.html`: the staff page.
- `compose.yaml`: runs the API and the tests in Docker.

Think out loud as you work. We are interested in how you reason, not only in the final code.
