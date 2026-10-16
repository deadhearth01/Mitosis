# Windows CI (Stage 11)

`.github/workflows/windows-ci.yml` restores, builds (Release), and tests the
Windows solution on `windows-latest`. Read-only (`contents: read`): no secrets,
no signing, no publishing, no deployment. macOS workflows are untouched.

## Triggers, runner, permissions

- Push to `WindowsVersion`, any pull request, manual `workflow_dispatch`.
- Runner `windows-latest`, 30 min timeout, concurrency per ref with
  cancel-in-progress (same shape as `ci.yml`).
- Third-party actions use the repo's tag policy (`checkout@v5`, `setup-dotnet@v4`,
  `upload-artifact@v4`).

## SDK selection

`setup-dotnet` installs `10.0.x`, satisfying `windows/global.json` (10.0.100 with
`latestFeature` roll-forward, MSTest.Sdk 4.5.1, MTP test runner).

## Commands (MTP-mode `dotnet test` syntax)

```powershell
cd windows
dotnet --info
dotnet restore Mitosis.sln
dotnet build Mitosis.sln -c Release --no-restore
dotnet test --solution Mitosis.sln -c Release --no-build --report-trx
```

`--report-trx` works out of the box with the pinned MSTest.Sdk (no extra
dependency): one `.trx` per test project under `windows/TestResults/`, uploaded
as the `windows-test-results` artifact (14-day retention) even on failure
(`if: always()`). Nonzero exit codes fail the run; nothing is skipped or weakened.

## Reproducing locally

Run the same commands from `windows/` with SDK 10.0.402+ (verified with
`dotnet --list-sdks`). Inspect failures in the console output or open the `.trx`
files (XML: per-test outcome, message, stack trace). `TestResults/` is gitignored.

## Known limitations

- CI compiles and runs unit tests only. It does not launch the WinUI app, check
  pixels, run Narrator, verify isolation, review licensing, or assess packaging.
- The WinUI app project builds headless (XAML compiles, MSIX not involved); the
  unpackaged runtime requirement applies at launch, not at build/test time.
- No remote CI run has been observed from this environment; status must be read
  from the GitHub Actions tab after push.
