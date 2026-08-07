using MindVault.Application.Abstractions.Configuration;
using MindVault.Application.Abstractions.Editors;
using MindVault.Application.Abstractions.Notes;
using MindVault.Application.Configuration;
using MindVault.Domain.Common;
using MindVault.Domain.Notes;

namespace MindVault.Application.Notes;

public sealed class NoteService(
    IConfigurationStore configurationStore,
    INoteFileStore fileStore,
    INoteDocumentSerializer serializer,
    IFileNameGenerator fileNameGenerator,
    IExternalEditor editor,
    IVaultDirectory vaultDirectory,
    TimeProvider timeProvider)
{
    public async Task<Result<Note>> CreateAsync(
        string titleText,
        bool openEditor,
        CancellationToken cancellationToken)
    {
        var context = await GetContextAsync(openEditor, cancellationToken);
        if (!context.IsSuccess)
        {
            return Result<Note>.Failure(context.Error!);
        }

        var title = NoteTitle.Create(titleText);
        if (!title.IsSuccess)
        {
            return Result<Note>.Failure(title.Error!);
        }

        var slug = fileNameGenerator.CreateSlug(title.Value!.Value);
        if (!slug.IsSuccess)
        {
            return Result<Note>.Failure(slug.Error!);
        }

        var now = timeProvider.GetLocalNow();
        var id = NoteId.New(timeProvider);
        var fileName = $"{slug.Value}.md";

        if (await fileStore.ExistsAsync(
                context.Value!.VaultPath!,
                fileName,
                cancellationToken))
        {
            fileName = $"{slug.Value}-{id.Value.ToString("N")[^6..]}.md";
        }

        var note = new Note(id, title.Value!, fileName, now, now);
        var content = serializer.Serialize(note, $"# {note.Title.Value}\n");
        var created = await fileStore.CreateAsync(
            context.Value.VaultPath!,
            fileName,
            content,
            cancellationToken);

        if (!created.IsSuccess)
        {
            return Result<Note>.Failure(created.Error!);
        }

        if (openEditor)
        {
            var opened = await editor.OpenAsync(
                context.Value.Editor!,
                created.Value!,
                cancellationToken);

            if (!opened.IsSuccess)
            {
                return Result<Note>.Failure(
                    new AppError(
                        opened.Error!.Kind,
                        $"A nota foi criada em '{created.Value}', mas não foi possível abrir o editor: {opened.Error.Message}"));
            }
        }

        return Result<Note>.Success(note);
    }

    public async Task<Result<IReadOnlyList<NoteSummary>>> ListAsync(
        CancellationToken cancellationToken)
    {
        var context = await GetContextAsync(false, cancellationToken);
        if (!context.IsSuccess)
        {
            return Result<IReadOnlyList<NoteSummary>>.Failure(context.Error!);
        }

        var notes = await ReadSummariesAsync(
            context.Value!.VaultPath!,
            cancellationToken);

        return Result<IReadOnlyList<NoteSummary>>.Success(
            notes
                .OrderBy(note => note.Title, StringComparer.CurrentCultureIgnoreCase)
                .ToArray());
    }

    public async Task<Result<NoteSummary>> OpenAsync(
        string query,
        CancellationToken cancellationToken)
    {
        var context = await GetContextAsync(true, cancellationToken);
        if (!context.IsSuccess)
        {
            return Result<NoteSummary>.Failure(context.Error!);
        }

        var resolved = await ResolveAsync(
            context.Value!.VaultPath!,
            query,
            cancellationToken);

        if (!resolved.IsSuccess)
        {
            return resolved;
        }

        var opened = await editor.OpenAsync(
            context.Value.Editor!,
            resolved.Value!.FullPath,
            cancellationToken);

        return opened.IsSuccess
            ? resolved
            : Result<NoteSummary>.Failure(opened.Error!);
    }

    public async Task<Result<DeleteNoteOutcome>> DeleteAsync(
        string query,
        bool confirmed,
        CancellationToken cancellationToken)
    {
        var context = await GetContextAsync(false, cancellationToken);
        if (!context.IsSuccess)
        {
            return Result<DeleteNoteOutcome>.Failure(context.Error!);
        }

        var resolved = await ResolveAsync(
            context.Value!.VaultPath!,
            query,
            cancellationToken);

        if (!resolved.IsSuccess)
        {
            return Result<DeleteNoteOutcome>.Failure(resolved.Error!);
        }

        if (!confirmed)
        {
            return Result<DeleteNoteOutcome>.Success(
                new DeleteNoteOutcome(resolved.Value!, true));
        }

        var deleted = await fileStore.DeleteAsync(
            context.Value.VaultPath!,
            resolved.Value!.FullPath,
            cancellationToken);

        return deleted.IsSuccess
            ? Result<DeleteNoteOutcome>.Success(
                new DeleteNoteOutcome(resolved.Value, false))
            : Result<DeleteNoteOutcome>.Failure(deleted.Error!);
    }

    private async Task<Result<UserConfiguration>> GetContextAsync(
        bool requireEditor,
        CancellationToken cancellationToken)
    {
        var read = await configurationStore.ReadAsync(cancellationToken);
        if (read.Status != ConfigurationStatusEnum.Valid)
        {
            return Result<UserConfiguration>.Failure(
                new AppError(
                    ErrorKind.Configuration,
                    read.Error ?? "Configure o vault antes de executar este comando."));
        }

        if (string.IsNullOrWhiteSpace(read.Configuration!.VaultPath))
        {
            return Result<UserConfiguration>.Failure(
                new AppError(
                    ErrorKind.Configuration,
                    "Nenhum vault foi configurado."));
        }

        if (!vaultDirectory.Exists(read.Configuration.VaultPath))
        {
            return Result<UserConfiguration>.Failure(
                new AppError(
                    ErrorKind.Configuration,
                    $"O vault configurado não existe: {read.Configuration.VaultPath}"));
        }

        if (requireEditor && read.Configuration.Editor is null)
        {
            return Result<UserConfiguration>.Failure(
                new AppError(
                    ErrorKind.Configuration,
                    "Nenhum editor foi configurado."));
        }

        return Result<UserConfiguration>.Success(read.Configuration);
    }

    private async Task<IReadOnlyList<NoteSummary>> ReadSummariesAsync(
        string vaultPath,
        CancellationToken cancellationToken)
    {
        var files = await fileStore.ListAsync(vaultPath, cancellationToken);
        return files
            .Select(file =>
            {
                var parsed = serializer.Deserialize(file.Content, file.FileName);
                return parsed.Note is { } note
                    ? new NoteSummary(
                        note.Id.ToString(),
                        note.Title.Value,
                        file.FileName,
                        file.FullPath,
                        file.ModifiedAt,
                        false,
                        null)
                    : new NoteSummary(
                        null,
                        Path.GetFileNameWithoutExtension(file.FileName),
                        file.FileName,
                        file.FullPath,
                        file.ModifiedAt,
                        true,
                        parsed.Error);
            })
            .ToArray();
    }

    private async Task<Result<NoteSummary>> ResolveAsync(
        string vaultPath,
        string query,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Result<NoteSummary>.Failure(
                new AppError(
                    ErrorKind.InvalidInput,
                    "Informe uma nota para localizar."));
        }

        var notes = await ReadSummariesAsync(vaultPath, cancellationToken);
        var normalizedQuery = query.Trim();
        IEnumerable<NoteSummary>[] levels =
        [
            notes.Where(note =>
                note.Id is not null &&
                string.Equals(
                    note.Id,
                    normalizedQuery,
                    StringComparison.OrdinalIgnoreCase)),
            notes.Where(note =>
                string.Equals(
                    note.FileName,
                    normalizedQuery,
                    StringComparison.OrdinalIgnoreCase)),
            notes.Where(note =>
                !note.HasInvalidMetadata &&
                string.Equals(
                    note.Title,
                    normalizedQuery,
                    StringComparison.CurrentCultureIgnoreCase)),
            notes.Where(note =>
                !note.HasInvalidMetadata &&
                note.Title.Contains(
                    normalizedQuery,
                    StringComparison.CurrentCultureIgnoreCase)),
            notes.Where(note =>
                note.FileName.Contains(
                    normalizedQuery,
                    StringComparison.OrdinalIgnoreCase))
        ];

        foreach (var level in levels)
        {
            var matches = level.ToArray();
            if (matches.Length == 1)
            {
                return Result<NoteSummary>.Success(matches[0]);
            }

            if (matches.Length > 1)
            {
                return Result<NoteSummary>.Failure(
                    new AppError(
                        ErrorKind.Ambiguous,
                        "Mais de uma nota corresponde à consulta.",
                        matches
                            .Select(note => $"{note.Title} ({note.FileName})")
                            .ToArray()));
            }
        }

        return Result<NoteSummary>.Failure(
            new AppError(
                ErrorKind.NotFound,
                $"Nenhuma nota encontrada para '{normalizedQuery}'."));
    }
}
