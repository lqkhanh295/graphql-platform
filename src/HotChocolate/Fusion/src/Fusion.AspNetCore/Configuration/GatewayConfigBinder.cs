using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using HotChocolate.Buffers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.CommandLine;
using Microsoft.Extensions.Configuration.EnvironmentVariables;

namespace HotChocolate.Fusion.Configuration;

/// <summary>
/// Binds a configuration section onto a model through its <see cref="JsonTypeInfo"/> and
/// collects an error for every key the model does not declare and every value it cannot read.
/// </summary>
internal sealed class GatewayConfigBinder(IConfiguration configuration, List<string> errors)
{
    public static void Bind(
        object model,
        JsonTypeInfo typeInfo,
        IConfiguration configuration,
        IConfigurationSection section,
        List<string> errors)
        => new GatewayConfigBinder(configuration, errors).BindObject(model, typeInfo, section);

    public static string DescribeKey(IConfiguration configuration, string key)
    {
        var path = key.StartsWith(GatewayConfigFile.RootKey + ":", StringComparison.OrdinalIgnoreCase)
            ? key[(GatewayConfigFile.RootKey.Length + 1)..]
            : key;
        path = path.Replace(':', '.');

        return GetSource(configuration, key) is { } source
            ? $"'{path}' ({source})"
            : $"'{path}'";
    }

    private void BindObject(object model, JsonTypeInfo typeInfo, IConfigurationSection section)
    {
        if (!string.IsNullOrEmpty(section.Value))
        {
            errors.Add($"{DescribeKey(configuration, section.Path)} must be a mapping.");
            return;
        }

        foreach (var child in section.GetChildren())
        {
            var property = FindProperty(typeInfo, child.Key);

            if (property?.Set is null)
            {
                errors.Add(UnknownSetting(child, typeInfo));
                continue;
            }

            if (child.Value is null && !child.GetChildren().Any())
            {
                continue;
            }

            var propertyTypeInfo = typeInfo.Options.GetTypeInfo(property.PropertyType);

            if (propertyTypeInfo.Kind == JsonTypeInfoKind.Object
                && property.Get?.Invoke(model) is { } current)
            {
                BindObject(current, propertyTypeInfo, child);
                continue;
            }

            if (TryRead(child, propertyTypeInfo, out var value))
            {
                property.Set(model, value);
            }
        }
    }

    private bool TryRead(IConfigurationSection section, JsonTypeInfo typeInfo, out object? value)
    {
        var errorCount = errors.Count;
        using var buffer = new PooledArrayWriter();

        using (var writer = new Utf8JsonWriter(buffer))
        {
            Write(section, typeInfo, writer);
        }

        value = null;

        if (errors.Count > errorCount)
        {
            return false;
        }

        try
        {
            value = JsonSerializer.Deserialize(buffer.WrittenSpan, typeInfo);
            return true;
        }
        catch (JsonException)
        {
            errors.Add($"{DescribeKey(configuration, section.Path)} has an invalid value.");
            return false;
        }
    }

