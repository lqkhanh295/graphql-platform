using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using HotChocolate.Fusion.Execution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HotChocolate.Fusion.Configuration;

/// <summary>
/// The <c>planner</c> section of the gateway config file.
/// A <c>null</c> property is not set in the config and keeps the value of the planner options.
/// </summary>
internal sealed class PlannerConfig
{
    public bool? EnableRequestGrouping { get; set; }
}

/// <summary>
/// Applies the <c>planner</c> section to the planner options of the gateway.
/// </summary>
internal sealed class PlannerConfigSection : GatewayConfigSection<PlannerConfig>
{
    public override string Name => "planner";

    public override JsonTypeInfo<PlannerConfig> TypeInfo
        => GatewayConfigJsonContext.Default.PlannerConfig;

    public override void Configure(PlannerConfig model, FusionGatewaySetup setup)
    {
        if (model.EnableRequestGrouping is { } enableRequestGrouping)
        {
            setup.PlannerOptionsModifiers.Add(o => o.EnableRequestGrouping = enableRequestGrouping);
        }
    }

    public override PlannerConfig Export(IServiceProvider services, string gatewayName)
    {
        var setup = services.GetRequiredService<IOptionsMonitor<FusionGatewaySetup>>().Get(gatewayName);
        var options = FusionRequestExecutorManager.CreatePlannerOptions(
            setup,
            FusionRequestExecutorManager.CreateOptions(setup));

        return new PlannerConfig { EnableRequestGrouping = options.EnableRequestGrouping };
    }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(PlannerConfig))]
internal sealed partial class GatewayConfigJsonContext : JsonSerializerContext;
