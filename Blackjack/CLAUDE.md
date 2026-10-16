# CLAUDE.md

## Project

Blackjack game in C#: core engine library, console app, simulation/statistics tool, later an Avalonia GUI.
The full requirements are in `docs/SPEC.md`. **Read it before starting any task.**

## Working mode

- Implement **one milestone at a time** (M1 … M5, see SPEC section 10). Do not implement features of later milestones unless asked.
- Start each milestone with a plan; wait for approval before writing code.
- If the spec is unclear or contradictory, ask instead of guessing.
- Do not deviate from the spec silently. Propose changes explicitly.
- Do not add NuGet packages without asking. `Blackjack.Core` has no external dependencies.

## Architecture rules

- `Blackjack.Core` contains no UI code: no `Console`, no Avalonia, no file I/O.
- Player decisions only via `IPlayerStrategy` (async).
- Random source injectable and seedable; tests use `FixedCardSource` for exact card sequences.
- The engine reports via awaited `IGameObserver` notifications (not .NET events) and returns `RoundResult`; it never prints.
- Money amounts are `decimal`.

## Code conventions

- .NET 10, latest C#, nullable enabled, warnings as errors
- File-scoped namespaces, one public type per file
- Records for immutable data, `sealed` by default
- XML doc comments on public members
- English for all identifiers, comments and docs

## Build and test

```bash
dotnet build
dotnet test
```

A task is only complete when the build has no warnings and all tests pass.
Every rule in the spec's acceptance criteria tables must be covered by a test.
