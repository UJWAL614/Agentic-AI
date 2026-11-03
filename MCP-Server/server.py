import sys
import pyodbc
from mcp.server.fastmcp import FastMCP

SERVER = r"AYAN-RIYAN"
DATABASE = "agenticAI_db"
DRIVER = "ODBC Driver 18 for SQL Server"

mcp = FastMCP("sql-server-stats")


def get_connection():
    conn_str = (
        f"DRIVER={{{DRIVER}}};"
        f"SERVER={SERVER};"
        f"DATABASE={DATABASE};"
        "Trusted_Connection=yes;"
        "TrustServerCertificate=yes;"
    )
    return pyodbc.connect(conn_str)


def get_table_columns(table: str) -> dict:
    """Return {column_name: data_type} for a table, or raise if not found."""
    with get_connection() as conn:
        cur = conn.cursor()
        cur.execute(
            "SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS "
            "WHERE TABLE_NAME = ?",
            table,
        )
        rows = cur.fetchall()
    if not rows:
        raise ValueError(f"Table '{table}' not found.")
    return {r[0]: r[1] for r in rows}


@mcp.tool()
def list_tables() -> list[str]:
    """List all user tables in the database."""
    with get_connection() as conn:
        cur = conn.cursor()
        cur.execute(
            "SELECT TABLE_SCHEMA + '.' + TABLE_NAME "
            "FROM INFORMATION_SCHEMA.TABLES "
            "WHERE TABLE_TYPE = 'BASE TABLE' ORDER BY 1"
        )
        return [r[0] for r in cur.fetchall()]


@mcp.tool()
def describe_table(table: str) -> dict:
    """Return the column names and data types of a table."""
    return get_table_columns(table)


@mcp.tool()
def fetch_rows(table: str, limit: int = 10) -> list[dict]:
    """Fetch the top N rows from a table (max 100)."""
    get_table_columns(table)  # validates the table exists
    limit = max(1, min(limit, 100))
    with get_connection() as conn:
        cur = conn.cursor()
        cur.execute(f"SELECT TOP {limit} * FROM [{table}]")
        cols = [c[0] for c in cur.description]
        return [
            dict(zip(cols, [str(v) if v is not None else None for v in row]))
            for row in cur.fetchall()
        ]


@mcp.tool()
def table_statistics(table: str) -> dict:
    """Row count plus per-column stats (nulls, distinct, min, max, avg for numbers)."""
    columns = get_table_columns(table)
    numeric_types = {"int", "bigint", "smallint", "tinyint", "decimal",
                     "numeric", "float", "real", "money", "smallmoney"}
    skip_types = {"text", "ntext", "image", "xml", "varbinary", "binary"}

    result = {"table": table, "columns": {}}
    with get_connection() as conn:
        cur = conn.cursor()
        cur.execute(f"SELECT COUNT(*) FROM [{table}]")
        result["row_count"] = cur.fetchone()[0]

        for col, dtype in columns.items():
            if dtype in skip_types:
                continue
            parts = [
                f"SUM(CASE WHEN [{col}] IS NULL THEN 1 ELSE 0 END)",
                f"COUNT(DISTINCT [{col}])",
                f"MIN([{col}])",
                f"MAX([{col}])",
            ]
            if dtype in numeric_types:
                parts.append(f"AVG(CAST([{col}] AS FLOAT))")
            cur.execute(f"SELECT {', '.join(parts)} FROM [{table}]")
            row = cur.fetchone()
            stats = {
                "data_type": dtype,
                "null_count": row[0],
                "distinct_count": row[1],
                "min": str(row[2]) if row[2] is not None else None,
                "max": str(row[3]) if row[3] is not None else None,
            }
            if dtype in numeric_types:
                stats["average"] = row[4]
            result["columns"][col] = stats
    return result


if __name__ == "__main__":
    mcp.run()