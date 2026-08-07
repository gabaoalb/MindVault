using System.Text.Json;
using MindVault.Application;
using MindVault.Domain;

namespace MindVault.Infrastructure;

public static class ConfigurationPathLocator
{
    public static string GetPath()
    {
        var overridePath = Environment.GetEnvironmentVariable("MINDVAULT_CONFIG_PATH");
        if (!string.IsNullOrWhiteSpace(overridePath)) return Path.GetFullPath(overridePath);
        if (OperatingSystem.IsWindows())
        {
            var root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrWhiteSpace(root)) throw new InvalidOperationException("Não foi possível localizar o diretório de configuração do usuário.");
            return Path.Combine(root, "MindVault", "config.json");
        }
        var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        var basePath = string.IsNullOrWhiteSpace(xdg)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config")
            : xdg;
        return Path.Combine(basePath, "mindvault", "config.json");
    }
}

public sealed class JsonConfigurationStore(string configurationPath) : IConfigurationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
    public string ConfigurationPath { get; } = Path.GetFullPath(configurationPath);

    public async Task<ConfigurationRead> ReadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(ConfigurationPath)) return new(ConfigurationStatus.Missing, null, ConfigurationPath);
        try
        {
            await using var stream = new FileStream(ConfigurationPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
            var dto = await JsonSerializer.DeserializeAsync<ConfigurationDto>(stream, JsonOptions, cancellationToken);
            if (dto is null) return new(ConfigurationStatus.Invalid, null, ConfigurationPath, "O arquivo de configuração está vazio.");
            EditorSettings? editor = null;
            if (!string.IsNullOrWhiteSpace(dto.Editor))
                editor = new(dto.Editor, dto.EditorArguments ?? []);
            return new(ConfigurationStatus.Valid, new UserConfiguration(dto.VaultPath, editor), ConfigurationPath);
        }
        catch (JsonException ex) { return new(ConfigurationStatus.Invalid, null, ConfigurationPath, $"JSON de configuração inválido: {ex.Message}"); }
        catch (IOException ex) { return new(ConfigurationStatus.Invalid, null, ConfigurationPath, $"Não foi possível ler a configuração: {ex.Message}"); }
        catch (UnauthorizedAccessException ex) { return new(ConfigurationStatus.Invalid, null, ConfigurationPath, $"Sem permissão para ler a configuração: {ex.Message}"); }
    }

    public async Task WriteAsync(UserConfiguration configuration, CancellationToken cancellationToken)
    {
        var parent = Path.GetDirectoryName(ConfigurationPath)!;
        Directory.CreateDirectory(parent);
        var temporaryPath = Path.Combine(parent, $".config.{Guid.NewGuid():N}.tmp");
        try
        {
            var dto = new ConfigurationDto(configuration.VaultPath, configuration.Editor?.Executable, configuration.Editor?.Arguments.ToArray());
            await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, true))
            {
                await JsonSerializer.SerializeAsync(stream, dto, JsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }
            File.Move(temporaryPath, ConfigurationPath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private sealed record ConfigurationDto(string? VaultPath, string? Editor, string[]? EditorArguments);
}

public sealed class VaultDirectory : IVaultDirectory
{
    public string Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("O caminho não pode estar vazio.", nameof(path));
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim())));
    }
    public bool Exists(string path) => Directory.Exists(path);
    public Task CreateAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(path);
        return Task.CompletedTask;
    }
    public Task<Result<bool>> CanReadAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            _ = Directory.EnumerateFileSystemEntries(path).Take(1).ToArray();
            return Task.FromResult(Result<bool>.Success(true));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { return Task.FromResult(Result<bool>.Failure(new AppError(ErrorKind.Configuration, $"Sem acesso de leitura ao vault: {ex.Message}"))); }
    }
    public async Task<Result<bool>> CanWriteAsync(string path, CancellationToken cancellationToken)
    {
        var probe = Path.Combine(path, $".mind-write-test-{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllTextAsync(probe, string.Empty, cancellationToken);
            File.Delete(probe);
            return Result<bool>.Success(true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (File.Exists(probe)) File.Delete(probe);
            return Result<bool>.Failure(new AppError(ErrorKind.Configuration, $"Sem acesso de escrita ao vault: {ex.Message}"));
        }
    }
}
