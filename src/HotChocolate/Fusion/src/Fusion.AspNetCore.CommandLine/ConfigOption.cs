using System.CommandLine;

namespace HotChocolate.Fusion.AspNetCore.CommandLine;

internal sealed class ConfigOption : Option<string>
{
    public const string OptionName = "--config";

    public ConfigOption() : base(OptionName)
    {
        Description = "The path of the gateway config file. Overrides the path passed to AddConfigFile.";
        Recursive = true;
    }
}
