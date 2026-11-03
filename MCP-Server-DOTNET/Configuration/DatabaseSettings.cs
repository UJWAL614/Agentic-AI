namespace McpServerDotNet.Configuration;

/// <summary>
/// Strongly typed settings that map to the "Database" section in appsettings.json.
/// Keeping settings in a class means the rest of the code never reads raw config keys.
/// </summary>
public class DatabaseSettings
{
    // The name of the section in appsettings.json. Used when binding the settings.
    public const string SectionName = "Database";

    // Full SQL Server connection string (Windows authentication in our case).
    public string ConnectionString { get; set; } = string.Empty;

    // Safety limit so a tool can never return thousands of rows to the AI.
    public int MaxRowLimit { get; set; } = 100;
}