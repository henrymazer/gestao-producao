using System.Text.Json;
using gestao_producao.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Security.Claims;

namespace gestao_producao.Data;

public class AppDbContext : IdentityDbContext<UsuarioAdmin>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AppDbContext(DbContextOptions<AppDbContext> options, IHttpContextAccessor httpContextAccessor)
        : base(options)
    {
        _httpContextAccessor = httpContextAccessor;
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
        return SaveChanges(acceptAllChangesOnSuccess: true);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        return ExecutarSaveChangesComAuditoria(acceptAllChangesOnSuccess);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await SaveChangesAsync(acceptAllChangesOnSuccess: true, cancellationToken);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        return ExecutarSaveChangesComAuditoriaAsync(acceptAllChangesOnSuccess, cancellationToken);
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
                    property.SetPrecision(18);
                    property.SetScale(4);
                }
            }
        }

        builder.Entity<Produto>()
            .Property(x => x.PrecoVenda)
            .HasPrecision(18, 2);

        builder.Entity<Insumo>()
            .Property(x => x.PrecoUnitario)
            .HasPrecision(18, 2);
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

    private int ExecutarSaveChangesComAuditoria(bool acceptAllChangesOnSuccess)
    {
        AplicarTimestamps();
        var auditoriasPendentes = RegistrarAuditoriaPreSave();

        var abriuTransacao = Database.CurrentTransaction is null;
        IDbContextTransaction? transacao = null;

        try
        {
            if (abriuTransacao)
            {
                transacao = Database.BeginTransaction();
            }

            var result = base.SaveChanges(acceptAllChangesOnSuccess);
            RegistrarAuditoriaPosSave(auditoriasPendentes);

            if (ChangeTracker.HasChanges())
            {
                base.SaveChanges(acceptAllChangesOnSuccess);
            }

            transacao?.Commit();
            return result;
        }
        catch
        {
            transacao?.Rollback();
            throw;
        }
        finally
        {
            transacao?.Dispose();
        }
    }

    private async Task<int> ExecutarSaveChangesComAuditoriaAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken)
    {
        AplicarTimestamps();
        var auditoriasPendentes = RegistrarAuditoriaPreSave();

        var abriuTransacao = Database.CurrentTransaction is null;
        IDbContextTransaction? transacao = null;

        try
        {
            if (abriuTransacao)
            {
                transacao = await Database.BeginTransactionAsync(cancellationToken);
            }

            var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
            RegistrarAuditoriaPosSave(auditoriasPendentes);

            if (ChangeTracker.HasChanges())
            {
                await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
            }

            if (transacao is not null)
            {
                await transacao.CommitAsync(cancellationToken);
            }

            return result;
        }
        catch
        {
            if (transacao is not null)
            {
                await transacao.RollbackAsync(cancellationToken);
            }

            throw;
        }
        finally
        {
            if (transacao is not null)
            {
                await transacao.DisposeAsync();
            }
        }
    }

    private List<AuditoriaPendente> RegistrarAuditoriaPreSave()
    {
        var auditEntries = ChangeTracker
            .Entries()
            .Where(e => e.Entity is not HistoricoAuditoria
                && e.Entity is EntidadeBase
                && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        if (auditEntries.Count == 0)
        {
            return [];
        }

        var now = DateTime.UtcNow;
        var usuarioId = ObterUsuarioIdAtual();
        var pendentes = new List<AuditoriaPendente>();

        foreach (var entry in auditEntries)
        {
            var chavePrimaria = entry.Metadata.FindPrimaryKey();
            var idValue = ObterIdentificadorEntidade(entry, chavePrimaria);
            var idTemporario = entry.State == EntityState.Added
                && (chavePrimaria?.Properties.Any(p => entry.Property(p.Name).IsTemporary) ?? false);

            var antigo = entry.State == EntityState.Modified || entry.State == EntityState.Deleted
                ? SerializarValores(entry.OriginalValues)
                : null;
            var novo = entry.State == EntityState.Modified || entry.State == EntityState.Added
                ? SerializarValores(entry.CurrentValues)
                : null;

            if (idTemporario)
            {
                pendentes.Add(new AuditoriaPendente(entry, entry.State, now, usuarioId));
                continue;
            }

            HistoricosAuditoria.Add(new HistoricoAuditoria
            {
                Entidade = entry.Metadata.ClrType.Name,
                EntidadeId = idValue,
                Acao = entry.State.ToString(),
                ValoresAnteriores = antigo,
                ValoresNovos = novo,
                UsuarioId = usuarioId,
                CriadoEm = now,
                AtualizadoEm = now
            });
        }

        return pendentes;
    }

    private void RegistrarAuditoriaPosSave(IReadOnlyCollection<AuditoriaPendente> auditoriasPendentes)
    {
        if (auditoriasPendentes.Count == 0)
        {
            return;
        }

        foreach (var pendente in auditoriasPendentes)
        {
            var entry = pendente.Entry;
            var idValue = ObterIdentificadorEntidade(entry, entry.Metadata.FindPrimaryKey());

            HistoricosAuditoria.Add(new HistoricoAuditoria
            {
                Entidade = entry.Metadata.ClrType.Name,
                EntidadeId = idValue,
                Acao = pendente.Estado.ToString(),
                ValoresAnteriores = null,
                ValoresNovos = SerializarValores(entry.CurrentValues),
                UsuarioId = pendente.UsuarioId,
                CriadoEm = pendente.Timestamp,
                AtualizadoEm = pendente.Timestamp
            });
        }
    }

    private string? ObterUsuarioIdAtual()
    {
        return _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    private static string SerializarValores(PropertyValues values)
    {
        var dict = values.Properties.ToDictionary(
            p => p.Name,
            p => values[p]);

        return JsonSerializer.Serialize(dict);
    }

    private static string ObterIdentificadorEntidade(EntityEntry entry, IKey? chavePrimaria)
    {
        if (chavePrimaria is null || chavePrimaria.Properties.Count == 0)
        {
            return string.Empty;
        }

        if (chavePrimaria.Properties.Count == 1)
        {
            var valor = ObterValorChavePrimaria(entry, chavePrimaria.Properties[0]);
            return valor?.ToString() ?? string.Empty;
        }

        var composto = chavePrimaria.Properties.ToDictionary(
            propriedade => propriedade.Name,
            propriedade => ObterValorChavePrimaria(entry, propriedade));

        return JsonSerializer.Serialize(composto);
    }

    private static object? ObterValorChavePrimaria(EntityEntry entry, IProperty propriedade)
    {
        var valor = entry.Property(propriedade.Name);
        return entry.State == EntityState.Deleted ? valor.OriginalValue : valor.CurrentValue;
    }

    private sealed record AuditoriaPendente(EntityEntry Entry, EntityState Estado, DateTime Timestamp, string? UsuarioId);
}
