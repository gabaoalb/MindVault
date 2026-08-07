using System.ComponentModel;
using System.Diagnostics;
using MindVault.Application;
using MindVault.Domain;

namespace MindVault.Infrastructure;

public sealed class EditorCommandParser : IEditorCommandParser
{
    public Result<EditorSettings> Parse(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return Result<EditorSettings>.Failure(new AppError(ErrorKind.InvalidInput, "O comando do editor não pode estar vazio."));
        var tokens = new List<string>();
        var current = new System.Text.StringBuilder();
        char? quote = null;
        var escaped = false;
        foreach (var ch in command.Trim())
        {
            if (escaped) { current.Append(ch); escaped = false; continue; }
            if (ch == '\\' && quote is not null) { escaped = true; continue; }
            if (ch is '\'' or '"')
            {
                if (quote == ch) quote = null;
                else if (quote is null) quote = ch;
                else current.Append(ch);
                continue;
            }
            if (char.IsWhiteSpace(ch) && quote is null)
            {
                if (current.Length > 0) { tokens.Add(current.ToString()); current.Clear(); }
            }
            else current.Append(ch);
        }
        if (escaped) current.Append('\\');
        if (quote is not null)
            return Result<EditorSettings>.Failure(new AppError(ErrorKind.InvalidInput, "O comando do editor contém aspas não fechadas."));
        if (current.Length > 0) tokens.Add(current.ToString());
        if (tokens.Count == 0)
            return Result<EditorSettings>.Failure(new AppError(ErrorKind.InvalidInput, "O executável do editor não foi informado."));
        return Result<EditorSettings>.Success(new(tokens[0], tokens.Skip(1).ToArray()));
    }
}

public sealed class ProcessExternalEditor : IExternalEditor
{
    public async Task<Result<int>> OpenAsync(EditorSettings editor, string filePath, CancellationToken cancellationToken)
    {
        try
        {
            var startInfo = new ProcessStartInfo { FileName = editor.Executable, UseShellExecute = false };
            foreach (var argument in editor.Arguments) startInfo.ArgumentList.Add(argument);
            startInfo.ArgumentList.Add(filePath);
            using var process = Process.Start(startInfo);
            if (process is null) return Result<int>.Failure(new AppError(ErrorKind.ExternalTool, "O processo do editor não pôde ser iniciado."));
            await process.WaitForExitAsync(cancellationToken);
            return process.ExitCode == 0
                ? Result<int>.Success(0)
                : Result<int>.Failure(new AppError(ErrorKind.ExternalTool, $"O editor terminou com o código {process.ExitCode}."));
        }
        catch (Win32Exception ex)
        { return Result<int>.Failure(new AppError(ErrorKind.ExternalTool, $"Editor não encontrado: {editor.Executable}. {ex.Message}")); }
        catch (FileNotFoundException ex)
        { return Result<int>.Failure(new AppError(ErrorKind.ExternalTool, $"Editor não encontrado: {ex.FileName ?? editor.Executable}.")); }
    }

    public bool CanLocate(string executable)
    {
        if (Path.IsPathFullyQualified(executable) || executable.Contains(Path.DirectorySeparatorChar) || executable.Contains(Path.AltDirectorySeparatorChar))
            return File.Exists(Path.GetFullPath(executable));
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path)) return false;
        var extensions = OperatingSystem.IsWindows()
            ? (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT;.COM").Split(';', StringSplitOptions.RemoveEmptyEntries)
            : [string.Empty];
        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            foreach (var extension in extensions.Prepend(string.Empty).Distinct(StringComparer.OrdinalIgnoreCase))
                if (File.Exists(Path.Combine(directory, executable.EndsWith(extension, StringComparison.OrdinalIgnoreCase) ? executable : executable + extension))) return true;
        return false;
    }
}
