using gestao_producao.Data;
using gestao_producao.Models;
using gestao_producao.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
QuestPDF.Settings.License = LicenseType.Community;

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' não encontrada.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddHttpContextAccessor();
builder.Services.Configure<AlertaEstoqueOptions>(builder.Configuration.GetSection(AlertaEstoqueOptions.SectionName));
builder.Services.AddScoped<EstoqueService>();
builder.Services.AddScoped<InsumoService>();
builder.Services.AddScoped<RastreabilidadeInsumoService>();
builder.Services.AddScoped<ProducaoService>();
builder.Services.AddScoped<AlertaService>();
builder.Services.AddScoped<RelatorioService>();
builder.Services.AddHostedService<AlertaBackgroundService>();

builder.Services
    .AddDefaultIdentity<UsuarioAdmin>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequiredLength = 8;
    })
    .AddEntityFrameworkStores<AppDbContext>();

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AllowAnonymousToPage("/Index");
    options.Conventions.AllowAnonymousToPage("/Error");
    options.Conventions.AllowAnonymousToPage("/Privacy");
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

await DbInitializer.SeedAdminAsync(app.Services, app.Configuration);

app.Run();
