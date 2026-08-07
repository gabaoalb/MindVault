# Repository Guidelines

## Project Structure & Module Organization

MindVault is a .NET 10 CLI organized as a layered solution:

- `src/MindVault.Domain`: entities, value objects, invariants, and explicit result/error types. It must not reference other solution projects.
- `src/MindVault.Application`: use cases and narrow interfaces for configuration, notes, editors, and filesystem behavior.
- `src/MindVault.Infrastructure`: JSON/YAML persistence, physical filesystem access, slug generation, and external-process execution.
- `src/MindVault.Cli`: `System.CommandLine` commands, dependency injection, Portuguese user messages, and exit-code mapping.
- `tests/*.Tests`: xUnit tests corresponding to Domain, Application, and Infrastructure.

Markdown files in the configured vault are the source of truth. Do not introduce a database or features listed only in the README roadmap.

## Build, Test, and Development Commands

From the repository root:

```powershell
dotnet restore MindVault.slnx
dotnet build MindVault.slnx --no-restore
dotnet test MindVault.slnx --no-build --no-restore
dotnet run --project src/MindVault.Cli -- --help
```

`restore` downloads pinned packages, `build` treats warnings as errors, and `test` runs all xUnit projects. For isolated CLI testing, point `MINDVAULT_CONFIG_PATH` to a temporary JSON file.

## Coding Style & Naming Conventions

Use four-space indentation and standard C# conventions: `PascalCase` for public types and members, `camelCase` for parameters and locals, and `I` prefixes for interfaces. Keep nullable reference types enabled and pass `CancellationToken` through asynchronous I/O APIs. Write code and identifiers in English; keep CLI-facing messages in Brazilian Portuguese.

Prefer composition, explicit results, and focused classes. Avoid generic repositories, catch-all classes, business rules in command handlers, and infrastructure dependencies in Domain.

## Testing Guidelines

Name tests after observable behavior, such as `Create_fails_without_vault`. Test invariants and failure paths rather than trivial properties. Application tests should use focused fakes; filesystem tests must use temporary directories and must not depend on Git, internet access, user configuration, or an installed editor.

## Commit & Pull Request Guidelines

No Git history is available in this workspace. Use short, imperative commit subjects, preferably Conventional Commits, for example `feat(cli): add note listing` or `fix(storage): reject vault traversal`.

Pull requests should explain the behavior change, identify affected commands, list verification commands, and note compatibility or security implications. Include terminal output for CLI presentation changes; screenshots are unnecessary unless a future visual interface is introduced.

## Security & Configuration

Never accept arbitrary paths for note operations. Normalize paths, enforce direct vault containment, reject escaping links, create files atomically, and never overwrite notes silently. Do not commit personal vault paths or real configuration files.
