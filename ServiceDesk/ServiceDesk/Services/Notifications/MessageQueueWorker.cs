using Microsoft.Extensions.Options;
using ServiceDesk.Services.Messaging;

namespace ServiceDesk.Services.Notifications;

// E-posta / SMS kuyruk worker'ı. Önceden gönderim her controller'da
// SaveChanges'tan hemen sonra istek içinde yapılıyordu: yavaş bir SMTP
// sunucusu kullanıcıyı bekletiyor, hata olursa mesaj bir sonraki olaya kadar
// takılı kalıyordu. Artık gönderim tek bir arka plan servisinde:
//   • controller kaydeder ve MessageQueueSignal ile worker'ı uyandırır,
//   • sinyal gelmezse de PollSeconds'ta bir kuyruğa bakılır
//     (yeniden deneme zamanı gelen kayıtlar bu sayede gönderilir).
public class MessageQueueWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly MessageQueueSignal _signal;
    private readonly QueueSettings _cfg;
    private readonly ILogger<MessageQueueWorker> _log;

    public MessageQueueWorker(IServiceScopeFactory scopes, MessageQueueSignal signal,
                              IOptions<MessagingSettings> cfg, ILogger<MessageQueueWorker> log)
    {
        _scopes = scopes;
        _signal = signal;
        _cfg = cfg.Value.Queue;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var poll = TimeSpan.FromSeconds(Math.Max(1, _cfg.PollSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Dolu bir parti işlendiyse kuyrukta daha fazlası olabilir: beklemeden devam
                int processed;
                do
                {
                    using var scope = _scopes.CreateScope();
                    var dispatcher = scope.ServiceProvider.GetRequiredService<NotificationDispatcher>();
                    processed = await dispatcher.DeliverDueAsync(stoppingToken);
                }
                while (processed >= _cfg.BatchSize && !stoppingToken.IsCancellationRequested);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Mesaj kuyruğu işlenemedi; sonraki turda tekrar denenecek");
            }

            await _signal.WaitAsync(poll, stoppingToken);
        }
    }
}
