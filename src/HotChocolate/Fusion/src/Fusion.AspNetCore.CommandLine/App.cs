using System.CommandLine;
using Microsoft.Extensions.Hosting;

namespace HotChocolate.Fusion.AspNetCore.CommandLine;

internal sealed class App(IHost host)
{
    public int Invoke(string[] args, TextWriter? output = null, TextWriter? error = null)
        => CreateRootCommand().Parse(args).Invoke(CreateConfiguration(output, error));

    public Task<int> InvokeAsync(string[] args, TextWriter? output = null, TextWriter? error = null)
        => CreateRootCommand().Parse(args).InvokeAsync(CreateConfiguration(output, error));

    private RootCommand CreateRootCommand()
    {
        var root = new RootCommand("A command line tool for the Fusion gateway.");
        root.Options.Add(new ConfigOption());
        root.Subcommands.Add(new ConfigCommand(host));
        return root;
    }

    private static InvocationConfiguration? CreateConfiguration(TextWriter? output, TextWriter? error)
    {
        if (output is null && error is null)
        {
            return null;
        }

        var configuration = new InvocationConfiguration();

        if (output is not null)
        {
            configuration.Output = output;
        }

        if (error is not null)
        {
            configuration.Error = error;
        }

        return configuration;
    }
}
