using System.ComponentModel;
using System.Diagnostics;
using MindVault.Application.Abstractions.Editors;
using MindVault.Application.Configuration;
using MindVault.Domain.Common;

namespace MindVault.Infrastructure.Editors;

public sealed class ProcessExternalEditor : IExternalEditor
{
    public async Task<Result<int>> OpenAsync(
        EditorSettings editor,
        string filePath,
        CancellationToken cancellationToken)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = editor.Executable,
                UseShellExecute = false
            };

            foreach (var argument in editor.Arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            startInfo.ArgumentList.Add(filePath);
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return Result<int>.Failure(
                    new AppError(
                        ErrorKind.ExternalTool,
                        "O processo do editor não pôde ser iniciado."));
            }

            await process.WaitForExitAsync(cancellationToken);
            return process.ExitCode == 0
                ? Result<int>.Success(0)
                : Result<int>.Failure(
                    new AppError(
                        ErrorKind.ExternalTool,
                        $"O editor terminou com o código {process.ExitCode}."));
        }
        catch (Win32Exception exception)
        {
            return Result<int>.Failure(
                new AppError(
                    ErrorKind.ExternalTool,
                    $"Editor não encontrado: {editor.Executable}. {exception.Message}"));
        }
        catch (FileNotFoundException exception)
        {
            return Result<int>.Failure(
                new AppError(
                    ErrorKind.ExternalTool,
                    $"Editor não encontrado: {exception.FileName ?? editor.Executable}."));
        }
    }

    public bool CanLocate(string executable)
    {
        if (Path.IsPathFullyQualified(executable) ||
            executable.Contains(Path.DirectorySeparatorChar) ||
            executable.Contains(Path.AltDirectorySeparatorChar))
        {
            return File.Exists(Path.GetFullPath(executable));
        }

        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var extensions = OperatingSystem.IsWindows()
            ? (Environment.GetEnvironmentVariable("PATHEXT") ??
               ".EXE;.CMD;.BAT;.COM")
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
            : [string.Empty];

        foreach (var directory in path.Split(
            Path.PathSeparator,
            StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries))
        {
            foreach (var extension in extensions
                .Prepend(string.Empty)
                .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var candidate = executable.EndsWith(
                    extension,
                    StringComparison.OrdinalIgnoreCase)
                    ? executable
                    : executable + extension;

                if (File.Exists(Path.Combine(directory, candidate)))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
