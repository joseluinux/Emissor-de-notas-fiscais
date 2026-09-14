using Estoque.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Estoque.Api.Data;

/// <summary>
/// Stock schema. This service owns it alone — Faturamento never reads these tables, only the
/// HTTP API in front of them.
/// </summary>
public class EstoqueDbContext(DbContextOptions<EstoqueDbContext> options) : DbContext(options)
{
    public DbSet<Produto> Produtos => Set<Produto>();

    public DbSet<MovimentacaoEstoque> MovimentacoesEstoque => Set<MovimentacaoEstoque>();

    public DbSet<MovimentacaoEstoqueItem> MovimentacoesEstoqueItens => Set<MovimentacaoEstoqueItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Produto>(entity =>
        {
            entity.ToTable("Produtos");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Codigo).IsRequired().HasMaxLength(40);

            // Codigo is the key other services address products by, so uniqueness is enforced by
            // the database and not only by the check in ProdutosController.Criar.
            entity.HasIndex(p => p.Codigo).IsUnique();

            entity.Property(p => p.Descricao).IsRequired().HasMaxLength(200);
            entity.Property(p => p.Saldo).IsRequired();

            // Npgsql maps a uint row version onto PostgreSQL's xmin system column, so no column of
            // our own is added. Two debits touching the same Produto then collide on SaveChanges
            // (DbUpdateConcurrencyException) instead of one silently overwriting the other's Saldo.
            entity.Property(p => p.Version).IsRowVersion();
        });

        modelBuilder.Entity<MovimentacaoEstoque>(entity =>
        {
            entity.ToTable("MovimentacoesEstoque");
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Referencia).IsRequired().HasMaxLength(80);

            // This index is the actual idempotency guarantee: even if two identical calls race past
            // the lookup in the service, only one row can exist for a given Referencia.
            entity.HasIndex(m => m.Referencia).IsUnique();

            entity.Property(m => m.CriadaEm).IsRequired();

            entity.HasMany(m => m.Itens)
                  .WithOne()
                  .HasForeignKey(i => i.MovimentacaoEstoqueId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MovimentacaoEstoqueItem>(entity =>
        {
            entity.ToTable("MovimentacoesEstoqueItens");
            entity.HasKey(i => i.Id);
            entity.Property(i => i.ProdutoCodigo).IsRequired().HasMaxLength(40);
            entity.Property(i => i.Quantidade).IsRequired();
            entity.Property(i => i.SaldoResultante).IsRequired();

            // TODO(revisar): why Restrict here while the movement's own items cascade? It blocks
            // deleting a Produto that has movement history, but there is no delete path for
            // produtos today, so I could not confirm whether protecting the audit trail was the
            // intent or whether Restrict is just the safer default.
            entity.HasOne<Produto>()
                  .WithMany()
                  .HasForeignKey(i => i.ProdutoId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
