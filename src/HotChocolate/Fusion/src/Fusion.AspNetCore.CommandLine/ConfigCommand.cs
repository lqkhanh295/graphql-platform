using System.CommandLine;
using HotChocolate.Buffers;
using HotChocolate.Fusion.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace HotChocolate.Fusion.AspNetCore.CommandLine;

/// <summary>
/// The <c>config</c> command, which groups the gateway config commands.
/// </summary>
internal sealed class ConfigCommand : Command
{
    public const string CommandName = "config";

    public ConfigCommand(IHost host) : base(CommandName)
    {
        Description = "Gateway config commands.";
        Subcommands.Add(new ConfigExportCommand(host));
    }
}

/// <summary>
/// The <c>config export &lt;path&gt;</c> command, which writes the effective gateway config.
/// </summary>
internal sealed class ConfigExportCommand : Command
{
    public ConfigExportCommand(IHost host) : base("export")
    {
        Description = "Writes the effective gateway config as YAML to a file.";

        var pathArgument = new Argument<FileInfo>("path")
        {
            Description = "The path of the file to write."
        };

        Arguments.Add(pathArgument);

        SetAction(
            (parseResult, cancellationToken) => ExecuteAsync(
                host.Services,
                parseResult.GetRequiredValue(pathArgument),
                parseResult.InvocationConfiguration.Output,
                parseResult.InvocationConfiguration.Error,
                cancellationToken));
    }

    private static async Task<int> ExecuteAsync(
        IServiceProvider services,
        FileInfo path,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (services.GetService<GatewayConfigFile>() is not { } file)
        {
            await error.WriteLineAsync(
                "The gateway does not load a config file. Call AddConfigFile() on the gateway builder.");
            return 1;
        }

        var configuration = services.GetRequiredService<IConfiguration>();
        var sections = GatewayConfigValidator.GetSections(
            services.GetServices<GatewayConfigSectionRegistration>(),
            file.GatewayName);
        var errors = GatewayConfigValidator.Validate(configuration, sections);

        if (errors.Count > 0)
        {
            await error.WriteLineAsync(ThrowHelper.GatewayConfigInvalidMessage(errors));
            return 1;
        }

        using var buffer = new PooledArrayWriter();
        GatewayConfigExporter.Export(services, file.GatewayName, sections, buffer);

        await using (var stream = File.Create(path.FullName))
        {
            await stream.WriteAsync(buffer.WrittenMemory, cancellationToken);
        }

        await output.WriteLineAsync($"Exported the gateway config to {path.FullName}.");
        return 0;
    }
}
