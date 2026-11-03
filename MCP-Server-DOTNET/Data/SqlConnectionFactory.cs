using McpServerDotNet.Configuration;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace McpServerDotNet.Data;

/// <summary>
/// Creates opened SQL connections. Every repository gets connections from here,
/// so the connection logic lives in exactly one place.
/// </summary

public class SqlConnectionFactory
{
    private readonly DatabaseSettings _databaseSettings;
    private readonly string _connectionString;

    public SqlConnectionFactory(IOptions<DatabaseSettings> databaseSettings)
    {
        _databaseSettings = databaseSettings.Value;
        _connectionString = _databaseSettings.ConnectionString;
    }

    /// <summary>Opens and returns a new connection. The caller must dispose it (use 'await using').</summary>
    public async Task<SqlConnection> OpenAsync()
    {
        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }
}