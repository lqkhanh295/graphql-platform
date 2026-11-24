using System.Buffers;
using System.Text;
using System.Text.Json;
using HotChocolate.Text.Yaml;

namespace HotChocolate.Fusion.Configuration;

/// <summary>
/// Writes the effective gateway config as a YAML document.
/// </summary>
internal static class GatewayConfigExporter
{
    public static void Export(
        IServiceProvider services,
        string gatewayName,
        IReadOnlyList<GatewayConfigSection> sections,
        IBufferWriter<byte> output)
    {
        var writer = new YamlWriter(output);
        writer.WriteStartObject();

        foreach (var section in sections)
        {
            writer.WritePropertyName(section.Name);
            Write(section.ExportJson(services, gatewayName), writer);
        }

        writer.WriteEndObject();
    }

    private static void Write(JsonElement element, YamlWriter writer)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();

                foreach (var property in element.EnumerateObject())
                {
                    writer.WritePropertyName(property.Name);
                    Write(property.Value, writer);
                }

                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();

                foreach (var item in element.EnumerateArray())
                {
                    Write(item, writer);
                }

                writer.WriteEndArray();
                break;

            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;

            case JsonValueKind.Number:
                writer.WriteNumberValue(Encoding.UTF8.GetBytes(element.GetRawText()));
                break;

            case JsonValueKind.True or JsonValueKind.False:
                writer.WriteBooleanValue(element.GetBoolean());
                break;

            default:
                writer.WriteNullValue();
                break;
        }
    }
}
