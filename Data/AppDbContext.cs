using System.Text.Json;
using gestao_producao.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace gestao_producao.Data;

public class AppDbContext : IdentityDbContext<UsuarioAdmin>
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Fornecedor> Fornecedores => Set<Fornecedor>();
    public DbSet<Insumo> Insumos => Set<Insumo>();
    public DbSet<Produto> Produtos => Set<Produto>();
    public DbSet<ProdutoInsumo> ProdutoInsumos => Set<ProdutoInsumo>();
    public DbSet<Estoque> Estoques => Set<Estoque>();
    public DbSet<MovimentacaoEstoque> MovimentacoesEstoque => Set<MovimentacaoEstoque>();
    public DbSet<Equipamento> Equipamentos => Set<Equipamento>();
    public DbSet<Funcionario> Funcionarios => Set<Funcionario>();
    public DbSet<OrdemProducao> OrdensProducao => Set<OrdemProducao>();
    public DbSet<PlanoProducao> PlanosProducao => Set<PlanoProducao>();
    public DbSet<PlanoProducaoItem> PlanosProducaoItens => Set<PlanoProducaoItem>();
    public DbSet<AlertaEstoque> AlertasEstoque => Set<AlertaEstoque>();
    public DbSet<HistoricoAuditoria> HistoricosAuditoria => Set<HistoricoAuditoria>();

    public override int SaveChanges()
    {
        AplicarTimestamps();
        RegistrarAuditoria();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        AplicarTimestamps();
        RegistrarAuditoria();
        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Fornecedor>().HasIndex(x => x.Cnpj).IsUnique();
        builder.Entity<Produto>().HasIndex(x => x.Codigo).IsUnique();
        builder.Entity<OrdemProducao>().HasIndex(x => x.Codigo).IsUnique();

        builder.Entity<Insumo>()
            .HasOne(x => x.Fornecedor)
            .WithMany(x => x.Insumos)
            .HasForeignKey(x => x.FornecedorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ProdutoInsumo>()
            .HasOne(x => x.Produto)
            .WithMany(x => x.Insumos)
            .HasForeignKey(x => x.ProdutoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ProdutoInsumo>()
            .HasOne(x => x.Insumo)
            .WithMany(x => x.Produtos)
            .HasForeignKey(x => x.InsumoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Estoque>()
            .HasOne(x => x.Insumo)
            .WithMany(x => x.Estoques)
            .HasForeignKey(x => x.InsumoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Estoque>()
            .HasOne(x => x.Produto)
            .WithMany(x => x.Estoques)
            .HasForeignKey(x => x.ProdutoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<MovimentacaoEstoque>()
            .HasOne(x => x.Estoque)
            .WithMany(x => x.Movimentacoes)
            .HasForeignKey(x => x.EstoqueId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<OrdemProducao>()
            .HasOne(x => x.Produto)
            .WithMany(x => x.OrdensProducao)
            .HasForeignKey(x => x.ProdutoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<OrdemProducao>()
            .HasOne(x => x.Equipamento)
            .WithMany(x => x.OrdensProducao)
            .HasForeignKey(x => x.EquipamentoId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<PlanoProducaoItem>()
            .HasOne(x => x.PlanoProducao)
            .WithMany(x => x.Itens)
            .HasForeignKey(x => x.PlanoProducaoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<PlanoProducaoItem>()
            .HasOne(x => x.OrdemProducao)
            .WithMany(x => x.PlanosProducao)
            .HasForeignKey(x => x.OrdemProducaoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<AlertaEstoque>()
            .HasOne(x => x.Estoque)
            .WithMany(x => x.Alertas)
            .HasForeignKey(x => x.EstoqueId)
            .OnDelete(DeleteBehavior.Cascade);

        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(decimal) || property.ClrType == typeof(decimal?))
                {
                    var isMonetary = property.Name.Contains("Preco", StringComparison.OrdinalIgnoreCase)
                        || property.Name.Contains("Valor", StringComparison.OrdinalIgnoreCase)
                        || property.Name.Contains("Custo", StringComparison.OrdinalIgnoreCase);

                    property.SetPrecision(isMonetary ? 18 : 18);
                    property.SetScale(isMonetary ? 2 : 4);
                }
            }
        }
    }

    private void AplicarTimestamps()
    {
        var entries = ChangeTracker
            .Entries<EntidadeBase>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified);

        var utcNow = DateTime.UtcNow;

        foreach (var entry in entries)
        {
            entry.Entity.AtualizadoEm = utcNow;

            if (entry.State == EntityState.Added)
            {
                entry.Entity.CriadoEm = utcNow;
            }
        }
    }

    private void RegistrarAuditoria()
    {
        var auditEntries = ChangeTracker
            .Entries()
            .Where(e => e.Entity is not HistoricoAuditoria
                && e.Entity is EntidadeBase
                && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        if (auditEntries.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;

        foreach (var entry in auditEntries)
        {
            var idValue = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "Id")?.CurrentValue?.ToString() ?? string.Empty;
            var antigo = entry.State == EntityState.Modified || entry.State == EntityState.Deleted
                ? SerializarValores(entry.OriginalValues)
                : null;
            var novo = entry.State == EntityState.Modified || entry.State == EntityState.Added
                ? SerializarValores(entry.CurrentValues)
                : null;

            HistoricosAuditoria.Add(new HistoricoAuditoria
            {
                Entidade = entry.Metadata.ClrType.Name,
                EntidadeId = idValue,
                Acao = entry.State.ToString(),
                ValoresAnteriores = antigo,
                ValoresNovos = novo,
                CriadoEm = now,
                AtualizadoEm = now
            });
        }
    }

    private static string SerializarValores(PropertyValues values)
    {
        var dict = values.Properties.ToDictionary(
            p => p.Name,
            p => values[p]);

        return JsonSerializer.Serialize(dict);
    }
}
