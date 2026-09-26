using Microsoft.EntityFrameworkCore;
using TodoApp.Infrastructure.Data;

namespace TodoApp.Api.Extensions;

public static class MigrationExtensions
{
    /// <summary>
    /// T12.3.2 & T11.1.3: Uygulama açılışında veritabanı şema kontrolünü ve bekleyen migration'ların uygulanmasını sağlar.
    /// Program.cs içerisindeki 20 satırlık scope açma ve hata yakalama bloğunu izole eder.
    /// </summary>
    public static async Task ApplyDatabaseMigrationsAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var dbContext = services.GetRequiredService<ApplicationDbContext>();

        if (dbContext.Database.IsSqlServer())
        {
            var logger = services.GetRequiredService<ILogger<Program>>();
            try
            {
                logger.LogInformation("Veritabanı migration kontrolü başlatılıyor...");
                await dbContext.Database.MigrateAsync();
                logger.LogInformation("Veritabanı güncel ve migration işlemi başarıyla tamamlandı.");
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "Veritabanı migration işlemi sırasında kritik hata oluştu: {Message}", ex.Message);
                throw;
            }
        }
    }
}
