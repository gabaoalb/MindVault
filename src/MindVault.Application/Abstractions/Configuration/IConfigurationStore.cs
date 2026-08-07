using MindVault.Application.Configuration;

namespace MindVault.Application.Abstractions.Configuration;

public interface IConfigurationStore
{
    string ConfigurationPath { get; }

    Task<ConfigurationRead> ReadAsync(CancellationToken cancellationToken);

    Task WriteAsync(UserConfiguration configuration, CancellationToken cancellationToken);
}
