using Faturamento.Api.Data;
using Faturamento.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Faturamento")));

builder.Services.AddControllers();

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
