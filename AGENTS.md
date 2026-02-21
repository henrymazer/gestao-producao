# AGENTS.md

## Projeto
- Nome: `gestao-producao`
- Stack principal: ASP.NET Core Razor Pages (`.NET 10`) + Entity Framework Core + PostgreSQL (Neon)
- Idioma: Português BR na interface e termos de negócio; nomes técnicos em inglês quando aplicável
- Banco padrão: connection string `ConnectionStrings:DefaultConnection` via user-secrets/ambiente

## Arquitetura e Convenções
- `Data/AppDbContext.cs` contém o `DbContext`, mapeamentos e auditoria automática (`HistoricosAuditoria`)
- `Program.cs` registra `AppDbContext`, Identity (`UsuarioAdmin`) e services de domínio
- Migrations EF Core ficam em `Data/Migrations`
- Entidades seguem nomenclatura de negócio em português (ex.: `OrdemProducao`, `MovimentacaoEstoque`)

## Fluxo Operacional
- Antes de concluir mudanças: executar `dotnet build`
- Para mudanças de schema: criar migration e aplicar no banco alvo Neon
- Para validação de fase/checklist, sempre buscar evidência executável (comando + resultado)

## Build/Deploy (Cloud Run)
- Priorizar imagem de build Debian/Ubuntu para .NET SDK (ex.: `mcr.microsoft.com/dotnet/sdk:10.0`), evitar SDK Alpine no stage de build.
- No build da solução em CI/CD, usar modo single-node para estabilidade: `dotnet build gestao-producao.sln -m:1`.
- No publish para deploy, usar: `dotnet publish gestao-producao.csproj -c Release -m:1`.
- Manter `UseSharedCompilation=false` via `Directory.Build.props` para evitar dependência de compiler server em ambientes com restrição de IPC/pipe.

## Uso do psql (Neon)

### Instalação local
- Alpine: `apk add --no-cache postgresql-client`

### Carregar connection string dos user-secrets
- `dotnet user-secrets list`
- Chave esperada: `ConnectionStrings:DefaultConnection`

### Conectar ao Neon com a connection string do projeto
Use este bloco para converter o formato Npgsql (`Host=...;Port=...`) em parâmetros do `psql`:

```bash
CONN_STR="$(dotnet user-secrets list | sed -n 's/^ConnectionStrings:DefaultConnection = //p')"
HOST=$(echo "$CONN_STR" | tr ';' '\n' | sed -n 's/^Host=//p')
PORT=$(echo "$CONN_STR" | tr ';' '\n' | sed -n 's/^Port=//p')
DB=$(echo "$CONN_STR" | tr ';' '\n' | sed -n 's/^Database=//p')
USER=$(echo "$CONN_STR" | tr ';' '\n' | sed -n 's/^Username=//p')
PASS=$(echo "$CONN_STR" | tr ';' '\n' | sed -n 's/^Password=//p')
PGPASSWORD="$PASS" PGSSLMODE=require psql -h "$HOST" -p "$PORT" -U "$USER" -d "$DB"
```

### Comandos de verificação rápida
- Banco/usuário atual:
  - `select current_database(), current_user;`
- Histórico de migrations:
  - `select "MigrationId", "ProductVersion" from "__EFMigrationsHistory" order by "MigrationId";`
- Listar tabelas de negócio:
  - `\dt`

### Segurança
- Não versionar senha ou connection string completa em arquivos rastreados pelo Git
- Preferir `user-secrets` (local) e variáveis de ambiente (deploy)
