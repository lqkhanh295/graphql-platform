using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using HotChocolate.Fusion.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace HotChocolate.Fusion.AspNetCore.CommandLine;

public sealed class ConfigExportCommandTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("fusion-config-").FullName;

    [Fact]
    public async Task ConfigExport_Should_WriteDefaults_When_ConfigFileDoesNotExist()
    {
        // arrange
        using var host = CreateHost(["config", "export", "out.yaml"], gateway => gateway.AddConfigFile());

        // act
        var exitCode = await new App(host).InvokeAsync(["config", "export", OutputPath]);

        // assert
        Assert.Equal(0, exitCode);
        File.ReadAllText(OutputPath).MatchInlineSnapshot(
            """
            planner:
              enableRequestGrouping: true

            """);
    }

    [Fact]
    public async Task ConfigExport_Should_WriteEffectiveConfig_When_CodeModifiesPlannerOptions()
    {
        // arrange
        WriteFile("config.yaml", "planner: {}\n");
        using var host = CreateHost(
            [],
            gateway => gateway
                .AddConfigFile()
                .ModifyPlannerOptions(o => o.EnableRequestGrouping = false));

        // act
        var exitCode = await new App(host).InvokeAsync(["config", "export", OutputPath]);

        // assert
        Assert.Equal(0, exitCode);
        File.ReadAllText(OutputPath).MatchInlineSnapshot(
            """
            planner:
              enableRequestGrouping: false

            """);
    }

    [Fact]
    public async Task ConfigExport_Should_IncludeExtensionSection_When_SectionIsAdded()
    {
        // arrange
        WriteFile(
            "prod.yaml",
            """
            extension:
              apiId: "123"
              headers:
                - Authorization
            """);
        using var host = CreateHost(
            ["--config", "prod.yaml"],
            gateway => gateway.AddConfigFile().AddConfigSection(new ExtensionConfigSection()));

        // act
        var exitCode = await new App(host).InvokeAsync(["config", "export", OutputPath, "--config", "prod.yaml"]);

        // assert
        Assert.Equal(0, exitCode);
        File.ReadAllText(OutputPath).MatchInlineSnapshot(
            """
            planner:
              enableRequestGrouping: true
            extension:
              apiId: "123"
              telemetry:
                enabled: true
                sampleRate: 10
              headers:
                - Authorization

            """);
    }

    [Fact]
    public async Task ConfigExport_Should_WriteFileThatLoadsBack_When_FileIsUsedAsConfig()
    {
        // arrange
        using (var host = CreateHost(
            [],
            gateway => gateway
                .AddConfigFile()
                .ModifyPlannerOptions(o => o.EnableRequestGrouping = false)))
        {
            await new App(host).InvokeAsync(["config", "export", OutputPath]);
        }

        using var exportedHost = CreateHost(["--config", OutputPath], gateway => gateway.AddConfigFile());
        var output = new StringWriter();

        // act
        var exitCode = await new App(exportedHost).InvokeAsync(
            ["config", "export", System.IO.Path.Combine(_directory, "again.yaml")],
            output);

        // assert
        Assert.Equal(0, exitCode);
        Assert.Equal(File.ReadAllText(OutputPath), File.ReadAllText(System.IO.Path.Combine(_directory, "again.yaml")));
    }

    [Fact]
    public async Task ConfigExport_Should_ReportErrors_When_ConfigIsInvalid()
    {
        // arrange
        WriteFile("config.yaml", "planner:\n  enableRequestGrouping: maybe\n");
        using var host = CreateHost([], gateway => gateway.AddConfigFile());
        var error = new StringWriter();

        // act
        var exitCode = await new App(host).InvokeAsync(["config", "export", OutputPath], error: error);

        // assert
        Assert.Equal(1, exitCode);
        Assert.False(File.Exists(OutputPath));
        Assert.Equal(
            "The gateway config is invalid:\n"
            + "- 'planner.enableRequestGrouping' (config.yaml, line 2) must be a boolean, but is 'maybe'.\n",
            error.ToString().ReplaceLineEndings("\n"));
    }

    [Fact]
    public async Task ConfigExport_Should_Fail_When_GatewayHasNoConfigFile()
    {
        // arrange
        using var host = CreateHost([], _ => { });
        var error = new StringWriter();

        // act
        var exitCode = await new App(host).InvokeAsync(["config", "export", OutputPath], error: error);

        // assert
        Assert.Equal(1, exitCode);
        Assert.Equal(
            "The gateway does not load a config file. Call AddConfigFile() on the gateway builder.\n",
            error.ToString().ReplaceLineEndings("\n"));
    }

    [Theory]
    [InlineData(new[] { "config", "export", "out.yaml" }, true)]
    [InlineData(new[] { "--config", "prod.yaml", "config", "export", "out.yaml" }, true)]
    [InlineData(new[] { "--config=prod.yaml", "config", "export", "out.yaml" }, true)]
    [InlineData(new[] { "--config", "prod.yaml" }, false)]
    [InlineData(new[] { "--urls", "http://localhost:5000" }, false)]
    [InlineData(new string[0], false)]
    public void IsGatewayCommand_Should_DetectCommand_When_ConfigOptionComesFirst(string[] args, bool expected)
    {
        // act
        var isCommand = args.IsGatewayCommand();

        // assert
        Assert.Equal(expected, isCommand);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private string OutputPath => System.IO.Path.Combine(_directory, "out.yaml");

    private void WriteFile(string name, string content)
        => File.WriteAllText(System.IO.Path.Combine(_directory, name), content);

    private IHost CreateHost(string[] args, Action<IFusionGatewayBuilder> configure)
    {
        var builder = Host.CreateApplicationBuilder(
            new HostApplicationBuilderSettings
            {
                Args = args,
                ContentRootPath = _directory,
                EnvironmentName = Environments.Production
            });

        configure(builder.AddGraphQLGateway());
        return builder.Build();
    }
}

public sealed class ExtensionConfig
{
    public string? ApiId { get; set; }

    public ExtensionTelemetryConfig Telemetry { get; set; } = new();

    public List<string> Headers { get; set; } = [];
}

public sealed class ExtensionTelemetryConfig
{
    public bool Enabled { get; set; } = true;

    public int SampleRate { get; set; } = 10;
}

internal sealed class ExtensionConfigSection : GatewayConfigSection<ExtensionConfig>
{
    public override string Name => "extension";

    public override JsonTypeInfo<ExtensionConfig> TypeInfo => ExtensionConfigJsonContext.Default.ExtensionConfig;
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ExtensionConfig))]
internal sealed partial class ExtensionConfigJsonContext : JsonSerializerContext;
