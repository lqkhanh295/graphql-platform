using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HotChocolate.Fusion.Configuration;

internal sealed class DefaultFusionGatewayBuilder : IFusionGatewayBuilder
{
    public DefaultFusionGatewayBuilder(
        IServiceCollection services,
        string name,
        IConfigurationManager? configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(name);

        Services = services;
        Name = name;
        Configuration = configuration;
    }

    public string Name { get; }

    public IServiceCollection Services { get; }

    public IConfigurationManager? Configuration { get; }
}
