using Billing.Api.Data;
using Billing.Api.Features.Invoices;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Persistência: banco PostgreSQL próprio do serviço de Faturamento (billing_db).
// Connection string configurada via appsettings ou variável de ambiente
// ConnectionStrings__BillingDb, sem segredos versionados.
var billingConnectionString = builder.Configuration.GetConnectionString("BillingDb")
    ?? throw new InvalidOperationException("Connection string 'BillingDb' not configured.");

builder.Services.AddDbContext<BillingDbContext>(options =>
    options.UseNpgsql(billingConnectionString));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Billing.Api",
        Version = "v1",
        Description = "Microsserviço de faturamento: notas fiscais e fluxo de impressão/fechamento.",
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

builder.Services.AddScoped<IInvoiceService, InvoiceService>();

// Cliente HTTP resiliente entre Faturamento e Estoque (docs/architecture.md):
// usado para validar produtos/capturar o snapshot de código-descrição ao
// criar uma nota, e para solicitar a baixa atômica de estoque ao imprimir.
// A resiliência (timeout total/por tentativa, retry com backoff exponencial
// e jitter, circuit breaker) é aplicada de forma idêntica aos dois clientes
// por InventoryResiliencePipeline.AddInventoryResilience (Task 11); valores
// externalizados na seção "InventoryApi:Resilience" (ver appsettings.json e
// docs/technical-details.md).
var inventoryApiBaseUrl = builder.Configuration["InventoryApi:BaseUrl"]
    ?? throw new InvalidOperationException("Configuration 'InventoryApi:BaseUrl' not set.");

var inventoryResilienceOptions = builder.Configuration
    .GetSection("InventoryApi:Resilience")
    .Get<InventoryResilienceOptions>() ?? new InventoryResilienceOptions();

// HttpClient.Timeout is a coarse safety net, deliberately set well above the
// resilience pipeline's own total timeout below: the pipeline (Polly, via
// AddInventoryResilience) is the real, fine-grained timeout mechanism
// (per-attempt + total), and this larger backstop only guards against the
// pipeline itself somehow failing to enforce its timeout, without ever
// racing/conflicting with it. See docs/technical-details.md.
var httpClientSafetyTimeout = TimeSpan.FromSeconds(
    inventoryResilienceOptions.TotalTimeoutSeconds + inventoryResilienceOptions.SafetyTimeoutMarginSeconds);

builder.Services.AddHttpClient<IInventoryProductClient, InventoryProductClient>(client =>
{
    client.BaseAddress = new Uri(inventoryApiBaseUrl);
    client.Timeout = httpClientSafetyTimeout;
}).AddInventoryResilience(inventoryResilienceOptions);

// Dedicated client for the atomic stock debit call used by the print/close
// flow (docs/architecture.md "Fluxo de impressão"). Same target service,
// timeout backstop, and resilience pipeline as the product lookup client
// above, kept separate because it has a distinct responsibility (write-off
// vs. read-only lookup).
builder.Services.AddHttpClient<IInventoryStockClient, InventoryStockClient>(client =>
{
    client.BaseAddress = new Uri(inventoryApiBaseUrl);
    client.Timeout = httpClientSafetyTimeout;
}).AddInventoryResilience(inventoryResilienceOptions);

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
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Billing.Api v1");
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
