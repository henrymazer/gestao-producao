using System.Text.Json;
using gestao_producao.Data;
using gestao_producao.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace gestao_producao.Tests;

public class AppDbContextAuditoriaTests
{
    [Fact]
    public async Task SaveChangesAsync_CreateUpdateDelete_DeveGerarAuditoriaSemRecursao()
    {
        await using var connection = CriarConexaoEmMemoria();
        await using var context = CriarContexto(connection);

        var fornecedor = new Fornecedor
        {
            Nome = "Fornecedor 1",
            Cnpj = "11111111000199"
        };

        context.Fornecedores.Add(fornecedor);
        await context.SaveChangesAsync();

        fornecedor.Nome = "Fornecedor 1 Atualizado";
        await context.SaveChangesAsync();

        context.Fornecedores.Remove(fornecedor);
        await context.SaveChangesAsync();

        var auditorias = await context.HistoricosAuditoria
            .Where(x => x.Entidade == nameof(Fornecedor))
            .OrderBy(x => x.Id)
            .ToListAsync();

        Assert.Equal(3, auditorias.Count);

        var criacao = Assert.Single(auditorias, a => a.Acao == EntityState.Added.ToString());
        Assert.Equal(fornecedor.Id.ToString(), criacao.EntidadeId);
        Assert.Null(criacao.ValoresAnteriores);
        Assert.NotNull(criacao.ValoresNovos);

        var atualizacao = Assert.Single(auditorias, a => a.Acao == EntityState.Modified.ToString());
        Assert.NotNull(atualizacao.ValoresAnteriores);
        Assert.NotNull(atualizacao.ValoresNovos);

        var exclusao = Assert.Single(auditorias, a => a.Acao == EntityState.Deleted.ToString());
        Assert.NotNull(exclusao.ValoresAnteriores);
        Assert.Null(exclusao.ValoresNovos);

        Assert.DoesNotContain(context.HistoricosAuditoria, h => h.Entidade == nameof(HistoricoAuditoria));
    }

    [Fact]
    public async Task SaveChangesAsync_QuandoFalharPersistenciaAuditoria_DeveFazerRollback()
    {
        await using var connection = CriarConexaoEmMemoria();
        var interceptor = new FalhaAoPersistirAuditoriaInterceptor();

        await using (var context = CriarContexto(connection, interceptor))
        {
            context.Fornecedores.Add(new Fornecedor
            {
                Nome = "Fornecedor com erro",
                Cnpj = "22222222000199"
            });

            await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
        }

        await using (var validacao = CriarContexto(connection))
        {
            Assert.Equal(0, await validacao.Fornecedores.CountAsync());
            Assert.Equal(0, await validacao.HistoricosAuditoria.CountAsync());
        }
    }

    [Fact]
    public async Task SaveChanges_EntidadeComChaveComposta_DeveSerializarIdentificadorComMetadadosDoEf()
    {
        await using var connection = CriarConexaoEmMemoria();
        await using var context = CriarContextoComChaveComposta(connection);

        context.EntidadesChaveComposta.Add(new EntidadeChaveComposta
        {
            CodigoA = "A1",
            CodigoB = "B1",
            Nome = "Composta"
        });

        context.SaveChanges();

        var auditoria = Assert.Single(context.HistoricosAuditoria.Where(x => x.Entidade == nameof(EntidadeChaveComposta)));
        Assert.Equal(EntityState.Added.ToString(), auditoria.Acao);

        using var document = JsonDocument.Parse(auditoria.EntidadeId);
        Assert.Equal("A1", document.RootElement.GetProperty(nameof(EntidadeChaveComposta.CodigoA)).GetString());
        Assert.Equal("B1", document.RootElement.GetProperty(nameof(EntidadeChaveComposta.CodigoB)).GetString());
    }

    private static AppDbContext CriarContexto(SqliteConnection connection, params IInterceptor[] interceptors)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection);

        if (interceptors.Length > 0)
        {
            optionsBuilder.AddInterceptors(interceptors);
        }

        var context = new AppDbContext(optionsBuilder.Options, CriarHttpContextAccessor());
        context.Database.EnsureCreated();

        return context;
    }

    private static ContextoComChaveComposta CriarContextoComChaveComposta(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new ContextoComChaveComposta(options, CriarHttpContextAccessor());
        context.Database.EnsureCreated();

        return context;
    }

    private static HttpContextAccessor CriarHttpContextAccessor()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
                [new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, "usuario-teste")],
                authenticationType: "TestAuth"));

        return new HttpContextAccessor { HttpContext = httpContext };
    }

    private static SqliteConnection CriarConexaoEmMemoria()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        return connection;
    }

    private sealed class FalhaAoPersistirAuditoriaInterceptor : SaveChangesInterceptor
    {
        private static void ValidarFalhaAuditoria(DbContext? context)
        {
            if (context is null)
            {
                return;
            }

            var possuiAuditoriaPendente = context.ChangeTracker
                .Entries<HistoricoAuditoria>()
                .Any(entry => entry.State == EntityState.Added);

            if (possuiAuditoriaPendente)
            {
                throw new InvalidOperationException("Falha simulada ao persistir auditoria.");
            }
        }

        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            ValidarFalhaAuditoria(eventData.Context);
            return result;
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            ValidarFalhaAuditoria(eventData.Context);
            return ValueTask.FromResult(result);
        }
    }

    private sealed class ContextoComChaveComposta : AppDbContext
    {
        public ContextoComChaveComposta(DbContextOptions<AppDbContext> options, IHttpContextAccessor httpContextAccessor)
            : base(options, httpContextAccessor)
        {
        }

        public DbSet<EntidadeChaveComposta> EntidadesChaveComposta => Set<EntidadeChaveComposta>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<EntidadeChaveComposta>(entity =>
            {
                entity.HasKey(x => new { x.CodigoA, x.CodigoB });
            });
        }
    }

    private sealed class EntidadeChaveComposta : EntidadeBase
    {
        public string CodigoA { get; set; } = string.Empty;
        public string CodigoB { get; set; } = string.Empty;
        public string Nome { get; set; } = string.Empty;
    }
}
