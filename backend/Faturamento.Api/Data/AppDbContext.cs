using Faturamento.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Faturamento.Api.Data;

/// <summary>
/// Billing schema. It holds no foreign key into Estoque — an item carries the product code plus a
/// snapshot of the description, so this service never depends on the other one's tables.
/// </summary>
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

            // The sequential number comes from the database itself (nextval), never from
            // MAX(Numero)+1 in application code: two simultaneous creations must not get the same
            // Numero. ValueGeneratedOnAdd is what makes EF read the generated value back after the
            // INSERT instead of trying to supply one.
            nota.Property(n => n.Numero)
                .HasDefaultValueSql($"nextval('\"{NumeroSequence}\"')")
                .ValueGeneratedOnAdd();

            nota.HasIndex(n => n.Numero).IsUnique();

            // Status is stored as text rather than as the enum's ordinal, so reordering or
            // inserting members of StatusNotaFiscal cannot silently remap rows already on disk.
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

            // TODO(revisar): 50 here against Estoque's 40 for the same business key. A code of 41
            // to 50 characters is accepted on an invoice and then rejected by Estoque at print
            // time. Was the wider column intentional, or should the two services agree on 40?
            item.Property(i => i.ProdutoCodigo).HasMaxLength(50).IsRequired();

            item.Property(i => i.Descricao).HasMaxLength(200).IsRequired();
            item.Property(i => i.Quantidade).IsRequired();
        });
    }
}
