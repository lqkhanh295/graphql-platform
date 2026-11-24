using System.Globalization;
using HotChocolate.Fusion.AspNetCore;
using HotChocolate.Text.Yaml;
using Microsoft.Extensions.Configuration;

namespace HotChocolate.Fusion.Configuration;

/// <summary>
/// Adds the gateway config file under the <c>Fusion</c> configuration section.
/// </summary>
internal sealed class YamlConfigurationSource(string fullPath) : IConfigurationSource
{
    public string FullPath { get; } = fullPath;

    public IConfigurationProvider Build(IConfigurationBuilder builder)
        => new YamlConfigurationProvider(FullPath);
}

/// <summary>
/// Loads configuration values from YAML. A missing file provides no keys.
/// </summary>
internal sealed class YamlConfigurationProvider(string fullPath) : ConfigurationProvider
{
    private Dictionary<string, int> _lines = new(StringComparer.OrdinalIgnoreCase);

    public string FileName { get; } = System.IO.Path.GetFileName(fullPath);

    public bool TryGetLine(string key, out int line)
        => _lines.TryGetValue(key, out line);

    public override void Load()
    {
        var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var lines = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        if (File.Exists(fullPath))
        {
            YamlDocument document;

            try
            {
                document = YamlDocument.Parse(File.ReadAllBytes(fullPath));
            }
            catch (YamlException ex)
            {
                throw ThrowHelper.ConfigFileInvalidYaml(fullPath, ex);
            }

            var root = document.RootElement;

            if (root.ValueKind is not (YamlValueKind.Object or YamlValueKind.Null))
            {
                throw ThrowHelper.ConfigFileRootNotMapping(fullPath);
            }

            if (root.ValueKind == YamlValueKind.Object)
            {
                Visit(root, GatewayConfigFile.RootKey, data, lines);
            }
        }

        Data = data;
        _lines = lines;
    }

    private static void Visit(
        YamlElement element,
        string key,
        Dictionary<string, string?> data,
        Dictionary<string, int> lines)
    {
        switch (element.ValueKind)
        {
            case YamlValueKind.Object:
                var hasProperties = false;

                foreach (var property in element.EnumerateObject())
                {
                    var propertyKey = ConfigurationPath.Combine(key, property.Name);
                    lines[propertyKey] = property.Line;
                    Visit(property.Value, propertyKey, data, lines);
                    hasProperties = true;
                }

                if (!hasProperties)
                {
                    data[key] = null;
                }

                break;

            case YamlValueKind.Array:
                var index = 0;

                foreach (var item in element.EnumerateArray())
                {
                    var itemKey = ConfigurationPath.Combine(key, index.ToString(CultureInfo.InvariantCulture));
                    lines[itemKey] = item.Line;
                    Visit(item, itemKey, data, lines);
                    index++;
                }

                if (index == 0)
                {
                    data[key] = null;
                }

                break;

            case YamlValueKind.Null:
                data[key] = null;
                break;

            default:
                data[key] = element.ToString();
                break;
        }
    }
}
