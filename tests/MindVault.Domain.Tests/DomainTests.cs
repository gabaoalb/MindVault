using Xunit;
using MindVault.Domain;
namespace MindVault.Domain.Tests;
public sealed class DomainTests
{
    [Theory] [InlineData("")] [InlineData("   ")] [InlineData("\t\r\n")]
    public void Blank_titles_are_rejected(string value) => Assert.False(NoteTitle.Create(value).IsSuccess);
    [Fact] public void Whitespace_is_normalized()
    {
        var result = NoteTitle.Create("  Minha   primeira\tnota  ");
        Assert.Equal("Minha primeira nota", result.Value!.Value);
    }
    [Fact] public void Identifiers_are_uuid_v7()
    {
        var id = NoteId.New(TimeProvider.System);
        Assert.Equal(7, id.Value.Version);
        Assert.True(NoteId.Parse(id.ToString()).IsSuccess);
        Assert.False(NoteId.Parse(Guid.NewGuid().ToString()).IsSuccess);
    }
}
