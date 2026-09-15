using Estoque.Api.Data;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Estoque.Api.Tests;

/// <summary>
/// Um PostgreSQL de verdade, descartavel, para os testes que dependem de algo que o provider
/// InMemory nao tem: indice unico que rejeita, xmin e transacao real.
///
/// O container sobe uma vez por execucao (subir custa segundos, seria inviavel por teste) e
/// morre no fim. O mesmo codigo roda na maquina local e no CI, sem configuracao externa.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    // A imagem vai no construtor: o construtor sem parametro foi depreciado no Testcontainers 4.15.
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16").Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        // Migrations de verdade, nao EnsureCreated: assim a suite tambem verifica que as
        // migrations do projeto aplicam sem erro, coisa que hoje nada testa.
        await using var db = CriarContexto();
        await db.Database.MigrateAsync();
    }

    /// <summary>
    /// Um contexto NOVO a cada chamada, de proposito. Em producao cada requisicao recebe o seu;
    /// um teste que compartilha contexto entre arranjo, acao e verificacao le o rastreador de
    /// mudancas em vez do banco, e a assercao de "foi persistido" passa mesmo sem persistir.
    /// </summary>
    public EstoqueDbContext CriarContexto() =>
        new(new DbContextOptionsBuilder<EstoqueDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options);

    /// <summary>Zera as tabelas entre testes, para que a ordem de execucao nunca importe.</summary>
    public async Task LimparAsync()
    {
        await using var db = CriarContexto();
        await db.Database.ExecuteSqlRawAsync(
            """
            TRUNCATE "MovimentacoesEstoqueItens", "MovimentacoesEstoque", "Produtos"
            RESTART IDENTITY CASCADE;
            """);
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

/// <summary>
/// Faz o xUnit criar UMA fixture para todos os testes marcados com esta colecao — e rodar esses
/// testes em sequencia, que e o que permite limpar o banco entre eles sem corrida.
/// </summary>
[CollectionDefinition(Nome)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Nome = "postgres";
}
