using MindVault.Application.Abstractions.Configuration;
using MindVault.Application.Abstractions.Editors;
using MindVault.Domain.Common;

namespace MindVault.Application.Configuration;

public sealed class ConfigurationService(IConfigurationStore store,
    IVaultDirectory directory,
    IEditorCommandParser editorParser)
{
    public async Task<Result<UserConfiguration>> SetVaultAsync(string path,
        bool create,
        CancellationToken cancellationToken)
    {
        string normalized;
        try
        {
            normalized = directory.Normalize(path);
        }
        catch (Exception exception)
            when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return Result<UserConfiguration>.Failure(
                new AppError(
                    ErrorKind.InvalidInput,
                    $"Caminho do vault inválido: {exception.Message}"));
        }

        if (!directory.Exists(normalized))
        {
            if (!create)
            {
                return Result<UserConfiguration>.Failure(
                    new AppError(
                        ErrorKind.NotFound,
                        $"O diretório '{normalized}' não existe."));
            }

            await directory.CreateAsync(normalized, cancellationToken);
        }

        var current = await ReadForUpdateAsync(cancellationToken);
        if (!current.IsSuccess)
        {
            return Result<UserConfiguration>.Failure(current.Error!);
        }

        var updated = current.Value! with { VaultPath = normalized };
        await store.WriteAsync(updated, cancellationToken);
        return Result<UserConfiguration>.Success(updated);
    }

    public async Task<Result<UserConfiguration>> SetEditorAsync(
        string command,
        CancellationToken cancellationToken)
    {
        var parsed = editorParser.Parse(command);
        if (!parsed.IsSuccess)
        {
            return Result<UserConfiguration>.Failure(parsed.Error!);
        }

        var current = await ReadForUpdateAsync(cancellationToken);
        if (!current.IsSuccess)
        {
            return Result<UserConfiguration>.Failure(current.Error!);
        }

        var updated = current.Value! with { Editor = parsed.Value };
        await store.WriteAsync(updated, cancellationToken);
        return Result<UserConfiguration>.Success(updated);
    }

    public async Task<Result<ConfigurationRead>> ShowAsync(
        CancellationToken cancellationToken)
    {
        var read = await store.ReadAsync(cancellationToken);
        return read.Status switch
        {
            ConfigurationStatusEnum.Valid => Result<ConfigurationRead>.Success(read),
            ConfigurationStatusEnum.Missing => Result<ConfigurationRead>.Failure(
                new AppError(
                    ErrorKind.Configuration,
                    $"Arquivo de configuração não encontrado: {read.Path}")),
            _ => Result<ConfigurationRead>.Failure(
                new AppError(
                    ErrorKind.Configuration,
                    read.Error ?? "A configuração é inválida."))
        };
    }

    private async Task<Result<UserConfiguration>> ReadForUpdateAsync(
        CancellationToken cancellationToken)
    {
        var read = await store.ReadAsync(cancellationToken);
        return read.Status switch
        {
            ConfigurationStatusEnum.Missing => Result<UserConfiguration>.Success(
                UserConfiguration.Empty),
            ConfigurationStatusEnum.Valid => Result<UserConfiguration>.Success(
                read.Configuration!),
            _ => Result<UserConfiguration>.Failure(
                new AppError(
                    ErrorKind.Configuration,
                    read.Error ?? "A configuração é inválida."))
        };
    }
}
