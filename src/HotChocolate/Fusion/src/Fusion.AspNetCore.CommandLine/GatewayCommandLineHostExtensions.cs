using HotChocolate.Fusion.AspNetCore.CommandLine;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Runs the Fusion gateway or one of its command line commands.
/// </summary>
public static class GatewayCommandLineHostExtensions
{
    /// <summary>
    /// Builds the host and either runs a gateway command or starts the gateway.
    /// </summary>
    /// <example>
    /// dotnet run -- config export ./config.yaml
    /// </example>
    /// <param name="builder">The host builder.</param>
    /// <param name="args">The command line arguments.</param>
    /// <returns>The exit code.</returns>
    public static async Task<int> RunWithGatewayCommandsAsync(this IHostBuilder builder, string[] args)
        => await builder.Build().RunWithGatewayCommandsAsync(args);

    /// <summary>
    /// Builds the host and either runs a gateway command or starts the gateway.
    /// </summary>
    /// <example>
    /// dotnet run -- config export ./config.yaml
    /// </example>
    /// <param name="builder">The host builder.</param>
    /// <param name="args">The command line arguments.</param>
    /// <returns>The exit code.</returns>
    public static int RunWithGatewayCommands(this IHostBuilder builder, string[] args)
        => builder.Build().RunWithGatewayCommands(args);

    /// <summary>
    /// Runs a gateway command when the arguments name one, and starts the gateway otherwise.
    /// </summary>
    /// <example>
    /// dotnet run -- config export ./config.yaml
    /// </example>
    /// <param name="host">The host.</param>
    /// <param name="args">The command line arguments.</param>
    /// <returns>The exit code.</returns>
    public static async Task<int> RunWithGatewayCommandsAsync(this IHost host, string[] args)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(args);

        if (args.IsGatewayCommand())
        {
            return await new App(host).InvokeAsync(args);
        }

        await host.RunAsync();
        return 0;
    }

    /// <summary>
    /// Runs a gateway command when the arguments name one, and starts the gateway otherwise.
    /// </summary>
    /// <example>
    /// dotnet run -- config export ./config.yaml
    /// </example>
    /// <param name="host">The host.</param>
    /// <param name="args">The command line arguments.</param>
    /// <returns>The exit code.</returns>
    public static int RunWithGatewayCommands(this IHost host, string[] args)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(args);

        if (args.IsGatewayCommand())
        {
            return new App(host).Invoke(args);
        }

        host.Run();
        return 0;
    }

    /// <summary>
    /// Returns <see langword="true"/> when the arguments invoke a gateway command instead of
    /// starting the gateway.
    /// </summary>
    /// <param name="args">The command line arguments.</param>
    /// <returns>
    /// <see langword="true"/> if the first argument after the <c>--config</c> option is a command.
    /// </returns>
    public static bool IsGatewayCommand(this string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == ConfigOption.OptionName)
            {
                i++;
                continue;
            }

            if (args[i].StartsWith(ConfigOption.OptionName + "=", StringComparison.Ordinal))
            {
                continue;
            }

            return args[i] == ConfigCommand.CommandName;
        }

        return false;
    }
}
