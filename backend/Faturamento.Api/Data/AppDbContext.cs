using Faturamento.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Faturamento.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public const string NumeroSequence = "NotaFiscalNumeroSeq";

    public DbSet<NotaFiscal> NotasFiscais => Set<NotaFiscal>();

    public DbSet<NotaFiscalItem> NotasFiscaisItens => Set<NotaFiscalItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasSequence<int>(NumeroSequence).StartsAt(1).IncrementsBy(1);

        modelBuilder.Entity<NotaFiscal>(nota =>
        {
            nota.HasKey(n => n.Id);

            // O sequencial sai do proprio banco (nextval), nunca de MAX(Numero)+1 na aplicacao:
            // duas criacoes simultaneas nao podem receber o mesmo Numero.
            nota.Property(n => n.Numero)
                .HasDefaultValueSql($"nextval('\"{NumeroSequence}\"')")
                .ValueGeneratedOnAdd();

            nota.HasIndex(n => n.Numero).IsUnique();

            nota.Property(n => n.Status)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            nota.Property(n => n.CriadaEm).IsRequired();

            nota.HasMany(n => n.Itens)
                .WithOne()
                .HasForeignKey(i => i.NotaFiscalId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<NotaFiscalItem>(item =>
        {
            item.HasKey(i => i.Id);
            item.Property(i => i.ProdutoCodigo).HasMaxLength(50).IsRequired();
            item.Property(i => i.Descricao).HasMaxLength(200).IsRequired();
            item.Property(i => i.Quantidade).IsRequired();
        });
    }
}
