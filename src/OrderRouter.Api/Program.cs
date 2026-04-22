using OrderRouter.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.PropertyNamingPolicy =
            System.Text.Json.JsonNamingPolicy.SnakeCaseLower;
        opts.JsonSerializerOptions.WriteIndented = true;
        opts.JsonSerializerOptions.DefaultIgnoreCondition =
            System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Data path: look for /app/Data in container, fall back to src directory for local dev
var dataDir = Path.Combine(AppContext.BaseDirectory, "Data");
if (!Directory.Exists(dataDir))
    dataDir = Path.Combine(Directory.GetCurrentDirectory(), "Data");

builder.Services.AddSingleton<IDataLoader>(_ => new CsvDataLoader(dataDir));
builder.Services.AddScoped<IRoutingEngine, RoutingEngine>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

// Eagerly load data on startup so errors surface immediately
var loader = app.Services.GetRequiredService<IDataLoader>();
Console.WriteLine($"[startup] Loaded {loader.Products.Count} products, {loader.Suppliers.Count} suppliers.");

app.MapControllers();
app.Run();
