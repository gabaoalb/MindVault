using Xunit;
using MindVault.Application;
using MindVault.Domain.Notes;
using MindVault.Infrastructure.Configuration;
using MindVault.Infrastructure.Editors;
using MindVault.Infrastructure.Notes;
using MindVault.Application.Configuration;
namespace MindVault.Infrastructure.Tests;

public sealed class InfrastructureTests
{
    [Theory]
    [InlineData("Estudos de eletrônica", "estudos-de-eletronica")]
    [InlineData("API / C#: desenho!", "api-c-desenho")]
    public void Slug_is_safe(string title, string expected) => Assert.Equal(expected, new SlugFileNameGenerator().CreateSlug(title).Value);
    [Fact]
    public void Yaml_round_trips()
    {
        var serializer = new YamlNoteDocumentSerializer();
        var note = new Note(NoteId.New(TimeProvider.System), NoteTitle.Create("Arquitetura: Price Watcher").Value!, "arquitetura.md", DateTimeOffset.Parse("2026-08-04T22:00:00-03:00"), DateTimeOffset.Parse("2026-08-04T22:00:00-03:00"));
        var parsed = serializer.Deserialize(serializer.Serialize(note, "# Arquitetura\n"), note.FileName);
        Assert.True(parsed.IsValid, parsed.Error);
        Assert.Equal(note.Id, parsed.Note!.Id);
        Assert.Equal(note.Title, parsed.Note.Title);
    }
    [Fact]
    public void Invalid_frontmatter_is_reported()
    {
        var parsed = new YamlNoteDocumentSerializer().Deserialize("---\nid: nope\n---\n", "bad.md");
        Assert.False(parsed.IsValid);
        Assert.Contains("inválido", parsed.Error!, StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public async Task Traversal_is_rejected()
    {
        using var temp = new TempDirectory();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new PhysicalNoteFileStore().CreateAsync(temp.Path, "../outside.md", "x", default));
    }
    [Fact]
    public async Task Configuration_round_trips()
    {
        using var temp = new TempDirectory();
        var store = new JsonConfigurationStore(System.IO.Path.Combine(temp.Path, "config", "config.json"));
        var config = new UserConfiguration(System.IO.Path.Combine(temp.Path, "vault"), new("code", ["--wait"]));
        await store.WriteAsync(config, default);
        var read = await store.ReadAsync(default);
        Assert.Equal(ConfigurationStatusEnum.Valid, read.Status);
        Assert.Equal(config.VaultPath, read.Configuration!.VaultPath);
        Assert.Equal("--wait", Assert.Single(read.Configuration.Editor!.Arguments));
    }
    [Fact]
    public void Editor_command_is_tokenized()
    {
        var result = new EditorCommandParser().Parse("code --wait \"--profile name\"");
        Assert.Equal("code", result.Value!.Executable);
        Assert.Equal(["--wait", "--profile name"], result.Value.Arguments);
    }
    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory() { Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"mindvault-tests-{Guid.NewGuid():N}"); Directory.CreateDirectory(Path); }
        public string Path { get; }
        public void Dispose() { if (Directory.Exists(Path)) Directory.Delete(Path, true); }
    }
}
