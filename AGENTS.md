# Repository Guidelines

## Project Structure & Module Organization

MindVault is a .NET 10 CLI organized as a layered solution:

- `src/MindVault.Domain`: entities, value objects, invariants, and explicit result/error types. It must not reference other solution projects.
- `src/MindVault.Application`: use cases and narrow interfaces for configuration, notes, editors, and filesystem behavior.
- `src/MindVault.Infrastructure`: JSON/YAML persistence, physical filesystem access, slug generation, and external-process execution.
- `src/MindVault.Cli`: `System.CommandLine` commands, dependency injection, Portuguese user messages, and exit-code mapping.
- `tests/*.Tests`: xUnit tests corresponding to Domain, Application, and Infrastructure.

Markdown files in the configured vault are the source of truth. Do not introduce roadmap features prematurely.

## Flat Vault Principle

Notes must exist only as `vault/*.md`. Never use directories to classify knowledge, recursively discover nested Markdown notes, or expose paths as note identities. Subdirectories are allowed only for auxiliary data such as `.git`, a future `.mind`, or possible attachments.

Represent organization logically through metadata and links. Keep `tags`, `areas`, `projects`, `type`, and `status` semantically distinct; do not encode directory trees in tags such as `programming/dotnet/ef-core`. Future hierarchical views must be generated from queries rather than physical folders.

Keep filenames human-readable using title slugs and add a short UUIDv7 fragment on collision. Any future SQLite index must remain disposable and reconstructible from Markdown—it must never become the source of truth.

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

### File and Namespace Organization

Declare exactly one top-level C# type per source file. This applies to classes, records, interfaces, structs, and enums. Name the file after the type, for example `NoteService.cs`, `NoteSummary.cs`, or `INoteFileStore.cs`. Do not group unrelated models, abstractions, services, or utility functions in a single file. Methods should remain with the type that owns their responsibility; extract a new focused type when responsibilities diverge. Generated code and the CLI top-level entry point are the only exceptions unless the repository explicitly requires another.

Namespaces must mirror the path below the project directory. Use the project root namespace followed by every relative folder segment:

```text
src/MindVault.Application/Notes/NoteService.cs
→ MindVault.Application.Notes

src/MindVault.Application/Abstractions/Notes/INoteFileStore.cs
→ MindVault.Application.Abstractions.Notes
```

When moving a file, update its namespace and all affected `using` directives. Organize new Application code under feature folders such as `Configuration`, `Notes`, or `Diagnostics`; place outbound contracts under the matching `Abstractions/<Feature>` folder.

## Testing Guidelines

Name tests after observable behavior, such as `Create_fails_without_vault`. Test invariants and failure paths rather than trivial properties. Application tests should use focused fakes; filesystem tests must use temporary directories and cover rejection or exclusion of nested notes. Tests must not depend on Git, internet access, user configuration, or an installed editor.

## Commit & Pull Request Guidelines

No Git history is available in this workspace. Use short, imperative commit subjects, preferably Conventional Commits, for example `feat(cli): add note listing` or `fix(storage): reject vault traversal`.

Pull requests should explain the behavior change, identify affected commands, list verification commands, and note compatibility or security implications. Include terminal output for CLI presentation changes; screenshots are unnecessary unless a future visual interface is introduced.

## Security & Configuration

Never accept arbitrary paths for note operations. Normalize paths, enforce direct vault containment, reject escaping links, create files atomically, and never overwrite notes silently. Do not commit personal vault paths or real configuration files.
