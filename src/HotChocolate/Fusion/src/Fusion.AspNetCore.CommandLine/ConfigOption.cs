using System.CommandLine;

namespace HotChocolate.Fusion.AspNetCore.CommandLine;

/// <summary>
/// The <c>--config</c> option, which selects the gateway config file.
/// <c>AddConfigFile</c> reads it from the arguments the host was created with.
/// </summary>
internal sealed class ConfigOption : Option<string>
{
    public const string OptionName = "--config";

    public ConfigOption() : base(OptionName)
    {
        Description = "The path of the gateway config file. Overrides the path passed to AddConfigFile.";
        Recursive = true;
    }
}
