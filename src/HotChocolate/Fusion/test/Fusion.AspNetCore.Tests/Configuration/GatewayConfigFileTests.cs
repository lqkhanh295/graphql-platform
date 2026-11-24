using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace HotChocolate.Fusion.Configuration;

public sealed class GatewayConfigFileTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("fusion-config-").FullName;

    [Fact]
    public void AddConfigFile_Should_LoadDefaultFile_When_ConfigYamlExists()
    {
        // arrange
        WriteFile("config.yaml", "extension:\n  apiId: default\n");
        var builder = CreateBuilder();

        // act
        builder.AddGraphQLGateway().AddConfigFile();

        // assert
        Assert.Equal("default", builder.Configuration["Fusion:extension:apiId"]);
    }

    [Fact]
    public void AddConfigFile_Should_LoadConfigOption_When_CommandLineHasConfig()
    {
        // arrange
        WriteFile("config.yaml", "extension:\n  apiId: default\n");
        WriteFile("prod.yaml", "extension:\n  apiId: prod\n");
        var builder = CreateBuilder("--config", "prod.yaml");

        // act
        builder.AddGraphQLGateway().AddConfigFile("config.yaml");

        // assert
        Assert.Equal("prod", builder.Configuration["Fusion:extension:apiId"]);
    }

    [Fact]
    public void AddConfigFile_Should_LoadPath_When_PathIsPassed()
    {
        // arrange
        WriteFile("gateway.yml", "extension:\n  apiId: gateway\n");
        var builder = CreateBuilder();

        // act
        builder.AddGraphQLGateway().AddConfigFile("gateway.yml");

        // assert
        Assert.Equal("gateway", builder.Configuration["Fusion:extension:apiId"]);
    }

    [Fact]
    public void AddConfigFile_Should_PreferCommandLine_When_FileSetsSameKey()
    {
        // arrange
        WriteFile("config.yaml", "extension:\n  apiId: file\n");
        var builder = CreateBuilder("--Fusion:extension:apiId=command-line");

        // act
        builder.AddGraphQLGateway().AddConfigFile();

        // assert
        Assert.Equal("command-line", builder.Configuration["Fusion:extension:apiId"]);
    }

    [Fact]
    public void AddConfigFile_Should_InsertFileBeforeEnvironmentVariables_When_HostHasDefaults()
    {
        // arrange
        WriteFile("config.yaml", "extension:\n  apiId: default\n");
        var builder = CreateBuilder();

        // act
        builder.AddGraphQLGateway().AddConfigFile();

        // assert
        var sources = builder.Configuration.Sources.ToList();
        var index = sources.FindIndex(s => s is YamlConfigurationSource);
        var next = Assert.IsType<EnvironmentVariablesConfigurationSource>(sources[index + 1]);
        Assert.True(string.IsNullOrEmpty(next.Prefix));
    }

    [Fact]
    public void AddConfigFile_Should_Throw_When_YamlAndYmlBothExist()
    {
        // arrange
        WriteFile("config.yaml", "planner: {}\n");
        WriteFile("config.yml", "planner: {}\n");
        var gateway = CreateBuilder().AddGraphQLGateway();

        // act
        var exception = Assert.Throws<InvalidOperationException>(gateway.AddConfigFile);

        // assert
        Assert.Equal(
            $"Both 'config.yaml' and 'config.yml' exist in '{_directory}'. "
            + "Delete one of them, or select one with --config <path>.",
            exception.Message);
    }

    [Fact]
    public void AddConfigFile_Should_Throw_When_FileIsNotValidYaml()
    {
        // arrange
        WriteFile("config.yaml", "planner:\n  enableRequestGrouping: &flag true\n");
        var gateway = CreateBuilder().AddGraphQLGateway();

        // act
        var exception = Assert.Throws<InvalidOperationException>(gateway.AddConfigFile);

        // assert
        Assert.Equal(
            $"The gateway config file '{System.IO.Path.Combine(_directory, "config.yaml")}' is not valid YAML: "
            + "Anchors are not supported (line 2, column 26).",
            exception.Message);
    }

    [Fact]
    public void AddConfigFile_Should_Throw_When_GatewayHasNoHostBuilder()
    {
        // arrange
        var gateway = new ServiceCollection().AddGraphQLGateway();

        // act
        var exception = Assert.Throws<InvalidOperationException>(gateway.AddConfigFile);

        // assert
        Assert.Equal(
            "AddConfigFile requires a gateway that was added with IHostApplicationBuilder.AddGraphQLGateway.",
            exception.Message);
    }

    [Fact]
    public async Task StartAsync_Should_Throw_When_DefaultFileIsMissing()
    {
        // arrange
        var builder = CreateBuilder();
        builder.AddGraphQLGateway().AddConfigFile();
        using var host = builder.Build();

        // act
        var exception = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync(TestContext.Current.CancellationToken));

        // assert
        Assert.Equal(
            "The gateway config file was not found. AddConfigFile() looks for 'config.yaml' and "
            + $"'config.yml' in '{_directory}'. Create one of them, pass a path to AddConfigFile, "
            + "or start the gateway with --config <path>.",
            exception.Message);
    }

    [Fact]
    public async Task StartAsync_Should_Throw_When_ConfigOptionFileIsMissing()
    {
        // arrange
        var builder = CreateBuilder("--config", "missing.yaml");
        builder.AddGraphQLGateway().AddConfigFile();
        using var host = builder.Build();

        // act
        var exception = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync(TestContext.Current.CancellationToken));

        // assert
        Assert.Equal(
            $"The gateway config file '{System.IO.Path.Combine(_directory, "missing.yaml")}' was not found. "
            + "The path was set with --config.",
            exception.Message);
    }

    [Fact]
    public async Task StartAsync_Should_ReportAllErrors_When_KeysAreUnknownOrInvalid()
    {
        // arrange
        WriteFile(
            "config.yaml",
            """
            planner:
              enableRequestGrouping: maybe
              enableRequestGroupin: true
            nitro:
              apiId: abc
            extension:
              telemetry:
                enabled: yes
                sampleRate: often
              unknown: 1
            """);
        var builder = CreateBuilder();
        builder.AddGraphQLGateway().AddConfigFile().AddConfigSection(new ExtensionConfigSection());
        using var host = builder.Build();

        // act
        var exception = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync(TestContext.Current.CancellationToken));

        // assert
        exception.Message.MatchInlineSnapshot(
            """
            The gateway config is invalid:
            - 'nitro' (config.yaml, line 4) is not a known section. Known sections: extension.
            - 'planner' (config.yaml, line 1) is not a known section. Known sections: extension.
            - 'extension.telemetry.enabled' (config.yaml, line 8) must be a boolean, but is 'yes'.
            - 'extension.telemetry.sampleRate' (config.yaml, line 9) must be a number, but is 'often'.
            - 'extension.unknown' (config.yaml, line 10) is not a known setting. Known settings: apiId, telemetry, headers.
            """);
    }

    [Fact]
    public void AddConfigSection_Should_BindModel_When_ExtensionAddsSection()
    {
        // arrange
        WriteFile(
            "config.yaml",
            """
            extension:
              apiId: abc
              telemetry:
                sampleRate: 5
              headers:
                - Authorization
                - X-Request-Id
            """);
        var builder = CreateBuilder();
        builder.AddGraphQLGateway().AddConfigFile().AddConfigSection(new ExtensionConfigSection());
        builder.Services.Configure<ExtensionConfig>(c => c.Telemetry.Enabled = false);

        // act
        using var host = builder.Build();
        var config = host.Services.GetRequiredService<IOptions<ExtensionConfig>>().Value;

        // assert
        Assert.Equal("abc", config.ApiId);
        Assert.False(config.Telemetry.Enabled);
        Assert.Equal(5, config.Telemetry.SampleRate);
        Assert.Equal(new[] { "Authorization", "X-Request-Id" }, config.Headers);
    }

    [Fact]
    public void AddConfigSection_Should_Throw_When_SectionNameIsAddedTwice()
    {
        // arrange
        var gateway = CreateBuilder().AddGraphQLGateway().AddConfigSection(new ExtensionConfigSection());

        // act
        var exception = Assert.Throws<InvalidOperationException>(
            () => gateway.AddConfigSection(new ExtensionConfigSection()));

        // assert
        Assert.Equal("The gateway config already has a section named 'extension'.", exception.Message);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private void WriteFile(string name, string content)
        => File.WriteAllText(System.IO.Path.Combine(_directory, name), content);

    private HostApplicationBuilder CreateBuilder(params string[] args)
        => Host.CreateApplicationBuilder(
            new HostApplicationBuilderSettings
            {
                Args = args,
                ContentRootPath = _directory,
                EnvironmentName = Environments.Production
            });
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
