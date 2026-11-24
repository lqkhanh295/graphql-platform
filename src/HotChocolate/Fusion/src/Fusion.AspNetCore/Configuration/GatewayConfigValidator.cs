using HotChocolate.Fusion.AspNetCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace HotChocolate.Fusion.Configuration;

/// <summary>
/// Validates the gateway config against the sections that were added to the gateway.
/// </summary>
internal static class GatewayConfigValidator
{
    public static List<string> Validate(
        IConfiguration configuration,
        IReadOnlyList<GatewayConfigSection> sections)
    {
        var errors = new List<string>();
        var root = configuration.GetSection(GatewayConfigFile.RootKey);

        foreach (var child in root.GetChildren())
        {
            if (!sections.Any(s => s.Name.Equals(child.Key, StringComparison.OrdinalIgnoreCase)))
            {
                var known = string.Join(", ", sections.Select(s => s.Name));
                errors.Add(
                    $"{GatewayConfigBinder.DescribeKey(configuration, child.Path)} is not a known section. "
                    + $"Known sections: {known}.");
            }
        }

        foreach (var section in sections)
        {
            section.Validate(configuration, errors);
        }

        return errors;
    }

    public static IReadOnlyList<GatewayConfigSection> GetSections(
        IEnumerable<GatewayConfigSectionRegistration> registrations,
        string gatewayName)
        => [.. registrations.Where(r => r.GatewayName == gatewayName).Select(r => r.Section)];
}

/// <summary>
/// The options that trigger the validation of the gateway config file when the host starts.
/// </summary>
internal sealed class GatewayConfigStartupValidation;

/// <summary>
/// Checks on host start that the gateway config file exists and that its values are valid.
/// </summary>
internal sealed class GatewayConfigStartupValidator(
    GatewayConfigFile file,
    IConfiguration configuration,
    IEnumerable<GatewayConfigSectionRegistration> registrations)
    : IValidateOptions<GatewayConfigStartupValidation>
{
    public ValidateOptionsResult Validate(string? name, GatewayConfigStartupValidation options)
    {
        if (!File.Exists(file.FullPath))
        {
            return ValidateOptionsResult.Fail(ThrowHelper.ConfigFileNotFoundMessage(file));
        }

        var sections = GatewayConfigValidator.GetSections(registrations, file.GatewayName);
        var errors = GatewayConfigValidator.Validate(configuration, sections);

        return errors.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(ThrowHelper.GatewayConfigInvalidMessage(errors));
    }
}

/// <summary>
/// Binds the values of a config section onto its model.
/// </summary>
internal sealed class BindGatewayConfigSection<TModel>(
    GatewayConfigSection<TModel> section,
    IConfiguration configuration)
    : IPostConfigureOptions<TModel>
    where TModel : class, new()
{
    public void PostConfigure(string? name, TModel options)
    {
        if (name != Options.DefaultName)
        {
            return;
        }

        var errors = new List<string>();
        GatewayConfigBinder.Bind(options, section.TypeInfo, configuration, section.GetSection(configuration), errors);

        if (errors.Count > 0)
        {
            throw ThrowHelper.GatewayConfigInvalid(errors);
        }
    }
}
