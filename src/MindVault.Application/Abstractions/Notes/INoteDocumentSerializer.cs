using MindVault.Application.Notes;
using MindVault.Domain.Notes;

namespace MindVault.Application.Abstractions.Notes;


public interface INoteDocumentSerializer
{
    string Serialize(Note note, string body);

    ParsedDocument Deserialize(string content, string fileName);
}
