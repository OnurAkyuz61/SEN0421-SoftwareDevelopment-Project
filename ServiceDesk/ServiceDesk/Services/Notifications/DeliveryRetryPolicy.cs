namespace ServiceDesk.Services.Notifications;

// Başarısız e-posta / SMS gönderiminin yeniden deneme zamanlaması:
// üstel geri çekilme (60 sn, 120 sn, 240 sn … en fazla RetryMaxSeconds).
// MaxAttempts dolunca kayıt "başarısız" işaretlenip kuyruktan çıkar.
//
// Saf sınıf: birim testi yazılabilir.
public static class DeliveryRetryPolicy
{
    public sealed record Outcome(bool GiveUp, DateTime? NextAttemptAt);

    public static Outcome AfterFailure(int attemptCount, DateTime nowUtc,
                                       int maxAttempts, int baseSeconds, int maxSeconds)
    {
        if (attemptCount >= maxAttempts) return new Outcome(true, null);

        // attemptCount 1 → base, 2 → 2×base, 3 → 4×base …
        var exp = Math.Min(attemptCount - 1, 20);
        var delay = Math.Min((double)baseSeconds * Math.Pow(2, exp), maxSeconds);
        return new Outcome(false, nowUtc.AddSeconds(delay));
    }
}
