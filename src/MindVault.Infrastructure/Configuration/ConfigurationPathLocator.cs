namespace MindVault.Infrastructure.Configuration;

public static class ConfigurationPathLocator
{
    public static string GetPath()
    {
        var overridePath =
            Environment.GetEnvironmentVariable("MINDVAULT_CONFIG_PATH");
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            return Path.GetFullPath(overridePath);
        }

        if (OperatingSystem.IsWindows())
        {
            var root = Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrWhiteSpace(root))
            {
                throw new InvalidOperationException(
                    "Não foi possível localizar o diretório de configuração do usuário.");
            }

            return Path.Combine(root, "MindVault", "config.json");
        }

        var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        var basePath = string.IsNullOrWhiteSpace(xdg)
            ? Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.UserProfile),
                ".config")
            : xdg;

        return Path.Combine(basePath, "mindvault", "config.json");
    }
}
