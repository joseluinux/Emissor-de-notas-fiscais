using Faturamento.Api.Data;
using Faturamento.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Faturamento")));

builder.Services.AddControllers();

// Typed client for Estoque. Deliberately without retry, circuit breaker or a timeout of its own:
// handling Estoque being unavailable is mandatory requirement 2 and is deferred — see NOTES.md.
var estoqueBaseUrl = builder.Configuration["Servicos:Estoque:BaseUrl"]
    ?? throw new InvalidOperationException(
        "Configuracao 'Servicos:Estoque:BaseUrl' ausente. Veja appsettings.Development.json.");

builder.Services.AddHttpClient<IEstoqueClient, EstoqueClient>(client =>
    client.BaseAddress = new Uri(estoqueBaseUrl));

// ProblemDetails plus the handler ahead of everything else, so no controller has to invent an
// error body of its own.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DominioExceptionHandler>();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// TODO(revisar): nothing registers authentication or authorization in this service, and Estoque
// has no equivalent line. Is this a leftover from the project template, or a placeholder for auth
// that is planned? I could not tell from the code, NOTES.md or CLAUDE.md.
app.UseAuthorization();

app.MapControllers();

app.Run();
