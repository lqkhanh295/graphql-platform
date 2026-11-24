using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HotChocolate.Fusion.Configuration;

/// <summary>
/// A top-level section of the gateway config file.
/// Derive from <see cref="GatewayConfigSection{TModel}"/> to define a section.
/// </summary>
public abstract class GatewayConfigSection
{
    private protected GatewayConfigSection()
    {
    }

    /// <summary>
    /// Gets the key of the section in the config file, for example <c>planner</c>.
    /// </summary>
    public abstract string Name { get; }

    internal abstract JsonTypeInfo ModelTypeInfo { get; }

    internal abstract void AddBinding(IServiceCollection services);

    internal abstract void Validate(IConfiguration configuration, List<string> errors);

    internal abstract void ApplyTo(IServiceProvider services, FusionGatewaySetup setup);

    internal abstract JsonElement ExportJson(IServiceProvider services, string gatewayName);
}

/// <summary>
/// A top-level section of the gateway config file, bound to <typeparamref name="TModel"/>.
/// The bound model is available as <see cref="IOptions{TOptions}"/> of <typeparamref name="TModel"/>.
/// </summary>
/// <typeparam name="TModel">The model the section is bound to.</typeparam>
public abstract class GatewayConfigSection<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TModel>
    : GatewayConfigSection
    where TModel : class, new()
{
    /// <summary>
    /// Gets the JSON metadata of the model, which defines the keys the section accepts and the
    /// shape it is exported with.
    /// </summary>
    public abstract JsonTypeInfo<TModel> TypeInfo { get; }

    internal sealed override JsonTypeInfo ModelTypeInfo => TypeInfo;

    /// <summary>
    /// Applies the bound model to the setup of the gateway that added the section.
    /// </summary>
    /// <param name="model">The bound model.</param>
    /// <param name="setup">The setup of the gateway.</param>
    public virtual void Configure(TModel model, FusionGatewaySetup setup)
    {
    }

    /// <summary>
    /// Gets the effective state of the section that <c>config export</c> writes.
    /// </summary>
    /// <param name="services">The application services.</param>
    /// <param name="gatewayName">The name of the gateway that loads the config file.</param>
    /// <returns>The model to export.</returns>
    public virtual TModel Export(IServiceProvider services, string gatewayName)
        => services.GetRequiredService<IOptionsMonitor<TModel>>().CurrentValue;

    internal sealed override void AddBinding(IServiceCollection services)
    {
        KeepClosedType(typeof(OptionsMonitor<TModel>));
        KeepClosedType(typeof(OptionsFactory<TModel>));

        services.AddOptions();
        services.AddSingleton<IPostConfigureOptions<TModel>>(
            sp => new BindGatewayConfigSection<TModel>(this, sp.GetRequiredService<IConfiguration>()));
    }

    internal sealed override void Validate(IConfiguration configuration, List<string> errors)
        => GatewayConfigBinder.Bind(new TModel(), TypeInfo, configuration, GetSection(configuration), errors);

    internal sealed override void ApplyTo(IServiceProvider services, FusionGatewaySetup setup)
        => Configure(services.GetRequiredService<IOptionsMonitor<TModel>>().CurrentValue, setup);

    internal sealed override JsonElement ExportJson(IServiceProvider services, string gatewayName)
        => JsonSerializer.SerializeToElement(Export(services, gatewayName), TypeInfo);

    internal IConfigurationSection GetSection(IConfiguration configuration)
        => configuration.GetSection(GatewayConfigFile.RootKey).GetSection(Name);

    // References a closed generic type that dependency injection creates at run time,
    // so that a Native AOT binary contains the type.
    private static void KeepClosedType(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type type)
        => GC.KeepAlive(type);
}
