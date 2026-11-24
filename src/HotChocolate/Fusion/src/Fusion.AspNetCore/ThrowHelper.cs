using HotChocolate.Fusion.Configuration;
using HotChocolate.Text.Yaml;

namespace HotChocolate.Fusion.AspNetCore;

internal static class ThrowHelper
{
    public static InvalidOperationException ConfigFileRequiresHostBuilder()
        => new("AddConfigFile requires a gateway that was added with IHostApplicationBuilder.AddGraphQLGateway.");

    public static InvalidOperationException ConfigFileAlreadyAdded()
        => new("The application already loads a gateway config file. Only one gateway can call AddConfigFile.");

    public static InvalidOperationException ConfigSectionAlreadyAdded(string name)
        => new($"The gateway config already has a section named '{name}'.");

    public static InvalidOperationException ConfigFileAmbiguous(string directory)
        => new(
            $"Both 'config.yaml' and 'config.yml' exist in '{directory}'. "
            + "Delete one of them, or select one with --config <path>.");

    public static InvalidOperationException ConfigFileInvalidYaml(string path, YamlException exception)
        => new($"The gateway config file '{path}' is not valid YAML: {exception.Message}", exception);

    public static InvalidOperationException ConfigFileRootNotMapping(string path)
        => new($"The gateway config file '{path}' must contain a mapping of sections at its root.");

    public static InvalidOperationException GatewayConfigInvalid(IEnumerable<string> errors)
        => new(GatewayConfigInvalidMessage(errors));

    public static string GatewayConfigInvalidMessage(IEnumerable<string> errors)
        => "The gateway config is invalid:" + string.Concat(errors.Select(e => "\n- " + e));

    public static string ConfigFileNotFoundMessage(GatewayConfigFile file)
        => file.PathSource switch
        {
            GatewayConfigFilePathSource.CommandLine
                => $"The gateway config file '{file.FullPath}' was not found. The path was set with --config.",
            GatewayConfigFilePathSource.Argument
                => $"The gateway config file '{file.FullPath}' was not found. The path was passed to AddConfigFile.",
            _ => "The gateway config file was not found. AddConfigFile() looks for 'config.yaml' and "
                + $"'config.yml' in '{System.IO.Path.GetDirectoryName(file.FullPath)}'. Create one of them, pass a path "
                + "to AddConfigFile, or start the gateway with --config <path>."
        };
}
