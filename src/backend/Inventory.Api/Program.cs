using Inventory.Api.Data;
using Inventory.Api.Features.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Persistência: banco PostgreSQL próprio do serviço de Estoque (inventory_db).
// Connection string configurada via appsettings ou variável de ambiente
// ConnectionStrings__InventoryDb, sem segredos versionados.
var inventoryConnectionString = builder.Configuration.GetConnectionString("InventoryDb")
    ?? throw new InvalidOperationException("Connection string 'InventoryDb' not configured.");

builder.Services.AddDbContext<InventoryDbContext>(options =>
    options.UseNpgsql(inventoryConnectionString));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Inventory.Api",
        Version = "v1",
        Description = "Microsserviço de estoque: produtos, saldos e baixas.",
    });
});

// ProblemDetails para respostas de erro sem stack trace, com traceId de correlação.
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
    };
});

builder.Services.AddHealthChecks();

builder.Services.AddScoped<IProductService, ProductService>();

// CORS: origem permitida configurável via "Cors:AllowedOrigins" (appsettings ou
// variável de ambiente Cors__AllowedOrigins__0), sem AllowAnyOrigin/AllowCredentials.
// Em Development, se a seção não estiver configurada, assume http://localhost:4200
// (dev server padrão do Angular local).
const string FrontendCorsPolicy = "FrontendCors";

var configuredOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>();

var allowedOrigins = configuredOrigins is { Length: > 0 }
    ? configuredOrigins
    : builder.Environment.IsDevelopment()
        ? ["http://localhost:4200"]
        : [];

builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .WithMethods("GET", "POST", "OPTIONS")
            .WithHeaders("Content-Type");
    });
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Inventory.Api v1");
    });
}

app.UseCors(FrontendCorsPolicy);

app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

public partial class Program
{
}
