using System.Text.Json;
using MindVault.Application.Abstractions.Configuration;
using MindVault.Application.Configuration;

namespace MindVault.Infrastructure.Configuration;

public sealed class JsonConfigurationStore(string configurationPath)
    : IConfigurationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public string ConfigurationPath { get; } =
        Path.GetFullPath(configurationPath);

    public async Task<ConfigurationRead> ReadAsync(
        CancellationToken cancellationToken)
    {
        if (!File.Exists(ConfigurationPath))
        {
            return new(
                ConfigurationStatusEnum.Missing,
                null,
                ConfigurationPath);
        }

        try
        {
            await using var stream = new FileStream(
                ConfigurationPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                4096,
                true);

            var dto = await JsonSerializer.DeserializeAsync<ConfigurationDto>(
                stream,
                JsonOptions,
                cancellationToken);

            if (dto is null)
            {
                return new(
                    ConfigurationStatusEnum.Invalid,
                    null,
                    ConfigurationPath,
                    "O arquivo de configuração está vazio.");
            }

            EditorSettings? editor = null;
            if (!string.IsNullOrWhiteSpace(dto.Editor))
            {
                editor = new(dto.Editor, dto.EditorArguments ?? []);
            }

            return new(
                ConfigurationStatusEnum.Valid,
                new UserConfiguration(dto.VaultPath, editor),
                ConfigurationPath);
        }
        catch (JsonException exception)
        {
            return new(
                ConfigurationStatusEnum.Invalid,
                null,
                ConfigurationPath,
                $"JSON de configuração inválido: {exception.Message}");
        }
        catch (IOException exception)
        {
            return new(
                ConfigurationStatusEnum.Invalid,
                null,
                ConfigurationPath,
                $"Não foi possível ler a configuração: {exception.Message}");
        }
        catch (UnauthorizedAccessException exception)
        {
            return new(
                ConfigurationStatusEnum.Invalid,
                null,
                ConfigurationPath,
                $"Sem permissão para ler a configuração: {exception.Message}");
        }
    }

    public async Task WriteAsync(
        UserConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var parent = Path.GetDirectoryName(ConfigurationPath)!;
        Directory.CreateDirectory(parent);
        var temporaryPath = Path.Combine(
            parent,
            $".config.{Guid.NewGuid():N}.tmp");

        try
        {
            var dto = new ConfigurationDto(
                configuration.VaultPath,
                configuration.Editor?.Executable,
                configuration.Editor?.Arguments.ToArray());

            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                4096,
                true))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    dto,
                    JsonOptions,
                    cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, ConfigurationPath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
