using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TodoApp.Application.Interfaces;
using TodoApp.Domain.Enums;
using TodoApp.Infrastructure.Data;

namespace TodoApp.Api.BackgroundServices;

public class TodoReminderService : BackgroundService
{
    private readonly ILogger<TodoReminderService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1);

    public TodoReminderService(ILogger<TodoReminderService> logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Hatırlatıcı Servisi başlatıldı.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndSendRemindersAsync(stoppingToken);
                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Uygulama kapanırken fırlatılan normal iptal sinyali
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Hatırlatıcı servisi çalışırken bir hata oluştu.");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        _logger.LogInformation("Hatırlatıcı Servisi durduruldu.");
    }

    private async Task CheckAndSendRemindersAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

        var targetDate = DateTime.UtcNow.AddHours(24);

        const int batchSize = 100;
        int totalProcessed = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            // Şartlar: Silinmemiş, Tamamlanmamış, Son 24 Saati kalmış ve henüz hatırlatıcı gönderilmemiş görevler
            // Bellek şişmesini önlemek için batchSize (100'erli) gruplar halinde işlenir
            var batch = await context.TodoItems
                .Include(t => t.Owner)
                .Where(t => !t.IsDeleted &&
                            t.Status != TodoApp.Domain.Entities.TodoItemStatus.Completed &&
                            t.DueDate.HasValue &&
                            t.DueDate.Value <= targetDate &&
                            t.ReminderSentAt == null)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            if (batch.Count == 0)
            {
                break; // İşlenecek başka görev kalmadı
            }

            foreach (var task in batch)
            {
                if (cancellationToken.IsCancellationRequested) break;

                try
                {
                    var htmlBody = $"""
                    <p>Merhaba {task.Owner.Email},</p>
                    <p><strong>'{task.Title}'</strong> adlı görevinizin teslim tarihi yaklaşmaktadır.</p>
                    <p>Son Tarih: {task.DueDate:dd.MM.yyyy HH:mm}</p>
                    <p>Lütfen görevinizi zamanında tamamlamayı unutmayın.</p>
                    """;

                    await emailSender.SendEmailAsync(
                        task.Owner.Email,
                        $"Görev Hatırlatması: {task.Title}",
                        htmlBody);

                    task.ReminderSentAt = DateTime.UtcNow;
                    totalProcessed++;
                    _logger.LogInformation("Görev '{Title}' ({Id}) için {Email} adresine hatırlatıcı gönderildi.", task.Title, task.Id, task.Owner.Email);
                }
                catch (Exception ex)
                {
                    // Tek bir e-posta gönderim hatası diğer görevlerin hatırlatıcılarını veya batch'i durdurmasın
                    _logger.LogError(ex, "Görev '{Title}' ({Id}) için hatırlatıcı e-postası gönderilemedi.", task.Title, task.Id);
                }
            }

            // Her 100'lük batch tamamlandığında durumu veritabanına yansıt
            await context.SaveChangesAsync(cancellationToken);
        }

        if (totalProcessed > 0)
        {
            _logger.LogInformation("Toplam {Total} adet görev için hatırlatıcı başarıyla gönderildi.", totalProcessed);
        }
    }
}
