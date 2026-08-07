using System.Globalization;
using System.Text;
using MindVault.Application.Abstractions.Notes;
using MindVault.Application.Notes;
using MindVault.Domain.Notes;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace MindVault.Infrastructure.Notes;

public sealed class YamlNoteDocumentSerializer : INoteDocumentSerializer
{
    private readonly ISerializer yamlSerializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    private readonly IDeserializer yamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    public string Serialize(Note note, string body)
    {
        var metadata = new NoteMetadata
        {
            Id = note.Id.ToString(),
            Title = note.Title.Value,
            Created = note.CreatedAt.ToString("O"),
            Updated = note.UpdatedAt.ToString("O")
        };

        var yaml = yamlSerializer.Serialize(metadata).TrimEnd();
        return $"---\n{yaml}\n---\n\n{body.TrimStart()}";
    }

    public ParsedDocument Deserialize(string content, string fileName)
    {
        try
        {
            using var reader = new StringReader(content);
            if (!string.Equals(
                reader.ReadLine(),
                "---",
                StringComparison.Ordinal))
            {
                return new(
                    null,
                    $"'{fileName}' não possui frontmatter YAML.");
            }

            var yaml = new StringBuilder();
            string? line;
            var closed = false;
            while ((line = reader.ReadLine()) is not null)
            {
                if (line == "---")
                {
                    closed = true;
                    break;
                }

                yaml.AppendLine(line);
            }

            if (!closed)
            {
                return new(
                    null,
                    $"Frontmatter de '{fileName}' não foi fechado.");
            }

            var metadata = yamlDeserializer.Deserialize<NoteMetadata>(
                yaml.ToString());
            if (metadata is null)
            {
                return new(
                    null,
                    $"Frontmatter de '{fileName}' está vazio.");
            }

            var id = NoteId.Parse(metadata.Id ?? string.Empty);
            var title = NoteTitle.Create(metadata.Title);
            if (!id.IsSuccess)
            {
                return new(null, $"ID inválido em '{fileName}'.");
            }

            if (!title.IsSuccess)
            {
                return new(null, $"Título inválido em '{fileName}'.");
            }

            if (!DateTimeOffset.TryParseExact(
                metadata.Created,
                "O",
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var created))
            {
                return new(
                    null,
                    $"Data created inválida em '{fileName}'.");
            }

            if (!DateTimeOffset.TryParseExact(
                metadata.Updated,
                "O",
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var updated))
            {
                return new(
                    null,
                    $"Data updated inválida em '{fileName}'.");
            }

            return new(
                new Note(
                    id.Value!,
                    title.Value!,
                    fileName,
                    created,
                    updated),
                null);
        }
        catch (Exception exception)
            when (exception is YamlDotNet.Core.YamlException or
                InvalidOperationException)
        {
            return new(
                null,
                $"Frontmatter inválido em '{fileName}': {exception.Message}");
        }
    }
}
