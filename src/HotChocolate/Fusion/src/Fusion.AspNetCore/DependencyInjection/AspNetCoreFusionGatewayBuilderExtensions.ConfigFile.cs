using System.Diagnostics.CodeAnalysis;
using HotChocolate.Fusion.AspNetCore;
using HotChocolate.Fusion.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.CommandLine;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

public static partial class AspNetCoreFusionGatewayBuilderExtensions
{
    private const string ConfigOption = "--config";
    private const string DefaultConfigFileName = "config.yaml";
    private const string AlternativeConfigFileName = "config.yml";

    /// <summary>
    /// Loads the gateway config from <c>config.yaml</c> or <c>config.yml</c> in the content root,
    /// or from the file passed with the <c>--config</c> command line option.
    /// The file is required when the host starts.
    /// </summary>
    /// <param name="builder">The gateway builder.</param>
    /// <returns>The <see cref="IFusionGatewayBuilder"/> for chaining.</returns>
    public static IFusionGatewayBuilder AddConfigFile(this IFusionGatewayBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return AddConfigFileCore(builder, path: null);
    }

    /// <summary>
    /// Loads the gateway config from <paramref name="path"/>, or from the file passed with the
    /// <c>--config</c> command line option. The file is required when the host starts.
    /// </summary>
    /// <param name="builder">The gateway builder.</param>
    /// <param name="path">The path of the config file, relative to the content root.</param>
    /// <returns>The <see cref="IFusionGatewayBuilder"/> for chaining.</returns>
    public static IFusionGatewayBuilder AddConfigFile(this IFusionGatewayBuilder builder, string path)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(path);

        return AddConfigFileCore(builder, path);
    }

    /// <summary>
    /// Adds a top-level section to the gateway config.
    /// </summary>
    /// <param name="builder">The gateway builder.</param>
    /// <param name="section">The section.</param>
    /// <typeparam name="TModel">The model the section is bound to.</typeparam>
    /// <returns>The <see cref="IFusionGatewayBuilder"/> for chaining.</returns>
    public static IFusionGatewayBuilder AddConfigSection<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TModel>(
        this IFusionGatewayBuilder builder,
        GatewayConfigSection<TModel> section)
        where TModel : class, new()
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(section);

        if (builder.Services.Any(
            d => d.ImplementationInstance is GatewayConfigSectionRegistration registration
                && registration.GatewayName == builder.Name
                && registration.Section.Name.Equals(section.Name, StringComparison.OrdinalIgnoreCase)))
        {
            throw ThrowHelper.ConfigSectionAlreadyAdded(section.Name);
        }

        builder.Services.AddSingleton(new GatewayConfigSectionRegistration(builder.Name, section));
        section.AddBinding(builder.Services);
        builder.Services
            .AddOptions<FusionGatewaySetup>(builder.Name)
            .PostConfigure<IServiceProvider>((setup, services) => section.ApplyTo(services, setup));

        return builder;
    }

    private static IFusionGatewayBuilder AddConfigFileCore(IFusionGatewayBuilder builder, string? path)
    {
        var configuration = builder.Configuration ?? throw ThrowHelper.ConfigFileRequiresHostBuilder();

        if (builder.Services.Any(d => d.ImplementationInstance is GatewayConfigFile))
        {
            throw ThrowHelper.ConfigFileAlreadyAdded();
        }

        var file = ResolveConfigFile(builder.Name, configuration, path);
        InsertBeforeEnvironmentVariables(configuration, new YamlConfigurationSource(file.FullPath));

        KeepClosedType(typeof(OptionsMonitor<GatewayConfigStartupValidation>));
        KeepClosedType(typeof(OptionsFactory<GatewayConfigStartupValidation>));

        builder.Services.AddSingleton(file);
        builder.Services
            .AddSingleton<IValidateOptions<GatewayConfigStartupValidation>, GatewayConfigStartupValidator>();
        builder.Services.AddOptions<GatewayConfigStartupValidation>().ValidateOnStart();

        return builder.AddConfigSection(new PlannerConfigSection());
    }

    private static GatewayConfigFile ResolveConfigFile(
        string gatewayName,
        IConfigurationManager configuration,
        string? path)
    {
        var contentRoot = configuration[HostDefaults.ContentRootKey] ?? Environment.CurrentDirectory;

        if (GetConfigOption(configuration) is { } option)
        {
            return new GatewayConfigFile(
                gatewayName,
                Path.GetFullPath(option, contentRoot),
                GatewayConfigFilePathSource.CommandLine);
        }

        if (path is not null)
        {
            return new GatewayConfigFile(
                gatewayName,
                Path.GetFullPath(path, contentRoot),
                GatewayConfigFilePathSource.Argument);
        }

        var yaml = Path.Combine(contentRoot, DefaultConfigFileName);
        var yml = Path.Combine(contentRoot, AlternativeConfigFileName);

        if (File.Exists(yaml) && File.Exists(yml))
        {
            throw ThrowHelper.ConfigFileAmbiguous(Path.GetDirectoryName(yaml)!);
        }

        return new GatewayConfigFile(
            gatewayName,
            File.Exists(yml) ? yml : yaml,
            GatewayConfigFilePathSource.Default);
    }

    private static string? GetConfigOption(IConfigurationManager configuration)
    {
        string? value = null;

        foreach (var source in configuration.Sources.OfType<CommandLineConfigurationSource>())
        {
            var args = source.Args.ToArray();

            for (var i = 0; i < args.Length; i++)
            {
                if (args[i] == ConfigOption && i + 1 < args.Length)
                {
                    value = args[++i];
                }
                else if (args[i].StartsWith(ConfigOption + "=", StringComparison.Ordinal))
                {
                    value = args[i][(ConfigOption.Length + 1)..];
                }
            }
        }

        return value;
    }

    private static void InsertBeforeEnvironmentVariables(
        IConfigurationManager configuration,
        IConfigurationSource source)
    {
        var sources = configuration.Sources;
        var index = IndexOf(sources, s => s is EnvironmentVariablesConfigurationSource { Prefix: null or "" });

        if (index < 0)
        {
            index = IndexOf(sources, s => s is CommandLineConfigurationSource);
        }

        if (index < 0)
        {
            sources.Add(source);
        }
        else
        {
            sources.Insert(index, source);
        }
    }

    private static int IndexOf(IList<IConfigurationSource> sources, Func<IConfigurationSource, bool> predicate)
    {
        for (var i = 0; i < sources.Count; i++)
        {
            if (predicate(sources[i]))
            {
                return i;
            }
        }

        return -1;
    }

    // References a closed generic type that dependency injection creates at run time,
    // so that a Native AOT binary contains the type.
    private static void KeepClosedType(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type type)
        => GC.KeepAlive(type);
}
