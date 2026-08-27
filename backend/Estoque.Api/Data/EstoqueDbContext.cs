using Estoque.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Estoque.Api.Data;

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
            entity.HasIndex(p => p.Codigo).IsUnique();
            entity.Property(p => p.Descricao).IsRequired().HasMaxLength(200);
            entity.Property(p => p.Saldo).IsRequired();
            entity.Property(p => p.Version).IsRowVersion();
        });

        modelBuilder.Entity<MovimentacaoEstoque>(entity =>
        {
            entity.ToTable("MovimentacoesEstoque");
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Referencia).IsRequired().HasMaxLength(80);
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

            entity.HasOne<Produto>()
                  .WithMany()
                  .HasForeignKey(i => i.ProdutoId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
