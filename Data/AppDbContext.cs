using System.Text.Json;
using gestao_producao.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
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
        AplicarTimestamps();
        var auditoriasPendentes = RegistrarAuditoriaPreSave();

        var result = base.SaveChanges();
        RegistrarAuditoriaPosSave(auditoriasPendentes);

        return result;
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        AplicarTimestamps();
        var auditoriasPendentes = RegistrarAuditoriaPreSave();

        var result = await base.SaveChangesAsync(cancellationToken);
        await RegistrarAuditoriaPosSaveAsync(auditoriasPendentes, cancellationToken);

        return result;
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
            var idProperty = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "Id");
            var idValue = idProperty?.CurrentValue?.ToString() ?? string.Empty;
            var idTemporario = entry.State == EntityState.Added && (idProperty?.IsTemporary ?? false);

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
            var idValue = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "Id")?.CurrentValue?.ToString() ?? string.Empty;

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

        base.SaveChanges();
    }

    private async Task RegistrarAuditoriaPosSaveAsync(IReadOnlyCollection<AuditoriaPendente> auditoriasPendentes, CancellationToken cancellationToken)
    {
        if (auditoriasPendentes.Count == 0)
        {
            return;
        }

        foreach (var pendente in auditoriasPendentes)
        {
            var entry = pendente.Entry;
            var idValue = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "Id")?.CurrentValue?.ToString() ?? string.Empty;

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

        await base.SaveChangesAsync(cancellationToken);
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

    private sealed record AuditoriaPendente(EntityEntry Entry, EntityState Estado, DateTime Timestamp, string? UsuarioId);
}
