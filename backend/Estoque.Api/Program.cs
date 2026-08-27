using Estoque.Api.Data;
using Estoque.Api.Errors;
using Estoque.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSwaggerGen();

var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException(
        "Connection string 'Postgres' não configurada. Veja appsettings.Development.json.");

builder.Services.AddDbContext<EstoqueDbContext>(options => options.UseNpgsql(connectionString));

builder.Services.AddScoped<MovimentacaoEstoqueService>();

// Every failure leaves this API as RFC 7807 ProblemDetails. Order matters: the domain handler
// declines anything it does not recognise, and the unhandled one is the backstop.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddExceptionHandler<UnhandledExceptionHandler>();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.Run();
