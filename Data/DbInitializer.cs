using gestao_producao.Models;
using Microsoft.AspNetCore.Identity;

namespace gestao_producao.Data;

public static class DbInitializer
{
    public static async Task SeedAdminAsync(IServiceProvider services, IConfiguration configuration)
    {
        using var scope = services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<UsuarioAdmin>>();

        var adminEmail = configuration["AdminUser:Email"] ?? "admin@gestaopro.com";
        var adminPassword = configuration["AdminUser:Password"];

        if (string.IsNullOrWhiteSpace(adminPassword))
        {
            return;
        }

        var existing = await userManager.FindByEmailAsync(adminEmail);
        if (existing is not null)
        {
            return;
        }

        var user = new UsuarioAdmin
        {
            UserName = adminEmail,
            Email = adminEmail,
            EmailConfirmed = true
        };

        await userManager.CreateAsync(user, adminPassword);
    }
}
