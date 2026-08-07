using Xunit;
using MindVault.Application;
using MindVault.Domain;
namespace MindVault.Application.Tests;
public sealed class ApplicationTests
{
    [Fact] public async Task Create_fails_without_vault()
    {
        var f = new Fixture(UserConfiguration.Empty);
        var result = await f.Service.CreateAsync("Nota", false, default);
        Assert.Equal(ErrorKind.Configuration, result.Error!.Kind);
    }
    [Fact] public async Task Collision_gets_id_suffix()
    {
        var f = new Fixture();
        f.Files.Add(new("minha-nota.md", @"C:\vault\minha-nota.md", default, "invalid"));
        var result = await f.Service.CreateAsync("Minha nota", false, default);
        Assert.True(result.IsSuccess);
        Assert.StartsWith("minha-nota-", result.Value!.FileName);
        Assert.Equal(2, f.Files.Count);
    }
    [Fact] public async Task Exact_id_wins()
    {
        var f = new Fixture();
        var id = NoteId.New(TimeProvider.System);
        f.Add("target.md", id, "Target");
        f.Add("other.md", NoteId.New(TimeProvider.System), "Target extra");
        var result = await f.Service.OpenAsync(id.ToString(), default);
        Assert.Equal("target.md", result.Value!.FileName);
        Assert.Equal(@"C:\vault\target.md", f.Opened);
    }
    [Fact] public async Task Partial_title_can_be_ambiguous()
    {
        var f = new Fixture();
        f.Add("one.md", NoteId.New(TimeProvider.System), "Price Watcher");
        f.Add("two.md", NoteId.New(TimeProvider.System), "Price Watcher Architecture");
        var result = await f.Service.OpenAsync("Price", default);
        Assert.Equal(ErrorKind.Ambiguous, result.Error!.Kind);
        Assert.Equal(2, result.Error.Details!.Count);
    }
    [Fact] public async Task Delete_requires_confirmation()
    {
        var f = new Fixture();
        f.Add("note.md", NoteId.New(TimeProvider.System), "Note");
        Assert.True((await f.Service.DeleteAsync("Note", false, default)).Value!.RequiresConfirmation);
        Assert.Single(f.Files);
        Assert.True((await f.Service.DeleteAsync("Note", true, default)).IsSuccess);
        Assert.Empty(f.Files);
    }
    private sealed class Fixture
    {
        public Fixture(UserConfiguration? config = null)
        {
            Config = config ?? new(@"C:\vault", new("editor", []));
            Service = new(new ConfigPort(this), new FilesPort(this), new DocumentPort(), new Names(), new EditorPort(this), new DirectoryPort(), TimeProvider.System);
        }
        public UserConfiguration Config { get; }
        public List<StoredFile> Files { get; } = [];
        public string? Opened { get; set; }
        public NoteService Service { get; }
        public void Add(string file, NoteId id, string title) => Files.Add(new(file, $@"C:\vault\{file}", default, $"{id}|{title}"));
        private sealed class ConfigPort(Fixture f) : IConfigurationStore
        {
            public string ConfigurationPath => "config.json";
            public Task<ConfigurationRead> ReadAsync(CancellationToken _) => Task.FromResult(new ConfigurationRead(ConfigurationStatus.Valid, f.Config, ConfigurationPath));
            public Task WriteAsync(UserConfiguration c, CancellationToken _) => Task.CompletedTask;
        }
        private sealed class DirectoryPort : IVaultDirectory
        {
            public string Normalize(string p) => p; public bool Exists(string p) => true;
            public Task CreateAsync(string p, CancellationToken t) => Task.CompletedTask;
            public Task<Result<bool>> CanReadAsync(string p, CancellationToken t) => Task.FromResult(Result<bool>.Success(true));
            public Task<Result<bool>> CanWriteAsync(string p, CancellationToken t) => Task.FromResult(Result<bool>.Success(true));
        }
        private sealed class FilesPort(Fixture f) : INoteFileStore
        {
            public Task<IReadOnlyList<StoredFile>> ListAsync(string v, CancellationToken t) => Task.FromResult<IReadOnlyList<StoredFile>>(f.Files.ToArray());
            public Task<bool> ExistsAsync(string v, string n, CancellationToken t) => Task.FromResult(f.Files.Any(x => x.FileName == n));
            public Task<Result<string>> CreateAsync(string v, string n, string c, CancellationToken t) { var p=$@"C:\vault\{n}"; f.Files.Add(new(n,p,default,c)); return Task.FromResult(Result<string>.Success(p)); }
            public Task<Result<bool>> DeleteAsync(string v, string p, CancellationToken t) { f.Files.RemoveAll(x => x.FullPath == p); return Task.FromResult(Result<bool>.Success(true)); }
        }
        private sealed class DocumentPort : INoteDocumentSerializer
        {
            public string Serialize(Note n, string b) => $"{n.Id}|{n.Title.Value}";
            public ParsedDocument Deserialize(string c, string f)
            {
                var p=c.Split('|',2); if(p.Length!=2)return new(null,"invalid");
                var id=NoteId.Parse(p[0]); var title=NoteTitle.Create(p[1]);
                return id.IsSuccess&&title.IsSuccess ? new(new(id.Value!,title.Value!,f,default,default),null) : new(null,"invalid");
            }
        }
        private sealed class Names : IFileNameGenerator { public Result<string> CreateSlug(string t) => Result<string>.Success(t.ToLowerInvariant().Replace(' ','-')); }
        private sealed class EditorPort(Fixture f) : IExternalEditor
        {
            public bool CanLocate(string e)=>true;
            public Task<Result<int>> OpenAsync(EditorSettings e,string p,CancellationToken t){f.Opened=p;return Task.FromResult(Result<int>.Success(0));}
        }
    }
}
