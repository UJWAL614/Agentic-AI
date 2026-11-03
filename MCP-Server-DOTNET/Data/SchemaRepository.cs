using Microsoft.Data.SqlClient;

namespace McpServerDotNet.Data;

/// <summary>
/// Reads schema information: which tables exist and which columns each table has.
/// It also validates table names, which protects us from SQL injection.
/// </summary>
public class SchemaRepository
{
    private readonly SqlConnectionFactory _factory;

    public SchemaRepository(SqlConnectionFactory factory)
    {
        _factory = factory;
    }

    /// <summary>Returns every user table as 'schema.table', for example 'dbo.Leads'.</summary>
    public async Task<List<string>> GetTablesAsync()
    {
        var tables = new List<string>();

        await using var conn = await _factory.OpenAsync();
        await using var cmd = new SqlCommand(
            "SELECT TABLE_SCHEMA + '.' + TABLE_NAME " +
            "FROM INFORMATION_SCHEMA.TABLES " +
            "WHERE TABLE_TYPE = 'BASE TABLE' ORDER BY 1", conn);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tables.Add(reader.GetString(0));
        }

        return tables;
    }

    /// <summary>
    /// Returns column name and data type for one table.
    /// Throws if the table does not exist, so callers get a validation check for free.
    /// </summary>
    public async Task<Dictionary<string, string>> GetColumnsAsync(string table)
    {
        var columns = new Dictionary<string, string>();

        await using var conn = await _factory.OpenAsync();
        await using var cmd = new SqlCommand(
            "SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS " +
            "WHERE TABLE_NAME = @table ORDER BY ORDINAL_POSITION", conn);

        // Parameters keep user input separate from the SQL text.
        cmd.Parameters.AddWithValue("@table", table);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns[reader.GetString(0)] = reader.GetString(1);
        }

        if (columns.Count == 0)
        {
            throw new ArgumentException($"Table '{table}' was not found.");
        }

        return columns;
    }
}