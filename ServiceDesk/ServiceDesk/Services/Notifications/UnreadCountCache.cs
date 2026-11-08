using Microsoft.Extensions.Caching.Memory;

namespace ServiceDesk.Services.Notifications;

// Okunmamış bildirim sayısı her sayfada (zil) ve hafif API ucunda okunuyor;
// her seferinde COUNT sorgusu atmamak için kullanıcı başına önbelleğe alınır.
// Bildirim eklenince / okununca AppDbContext kayıttan hemen sonra Invalidate
// çağırır; yine de kaçan bir yol olursa kısa ömür (TTL) bayatlığı sınırlar.
public class UnreadCountCache
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);
    private readonly IMemoryCache _cache;

    public UnreadCountCache(IMemoryCache cache) => _cache = cache;

    private static string Key(int userId) => $"unread:{userId}";

    public async Task<int> GetOrAddAsync(int userId, Func<Task<int>> factory)
    {
        if (_cache.TryGetValue(Key(userId), out int count)) return count;

        count = await factory();
        _cache.Set(Key(userId), count, Ttl);
        return count;
    }

    public void Invalidate(int userId) => _cache.Remove(Key(userId));
}

// Kuyruk worker'ını beklemeden uyandırmak için sinyal. Bildirim kaydedildikten
// sonra controller Notify() çağırır; worker bir sonraki periyodu beklemez.
public class MessageQueueSignal
{
    private readonly SemaphoreSlim _signal = new(0, 1);

    public void Notify()
    {
        // Zaten bekleyen bir sinyal varsa ikinciyi biriktirme
        if (_signal.CurrentCount == 0)
        {
            try { _signal.Release(); } catch (SemaphoreFullException) { }
        }
    }

    public async Task WaitAsync(TimeSpan timeout, CancellationToken token)
    {
        try { await _signal.WaitAsync(timeout, token); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }
}
