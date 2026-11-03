using McpServerDotNet.Configuration;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace McpServerDotNet.Data;

/// <summary>
/// Reads data from a table: sample rows and statistics.
/// Table and column names cannot be SQL parameters, so we validate them against the
/// real schema first (SchemaRepository) and then wrap them in [brackets].
/// </summary>
public class TableRepository
{
    private readonly SqlConnectionFactory _factory;
    private readonly SchemaRepository _schema;
    private readonly int _maxRowLimit;

    // Data types we can average.
    private static readonly HashSet<string> NumericTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "int", "bigint", "smallint", "tinyint", "decimal",
        "numeric", "float", "real", "money", "smallmoney"
    };

    // Data types that cannot be used with MIN, MAX, or DISTINCT.
    private static readonly HashSet<string> SkipTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "text", "ntext", "image", "xml", "varbinary", "binary"
    };

    public TableRepository(
        SqlConnectionFactory factory,
        SchemaRepository schema,
        IOptions<DatabaseSettings> options)
    {
        _factory = factory;
        _schema = schema;
        _maxRowLimit = options.Value.MaxRowLimit;
    }

    /// <summary>Fetches the top N rows of a table. N is clamped between 1 and MaxRowLimit.</summary>
    public async Task<List<Dictionary<string, string?>>> GetRowsAsync(string table, int limit)
    {
        // Step 1: validate the table name against the real schema.
        await _schema.GetColumnsAsync(table);

        // Step 2: keep the limit inside safe bounds.
        limit = Math.Clamp(limit, 1, _maxRowLimit);

        // Step 3: run the query and turn each row into a dictionary of column to value.
        var rows = new List<Dictionary<string, string?>>();

        await using var conn = await _factory.OpenAsync();
        await using var cmd = new SqlCommand($"SELECT TOP ({limit}) * FROM [{table}]", conn);
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, string?>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i).ToString();
            }
            rows.Add(row);
        }

        return rows;
    }

    /// <summary>Returns row count plus per-column statistics for a table.</summary>
    public async Task<Dictionary<string, object?>> GetStatisticsAsync(string table)
    {
        // Step 1: validate the table and get its columns.
        var columns = await _schema.GetColumnsAsync(table);
        var columnStats = new Dictionary<string, object?>();

        await using var conn = await _factory.OpenAsync();

        // Step 2: total row count.
        long rowCount;
        await using (var countCmd = new SqlCommand($"SELECT COUNT(*) FROM [{table}]", conn))
        {
            rowCount = Convert.ToInt64(await countCmd.ExecuteScalarAsync());
        }

        // Step 3: statistics for each column.
        foreach (var (column, type) in columns)
        {
            if (SkipTypes.Contains(type)) continue;

            bool isNumeric = NumericTypes.Contains(type);

            // Build one query that returns nulls, distinct, min, max (and average for numbers).
            var sql =
                $"SELECT SUM(CASE WHEN [{column}] IS NULL THEN 1 ELSE 0 END), " +
                $"COUNT(DISTINCT [{column}]), MIN([{column}]), MAX([{column}])" +
                (isNumeric ? $", AVG(CAST([{column}] AS FLOAT))" : "") +
                $" FROM [{table}]";

            await using var cmd = new SqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            await reader.ReadAsync();

            var stats = new Dictionary<string, object?>
            {
                ["data_type"] = type,
                ["null_count"] = reader.IsDBNull(0) ? 0 : Convert.ToInt64(reader.GetValue(0)),
                ["distinct_count"] = Convert.ToInt64(reader.GetValue(1)),
                ["min"] = reader.IsDBNull(2) ? null : reader.GetValue(2).ToString(),
                ["max"] = reader.IsDBNull(3) ? null : reader.GetValue(3).ToString()
            };

            if (isNumeric)
            {
                stats["average"] = reader.IsDBNull(4) ? null : Convert.ToDouble(reader.GetValue(4));
            }

            columnStats[column] = stats;
        }

        // Step 4: combine everything into one result.
        return new Dictionary<string, object?>
        {
            ["table"] = table,
            ["row_count"] = rowCount,
            ["columns"] = columnStats
        };
    }
}