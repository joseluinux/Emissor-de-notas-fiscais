using Faturamento.Api.Data;
using Faturamento.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Faturamento")));

builder.Services.AddControllers();

// Cliente tipado para o Estoque. De proposito sem retry, sem circuit breaker e sem timeout
// proprio: tratar a indisponibilidade do Estoque e o requisito obrigatorio 2 e esta adiado.
var estoqueBaseUrl = builder.Configuration["Servicos:Estoque:BaseUrl"]
    ?? throw new InvalidOperationException(
        "Configuracao 'Servicos:Estoque:BaseUrl' ausente. Veja appsettings.Development.json.");

builder.Services.AddHttpClient<IEstoqueClient, EstoqueClient>(client =>
    client.BaseAddress = new Uri(estoqueBaseUrl));

// ProblemDetails + handler antes de tudo, para que nenhum controller precise inventar
// um corpo de erro proprio.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DominioExceptionHandler>();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseExceptionHandler();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();

app.Run();
