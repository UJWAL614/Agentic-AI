
using McpServerDotNet.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using McpServerDotNet.Data;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Step 1: create the host. It automatically loads appsettings.json

var builder = Host.CreateApplicationBuilder(args);

// Step 2: send all logs to stderr. Standard output is reserved for the MCP protocol,
// so anything printed there would break the connection.

builder.Logging.AddConsole(options =>{options.LogToStandardErrorThreshold = LogLevel.Information;});

// Step 3: bind the "Database" section of appsettings.json to DatabaseSettings.
builder.Services.Configure<DatabaseSettings>(
    builder.Configuration.GetSection(DatabaseSettings.SectionName));
// Step 4: register our data classes so they can be injected into the tools.

builder.Services.AddSingleton<SqlConnectionFactory>();
builder.Services.AddSingleton<SchemaRepository>();
builder.Services.AddSingleton<TableRepository>();

// Step 5: register the MCP server, use stdio, and find every [McpServerToolType] class.    

 
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();
// Step 6: start and keep running until the client disconnects.
await builder.Build().RunAsync();