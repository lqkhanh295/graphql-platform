namespace HotChocolate.Fusion.Configuration;

/// <summary>
/// The config file that a gateway loads.
/// </summary>
/// <param name="GatewayName">The name of the gateway that loads the file.</param>
/// <param name="FullPath">The full path of the file.</param>
/// <param name="PathSource">Where the path came from.</param>
internal sealed record GatewayConfigFile(
    string GatewayName,
    string FullPath,
    GatewayConfigFilePathSource PathSource)
{
    /// <summary>
    /// The configuration section that holds the values of the config file.
    /// </summary>
    public const string RootKey = "Fusion";
}

/// <summary>
/// Specifies where the path of the gateway config file came from.
/// </summary>
internal enum GatewayConfigFilePathSource
{
    /// <summary>
    /// The <c>--config</c> command line option.
    /// </summary>
    CommandLine,

    /// <summary>
    /// The path passed to <c>AddConfigFile</c>.
    /// </summary>
    Argument,

    /// <summary>
    /// The default file name in the content root.
    /// </summary>
    Default
}

/// <summary>
/// A config section that was added to a gateway.
/// </summary>
/// <param name="GatewayName">The name of the gateway.</param>
/// <param name="Section">The section.</param>
internal sealed record GatewayConfigSectionRegistration(string GatewayName, GatewayConfigSection Section);