    private void Write(IConfigurationSection section, JsonTypeInfo typeInfo, Utf8JsonWriter writer)
    {
        switch (typeInfo.Kind)
        {
            case JsonTypeInfoKind.Object:
                if (!string.IsNullOrEmpty(section.Value))
                {
                    errors.Add($"{DescribeKey(configuration, section.Path)} must be a mapping.");
                    return;
                }

                writer.WriteStartObject();

                foreach (var child in section.GetChildren())
                {
                    var property = FindProperty(typeInfo, child.Key);

                    if (property is null)
                    {
                        errors.Add(UnknownSetting(child, typeInfo));
                        continue;
                    }

                    writer.WritePropertyName(property.Name);
                    Write(child, typeInfo.Options.GetTypeInfo(property.PropertyType), writer);
                }

                writer.WriteEndObject();
                break;

            case JsonTypeInfoKind.Enumerable:
                var elementTypeInfo = typeInfo.Options.GetTypeInfo(GetElementType(typeInfo));
                writer.WriteStartArray();

                foreach (var child in section.GetChildren())
                {
                    if (!int.TryParse(child.Key, NumberStyles.None, CultureInfo.InvariantCulture, out _))
                    {
                        errors.Add($"{DescribeKey(configuration, child.Path)} is not a list index.");
                        continue;
                    }

                    Write(child, elementTypeInfo, writer);
                }

                writer.WriteEndArray();
                break;

            case JsonTypeInfoKind.Dictionary:
                var valueTypeInfo = typeInfo.Options.GetTypeInfo(GetElementType(typeInfo));
                writer.WriteStartObject();

                foreach (var child in section.GetChildren())
                {
                    writer.WritePropertyName(child.Key);
                    Write(child, valueTypeInfo, writer);
                }

                writer.WriteEndObject();
                break;

            default:
                WriteScalar(section, typeInfo.Type, writer);
                break;
        }
    }

    private void WriteScalar(IConfigurationSection section, Type type, Utf8JsonWriter writer)
    {
        var value = section.Value;

        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        type = Nullable.GetUnderlyingType(type) ?? type;

        if (type == typeof(bool))
        {
            if (bool.TryParse(value, out var boolean))
            {
                writer.WriteBooleanValue(boolean);
            }
            else
            {
                errors.Add($"{DescribeKey(configuration, section.Path)} must be a boolean, but is '{value}'.");
            }

            return;
        }

        if (IsNumber(type))
        {
            if (decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
            {
                writer.WriteNumberValue(number);
            }
            else
            {
                errors.Add($"{DescribeKey(configuration, section.Path)} must be a number, but is '{value}'.");
            }

            return;
        }

        writer.WriteStringValue(value);
    }

    private string UnknownSetting(IConfigurationSection section, JsonTypeInfo typeInfo)
    {
        var known = string.Join(", ", typeInfo.Properties.Where(p => p.Set is not null).Select(p => p.Name));
        return $"{DescribeKey(configuration, section.Path)} is not a known setting. Known settings: {known}.";
    }

    private static JsonPropertyInfo? FindProperty(JsonTypeInfo typeInfo, string key)
    {
        foreach (var property in typeInfo.Properties)
        {
            if (property.Name.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                return property;
            }
        }

        return null;
    }

    // The element type of a list, or the value type of a dictionary.
    private static Type GetElementType(JsonTypeInfo typeInfo)
#if NET9_0_OR_GREATER
        => typeInfo.ElementType!;
#else
        => typeInfo.Type.IsArray
            ? typeInfo.Type.GetElementType()!
            : typeInfo.Type.GetGenericArguments()[^1];
#endif

    private static bool IsNumber(Type type)
        => type == typeof(int) || type == typeof(long) || type == typeof(double) || type == typeof(decimal)
            || type == typeof(float) || type == typeof(short) || type == typeof(byte) || type == typeof(uint)
            || type == typeof(ulong) || type == typeof(ushort) || type == typeof(sbyte);

    private static string? GetSource(IConfiguration configuration, string key)
    {
        if (configuration is not IConfigurationRoot root)
        {
            return null;
        }

        for (var i = root.Providers.Count() - 1; i >= 0; i--)
        {
            var provider = root.Providers.ElementAt(i);

            if (provider is YamlConfigurationProvider yaml)
            {
                if (yaml.TryGetLine(key, out var line))
                {
                    return $"{yaml.FileName}, line {line}";
                }

                continue;
            }

            if (!provider.TryGet(key, out _) && !provider.GetChildKeys([], key).Any())
            {
                continue;
            }

            return provider switch
            {
                EnvironmentVariablesConfigurationProvider => $"environment variable {key.Replace(":", "__")}",
                CommandLineConfigurationProvider => "command line",
                _ => provider.ToString()
            };
        }

        return null;
    }
}
