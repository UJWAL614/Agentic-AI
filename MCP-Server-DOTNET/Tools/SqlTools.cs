using System.ComponentModel;
using McpServerDotNet.Data;
using ModelContextProtocol.Server;

namespace McpServerDotNet.Tools;

/// <summary>
/// MCP tools for exploring a SQL Server database.
/// The tools stay thin: they only call repositories. No SQL lives here.
/// The repositories are injected into each method by the MCP SDK.
/// </summary>
[McpServerToolType]
public class SqlTools
{
    private readonly TableRepository _tableRepository;
    private readonly SchemaRepository _schemaRepository;

    public SqlTools(TableRepository tableRepository, SchemaRepository schemaRepository)
    {
        _tableRepository = tableRepository;
        _schemaRepository = schemaRepository;
    }

   [McpServerTool(Name="list-tables")]
    [Description("Returns a list of all user tables in the database, in the format 'schema.table'.")]
    public async Task<List<string>> ListTablesAsync()
    {
        return await _schemaRepository.GetTablesAsync();
    }

    [McpServerTool(Name="describe-table")]
    [Description("Returns a dictionary of column name to data type for the given table. Throws if the table does not exist.")]
    public async Task<Dictionary<string, string>> ListColumnsAsync(string table)
    {
        return await _schemaRepository.GetColumnsAsync(table);
    }

    [McpServerTool(Name="get-rows")]
    [Description("Returns the top N rows of the specified table. Throws if the table does not exist.")]
    public async Task<List<Dictionary<string, string?>>> GetRowsAsync(string table, int limit)
    {
        return await _tableRepository.GetRowsAsync(table, limit);
    }
    [McpServerTool(Name="table-stats")]
    [Description("Returns statistics for the specified table. Throws if the table does not exist.")]
    public async Task<Dictionary<string, object?>> GetTableStatsAsync(string table)
    {
        return await _tableRepository.GetStatisticsAsync(table);
    }
}