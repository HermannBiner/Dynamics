# Dynamics – notes for Claude Code

C# / Blazor WebAssembly port of the VB.NET WinForms application Simulator. The README is the reference for the
structure, the name mappings, the corrected VB errors and the comparison with VB; keep it up to date with every change.

## Status

All systems of Simulator are ported (pendulum, numeric methods, Julia/Mandelbrot, Newton, strange attractors,
billiards, universe, growth models, L-systems) and compute in `Double` since step 6. AOT was decided against
(Release build fast enough, see README). Next (2026-11-12, by the user): optimise the GUI and simplify the user
actions; then move to VS 2026. The logic is in the controllers of `Dynamics.Core` and covered by the tests, so the pages
can be changed freely.

## Working rules

- The user writes in German; answer in German. Code, comments, commit messages and the README are in English.
- Commit only after the user agreed; then push to `origin` (github.com/HermannBiner/Dynamics) right after the commit.
- Simulator (https://github.com/HermannBiner/Simulator, local `..\..\Simulator 8.0 GitHubRep\Simulator`) is a
  separate project that the user develops independently (correcting the VB errors found during the port). The
  reference for the Dynamics tests stays its tag `dynamics-reference` (commit `200c300`); `tools/build-simulator.ps1`
  builds that tag, not the working tree. Work in Dynamics does not change Simulator. The corrected VB errors are listed
  in the README table "Errors of Simulator corrected in Dynamics" and checked mathematically in the tests.
- Models in `Dynamics.Core` return state only, without UI; the pages draw (`Drawing/DrawBatch`, `wwwroot/js/canvas.js`).
- Tests compare with the VB reference data with tolerance (`tests/Dynamics.Tests/ReferenceData.cs`: `Close`,
  `FollowsVB` for chaotic trajectories, `AssertColumn` for diagram columns). Regenerate the data only with
  `tools/build-simulator.ps1` and `tools/ReferenceExport` (README, "Reference data").

## Dev server

`dotnet run --project src/Dynamics.Web` serves http://localhost:5257. After any `dotnet build` or `dotnet test` the
running server keeps the old fingerprinted framework file names and the app hangs at "loading": restart the server as
the last step before the user tests, and check that the page loads (heading present, no 4xx resources).
Pages can be checked headless with Edge: `msedge --headless=new --virtual-time-budget=20000 --dump-dom <url>`.

## Pitfalls

- Tent map: its step (`Tentmap.F`) computes in Decimal on purpose; in binary Double a = 2 falls to 0 after ~53 steps.
- Double does not throw on overflow: models use `NumericCheck` (angle loops would never end with NaN or infinity),
  `DrawBatch` drops commands with non-finite numbers.
- Reference export: the VB turtles await `Task.Delay`; set `WindowsFormsSynchronizationContext.AutoInstall = false` on
  the STA thread or the export deadlocks.
- The repository is in OneDrive: a `dotnet publish` can fail because a folder in `obj` is locked; running it again helps.
- Windows PowerShell 5.1: a trailing backslash in a quoted native argument (`"-p:OutputPath=dir\"`) escapes the quote.
