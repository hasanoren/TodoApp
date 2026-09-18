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
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1); // Geliştirme için 1 dakika (Canlıda 1 saat olabilir)

    public TodoReminderService(ILogger<TodoReminderService> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Hatırlatıcı Servisi başlatıldı.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndSendRemindersAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Hatırlatıcı servisi çalışırken bir hata oluştu.");
            }

            // Bekleme süresi (1 dakika)
            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("Hatırlatıcı Servisi durduruldu.");
    }

    private async Task CheckAndSendRemindersAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

        var targetDate = DateTime.UtcNow.AddHours(24);

        // Şartlar: Silinmemiş, Tamamlanmamış, Son 24 Saati kalmış ve henüz hatırlatıcı gönderilmemiş görevler
        var approachingTasks = await context.TodoItems
            .Include(t => t.Owner)
            .Where(t => !t.IsDeleted &&
                        t.Status != TodoApp.Domain.Entities.TodoItemStatus.Completed &&
                        t.DueDate.HasValue &&
                        t.DueDate.Value <= targetDate &&
                        t.ReminderSentAt == null)
            .ToListAsync(cancellationToken);

        if (!approachingTasks.Any())
        {
            return; // İşlem yapılacak görev yok
        }

        _logger.LogInformation("{Count} adet görev için hatırlatıcı gönderilecek.", approachingTasks.Count);

        foreach (var task in approachingTasks)
        {
            if (cancellationToken.IsCancellationRequested) break;

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
            _logger.LogInformation("Görev '{Title}' ({Id}) için {Email} adresine hatırlatıcı gönderildi.", task.Title, task.Id, task.Owner.Email);
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
