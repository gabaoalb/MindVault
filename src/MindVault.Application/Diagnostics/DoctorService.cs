using MindVault.Application.Abstractions.Configuration;
using MindVault.Application.Abstractions.Editors;
using MindVault.Application.Configuration;

namespace MindVault.Application.Diagnostics;

public sealed class DoctorService(IConfigurationStore configurationStore,
    IVaultDirectory directory,
    IExternalEditor editor)
{
    public async Task<DoctorReport> RunAsync(CancellationToken cancellationToken)
    {
        var checks = new List<DiagnosticCheck>();
        var read = await configurationStore.ReadAsync(cancellationToken);

        checks.Add(new DiagnosticCheck(
            read.Status switch
            {
                ConfigurationStatusEnum.Missing =>
                    $"Arquivo de configuração não encontrado: {read.Path}",
                ConfigurationStatusEnum.Valid =>
                    $"Arquivo de configuração válido: {read.Path}",
                _ => read.Error ?? "Configuração inválida"
            },
            read.Status == ConfigurationStatusEnum.Valid,
            true));

        if (read.Status != ConfigurationStatusEnum.Valid)
        {
            return new DoctorReport(checks);
        }

        var configuration = read.Configuration!;
        var hasVault = !string.IsNullOrWhiteSpace(configuration.VaultPath);
        checks.Add(new DiagnosticCheck(
            hasVault
                ? $"Vault configurado: {configuration.VaultPath}"
                : "Vault não configurado",
            hasVault,
            true));

        if (hasVault)
        {
            var exists = directory.Exists(configuration.VaultPath!);
            checks.Add(new DiagnosticCheck(
                exists
                    ? "Diretório do vault existente"
                    : "O vault configurado não existe",
                exists,
                true));

            if (exists)
            {
                var readAccess = await directory.CanReadAsync(
                    configuration.VaultPath!,
                    cancellationToken);
                var writeAccess = await directory.CanWriteAsync(
                    configuration.VaultPath!,
                    cancellationToken);

                checks.Add(new DiagnosticCheck(
                    readAccess.IsSuccess
                        ? "Vault acessível para leitura"
                        : readAccess.Error!.Message,
                    readAccess.IsSuccess,
                    true));
                checks.Add(new DiagnosticCheck(
                    writeAccess.IsSuccess
                        ? "Vault acessível para escrita"
                        : writeAccess.Error!.Message,
                    writeAccess.IsSuccess,
                    true));
            }
        }

        var hasEditor = configuration.Editor is not null;
        checks.Add(new DiagnosticCheck(
            hasEditor
                ? $"Editor configurado: {configuration.Editor!.Executable}"
                : "Editor não configurado",
            hasEditor,
            true));

        if (hasEditor)
        {
            var found = editor.CanLocate(configuration.Editor!.Executable);
            checks.Add(new DiagnosticCheck(
                found
                    ? "Executável do editor localizado"
                    : $"Executável do editor não localizado: {configuration.Editor.Executable}",
                found,
                true));
        }

        return new DoctorReport(checks);
    }
}
