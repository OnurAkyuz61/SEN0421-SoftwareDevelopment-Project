using Microsoft.EntityFrameworkCore;
using ServiceDesk.Models;
using ServiceDesk.Services;

namespace ServiceDesk.Data;

// ───── GELİŞTİRME KOLAYLIĞI ───────────────────────────────────────────────
// Panelin, grafiklerin ve raporların dolu görünmesi için gerçekçi örnek veri
// üretir. YALNIZCA Development ortamında ve veritabanında yeterli talep yoksa
// çalışır; mevcut veriye dokunmaz. Canlıya çıkmadan önce bu dosya ve
// Program.cs'teki çağrısı silinecek.
public static class DevDataSeeder
{
    // Bu sayının altındaysa örnek veri üretilir
    private const int MinTickets = 15;

    private static readonly Random Rnd = new(20261016); // tekrarlanabilir olsun

    public static async Task SeedAsync(AppDbContext db, ILogger logger)
    {
        await SeedDemoUsersAsync(db, logger);

        if (await db.Tickets.CountAsync() >= MinTickets)
        {
            logger.LogInformation("Örnek veri atlandı: veritabanında zaten yeterli talep var.");
            return;
        }

        var users = await db.Users.Where(u => u.IsActive).ToListAsync();
        var requesters = users.Where(u => u.Role == UserRole.User).ToList();
        var staff = users.Where(u => u.Role != UserRole.User).ToList();
        var categories = await db.Categories.Where(c => c.IsActive).ToListAsync();
        var policies = await db.SlaPolicies.Where(p => p.IsActive).ToListAsync();
        var questions = await db.SurveyQuestions.Where(q => q.IsActive).OrderBy(q => q.SortOrder).ToListAsync();

        if (requesters.Count == 0 || staff.Count == 0 || categories.Count == 0 || policies.Count == 0)
        {
            logger.LogWarning("Örnek veri üretilemedi: temel kayıtlar (kullanıcı/kategori/politika) eksik.");
            return;
        }

        await SeedArticlesAsync(db, staff, categories);

        var konular = new (string Baslik, string Aciklama, string Kategori, Priority Oncelik, TicketType Tur)[]
        {
            ("Bilgisayar açılmıyor", "Sabah geldiğimde bilgisayar güç vermiyor, ışık yanmıyor.", "Donanım", Priority.Critical, TicketType.Incident),
            ("Yazıcı kağıt sıkıştırıyor", "3. kattaki yazıcı sürekli kağıt sıkıştırıyor.", "Donanım", Priority.Medium, TicketType.Incident),
            ("Monitör titriyor", "Ekran görüntüsü düzensiz titriyor, kablo değiştirdim geçmedi.", "Donanım", Priority.Low, TicketType.Incident),
            ("Excel dosyası açılmıyor", "Paylaşılan klasördeki dosya bozuk diyor.", "Yazılım", Priority.High, TicketType.Incident),
            ("Muhasebe programı donuyor", "Rapor alırken program yanıt vermiyor.", "Yazılım", Priority.High, TicketType.Incident),
            ("Yeni yazılım lisansı", "Tasarım ekibi için lisans talebi.", "Yazılım", Priority.Medium, TicketType.Change),
            ("İnternet çok yavaş", "Öğleden sonra bağlantı neredeyse kullanılamıyor.", "Ağ / İnternet", Priority.High, TicketType.Incident),
            ("VPN bağlantısı kopuyor", "Evden bağlandığımda 10 dakikada bir düşüyor.", "Ağ / İnternet", Priority.Critical, TicketType.Incident),
            ("Toplantı odasında wifi yok", "B blok toplantı odasında sinyal alınmıyor.", "Ağ / İnternet", Priority.Medium, TicketType.Incident),
            ("Switch değişimi", "Kat switchinin yenisiyle değiştirilmesi.", "Ağ / İnternet", Priority.High, TicketType.Change),
            ("Şifremi unuttum", "Hesabıma giriş yapamıyorum.", "Hesap / Erişim", Priority.High, TicketType.Incident),
            ("Paylaşılan klasöre erişim", "Muhasebe klasörüne erişim yetkisi gerekiyor.", "Hesap / Erişim", Priority.Medium, TicketType.Change),
            ("Hesabım kilitlendi", "Üç kez yanlış şifre girdim, hesap kilitlendi.", "Hesap / Erişim", Priority.Critical, TicketType.Incident),
            ("E-postalar gelmiyor", "Dünden beri hiç e-posta almıyorum.", "E-posta", Priority.Critical, TicketType.Incident),
            ("Posta kutusu doldu", "Kota uyarısı alıyorum, alan artırımı gerekiyor.", "E-posta", Priority.Medium, TicketType.Change),
            ("Spam filtresi çalışmıyor", "Günde onlarca istenmeyen posta geliyor.", "E-posta", Priority.Low, TicketType.Incident),
            ("Klavye tuşları çalışmıyor", "Birkaç tuş basmıyor, değişim gerekli.", "Donanım", Priority.Low, TicketType.Incident),
            ("Ekran kartı sürücüsü", "Sürücü güncellemesinden sonra görüntü bozuldu.", "Yazılım", Priority.Medium, TicketType.Incident),
        };

        var yorumlar = new[]
        {
            "Konuyu inceliyoruz, en kısa sürede döneceğiz.",
            "Yerinde kontrol ettik, parça değişimi gerekiyor.",
            "Geçici çözüm uygulandı, kalıcı çözüm için planlama yapıldı.",
            "Sorunu tekrar üretemedik, tekrarlarsa bilgi verin lütfen.",
            "Ayarlar güncellendi, lütfen kontrol eder misiniz?"
        };

        var now = AppTime.UtcNow;
        var eklenen = new List<Ticket>();

        for (var i = 0; i < konular.Length; i++)
        {
            var k = konular[i];
            var kategori = categories.FirstOrDefault(c => c.Name == k.Kategori) ?? categories[0];
            var requester = requesters[i % requesters.Count];
            var politika = policies.FirstOrDefault(p => p.Priority == k.Oncelik) ?? policies[0];

            // Son 30 güne yayılmış açılış tarihleri
            var acilis = now.AddDays(-Rnd.Next(1, 30)).AddHours(-Rnd.Next(0, 10));

            var t = new Ticket
            {
                Title = k.Baslik,
                Description = k.Aciklama,
                Type = k.Tur,
                Priority = k.Oncelik,
                CategoryId = kategori.Id,
                AssignedTeamId = kategori.DefaultTeamId,
                RequesterId = requester.Id,
                RequesterName = requester.FullName,
                CreatedAt = acilis,
                Status = TicketStatus.New,
                SlaPolicyId = politika.Id,
                FirstResponseDueAt = acilis.AddMinutes(politika.FirstResponseMinutes),
                SlaDueAt = acilis.AddMinutes(politika.ResolutionMinutes),
                ApprovalStatus = k.Tur == TicketType.Change ? ApprovalStatus.Pending : ApprovalStatus.NotRequired
            };

            db.Tickets.Add(t);
            eklenen.Add(t);
        }

        await db.SaveChangesAsync();

        // Numara üretimi (uygulamadaki akışın aynısı)
        foreach (var t in eklenen) t.Number = Ui.BuildTicketNumber(t);
        await db.SaveChangesAsync();

        // Taleplerin bir kısmı işlenmiş, çözülmüş veya ihlale düşmüş olsun
        for (var i = 0; i < eklenen.Count; i++)
        {
            var t = eklenen[i];
            var agent = staff[i % staff.Count];

            // Her talebin ilk yanıtı var
            t.AssigneeId = agent.Id;
            t.FirstRespondedAt = t.CreatedAt.AddMinutes(Rnd.Next(10, 240));

            db.TicketComments.Add(new TicketComment
            {
                TicketId = t.Id,
                AuthorId = agent.Id,
                Body = yorumlar[i % yorumlar.Length],
                CreatedAt = t.FirstRespondedAt.Value
            });

            // Değişiklik talepleri onay sürecinde
            if (t.Type == TicketType.Change)
            {
                db.ChangeDetails.Add(new ChangeDetail
                {
                    TicketId = t.Id,
                    Risk = i % 3 == 0 ? RiskLevel.High : RiskLevel.Low,
                    PlannedStart = t.CreatedAt.AddDays(3),
                    PlannedEnd = t.CreatedAt.AddDays(3).AddHours(2),
                    ImplementationPlan = "Mesai dışı saatte uygulanacak.",
                    RollbackPlan = "Önceki yapılandırmaya dönülecek.",
                    ImpactAnalysis = "Uygulama sırasında kısa süreli kesinti olabilir.",
                    UpdatedById = agent.Id,
                    UpdatedAt = t.CreatedAt.AddDays(1)
                });
                continue;   // onay bekleyen talepler açık kalsın
            }

            var durum = i % 5;
            if (durum is 0 or 1 or 2)
            {
                // Çözülmüş: bir kısmı süresinde, bir kısmı gecikmeli
                var gecikmeli = i % 5 == 2;
                var cozum = gecikmeli ? t.SlaDueAt.AddHours(Rnd.Next(2, 20)) : t.CreatedAt.AddMinutes(Rnd.Next(30, 400));
                if (cozum > now) cozum = now.AddHours(-1);

                t.Status = TicketStatus.Closed;
                t.ResolvedAt = cozum;
                t.ClosedAt = cozum.AddHours(1);

                if (gecikmeli)
                {
                    t.IsSlaBreached = true;
                    t.SlaBreachedAt = t.SlaDueAt;
                    t.IsEscalated = true;
                    t.EscalationLevel = 1;

                    db.EscalationRecords.Add(new EscalationRecord
                    {
                        TicketId = t.Id,
                        Level = 1,
                        OccurredAt = t.SlaDueAt,
                        MinutesOverdue = (int)(cozum - t.SlaDueAt).TotalMinutes,
                        EscalatedToId = agent.Id,
                        Note = "Örnek veri: süresi aşılan talep"
                    });

                    // Kapalı örnek kayıtta etkinlik doğrulaması İZLİ olsun
                    // (md. 10.2.2): doğrulayan sorumludan farklı bir personel,
                    // doğrulama zamanı ve notu birlikte durur. Tek personelli
                    // kurulumda doğrulama hiç yazılmaz — kural böyle.
                    var kapaliOrnek = i % 2 == 0;
                    var dogrulayan = staff.FirstOrDefault(u => u.Id != agent.Id);

                    db.Nonconformities.Add(new Nonconformity
                    {
                        TicketId = t.Id,
                        Source = NonconformitySource.SlaBreach,
                        Status = kapaliOrnek ? NonconformityStatus.Closed : NonconformityStatus.Open,
                        Description = $"{t.Number} talebinde hizmet seviyesi aşıldı.",
                        RootCause = kapaliOrnek ? "Yedek parça stokta yoktu." : null,
                        CorrectiveAction = kapaliOrnek ? "Kritik parçalar için asgari stok seviyesi tanımlandı." : null,
                        ResponsibleId = agent.Id,
                        DetectedAt = t.SlaDueAt,
                        DueAt = t.SlaDueAt.AddDays(7),
                        ClosedAt = kapaliOrnek ? t.SlaDueAt.AddDays(3) : null,
                        EffectivenessVerified = kapaliOrnek && dogrulayan != null ? true : null,
                        VerifiedById = kapaliOrnek ? dogrulayan?.Id : null,
                        VerifiedAt = kapaliOrnek && dogrulayan != null ? t.SlaDueAt.AddDays(3) : null,
                        VerificationNote = kapaliOrnek && dogrulayan != null
                            ? "Önlemden sonraki 30 günde aynı kaynaklı ihlal tekrarlamadı; stok sayımı kaydıyla doğrulandı."
                            : null
                    });
                }

                // Çözülenlerin çoğu anket doldurmuş
                if (i % 4 != 3 && questions.Count > 0)
                {
                    var dusuk = i % 7 == 2;     // bazıları memnuniyetsiz
                    var cevap = new SurveyResponse
                    {
                        TicketId = t.Id,
                        TemplateId = questions[0].TemplateId,
                        SubmittedById = t.RequesterId,
                        CreatedAt = t.ClosedAt!.Value.AddHours(2),
                        Comment = dusuk ? "Çözüm çok uzun sürdü, bilgilendirilmedim." : null
                    };
                    foreach (var q in questions)
                        cevap.Answers.Add(new SurveyAnswer
                        {
                            QuestionId = q.Id,
                            Score = dusuk ? Rnd.Next(1, 3) : Rnd.Next(4, 6)
                        });

                    db.SurveyResponses.Add(cevap);

                    if (dusuk)
                    {
                        db.Nonconformities.Add(new Nonconformity
                        {
                            TicketId = t.Id,
                            Source = NonconformitySource.LowSatisfaction,
                            Status = NonconformityStatus.InProgress,
                            Description = $"{t.Number} talebinde memnuniyet ortalaması eşiğin altında.",
                            RootCause = "Kullanıcı süreç boyunca bilgilendirilmedi.",
                            ResponsibleId = agent.Id,
                            DetectedAt = cevap.CreatedAt,
                            DueAt = cevap.CreatedAt.AddDays(7)
                        });
                    }
                }
            }
            else if (durum == 3)
            {
                t.Status = TicketStatus.InProgress;
            }
            // durum == 4: yeni olarak kalır
        }

        await db.SaveChangesAsync();
        logger.LogInformation("Örnek veri üretildi: {Adet} talep.", eklenen.Count);
    }

    // Test hesapları. Canlı veritabanına bilinen şifreli hesap yazılmaması için
    // migration seed'inden çıkarıldı; yalnızca burada, Development'ta oluşturulur.
    // Şifre özetleri PasswordHasher<AppUser> biçiminde sabit değerlerdir.
    private static async Task SeedDemoUsersAsync(AppDbContext db, ILogger logger)
    {
        if (await db.Users.AnyAsync()) return;

        var tarih = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        // Demo hesaplar seed anında "yeni şifrelenmiş" sayılsın; aksi halde
        // PasswordMaxAgeDays dolmuş gibi profil kilidine düşerlerdi.
        var sifreTarihi = AppTime.UtcNow;

        db.Users.AddRange(
            new AppUser
            {
                FirstName = "Demo", LastName = "Yönetici",
                Email = "admin@servicedesk.local",
                PasswordHash = "AQAAAAIAAYagAAAAEFi3JF03e5L+rT0TFVRNIDpLX9U4gHjj97qMn+Eb/motK5gw/4voqGzIWdLDQnLoRg==", // admin123
                PhoneNumber = "05321234567",
                Role = UserRole.Admin, DepartmentId = 1, IsActive = true, CreatedAt = tarih,
                PasswordChangedAt = sifreTarihi
            },
            new AppUser
            {
                FirstName = "Ayşe", LastName = "Destek",
                Email = "agent@servicedesk.local",
                PasswordHash = "AQAAAAIAAYagAAAAEGkvNowp+RHWmgqpxlh3o9uxa4AB+oXFA7R1IxRkUxU8Phrot1adAxfk+tsgti4wag==", // agent123
                PhoneNumber = "05329876543",
                Role = UserRole.Agent, DepartmentId = 1, TeamId = 1, IsActive = true, CreatedAt = tarih,
                PasswordChangedAt = sifreTarihi
            },
            new AppUser
            {
                FirstName = "Mehmet", LastName = "Kullanıcı",
                Email = "user@servicedesk.local",
                PasswordHash = "AQAAAAIAAYagAAAAEGCuZpCsLC2ztoJiLtcHCbzkXXyQBZZ6US8lq80NpQ9uHcFc6ORCHZpysx1O4810DA==", // user123
                PhoneNumber = "05321112233",
                Role = UserRole.User, DepartmentId = 2, IsActive = true, CreatedAt = tarih,
                PasswordChangedAt = sifreTarihi
            });

        await db.SaveChangesAsync();
        logger.LogInformation("Demo kullanıcılar oluşturuldu (yalnızca Development).");
    }

    private static async Task SeedArticlesAsync(AppDbContext db, List<AppUser> staff, List<Category> categories)
    {
        if (await db.KnowledgeArticles.AnyAsync()) return;

        var yazar = staff[0];
        // Onaylayan: yazardan farklı biri olmalı (KnowledgePublishing: kendi
        // makalesini onaylayan olmaz). Tek personelli kurulumda yazara düşer;
        // örnek veri yine de yayında görünür.
        var onaylayan = staff.FirstOrDefault(u => u.Id != yazar.Id) ?? yazar;
        Category? Kat(string ad) => categories.FirstOrDefault(c => c.Name == ad);

        var makaleler = new[]
        {
            new KnowledgeArticle
            {
                Title = "Şifre sıfırlama adımları",
                Body = "1. Giriş ekranında 'Şifremi unuttum' bağlantısına tıklayın.\n"
                     + "2. Kurumsal e-posta adresinizi girin.\n"
                     + "3. Gelen bağlantıyla yeni şifrenizi belirleyin.\n"
                     + "Şifre en az 10 karakter olmalı ve büyük/küçük harf ile rakam içermelidir.",
                CategoryId = Kat("Hesap / Erişim")?.Id,
                AuthorId = yazar.Id,
                UsedCount = 12
            },
            new KnowledgeArticle
            {
                Title = "Yazıcı kağıt sıkışması giderme",
                Body = "1. Yazıcıyı kapatın ve fişini çekin.\n"
                     + "2. Arka kapağı açıp sıkışan kağıdı yırtmadan düz çekin.\n"
                     + "3. Kağıt tepsisini çıkarıp kılavuzları kağıt boyutuna ayarlayın.\n"
                     + "Sıkışma tekrar ederse besleme silindiri aşınmış olabilir, talep açın.",
                CategoryId = Kat("Donanım")?.Id,
                AuthorId = yazar.Id,
                UsedCount = 7
            },
            new KnowledgeArticle
            {
                Title = "VPN bağlantı sorunları",
                Body = "Bağlantı sık kopuyorsa:\n"
                     + "1. Modem üzerinden değil doğrudan kablo ile deneyin.\n"
                     + "2. VPN istemcisini güncelleyin.\n"
                     + "3. Güç ayarlarında ağ kartının uyku moduna geçmesini kapatın.\n"
                     + "Sorun sürerse bağlantı kaydıyla birlikte talep açın.",
                CategoryId = Kat("Ağ / İnternet")?.Id,
                AuthorId = yazar.Id,
                UsedCount = 4
            },
            new KnowledgeArticle
            {
                Title = "Değişiklik talebi nasıl açılır",
                Body = "Mevcut bir hizmette kalıcı değişiklik isteyen talepler Değişiklik türünde açılır.\n"
                     + "Talep yönetici onayından geçmeden işleme alınmaz.\n"
                     + "Yüksek riskli değişiklikler iki ayrı onay gerektirir.\n"
                     + "Talepte beklenen sonucu ve aciliyeti net yazın.",
                AuthorId = yazar.Id,
                UsedCount = 2
            }
        };

        // Örnek makaleler yayında başlar: chatbot ve talep çözüm listesi dolu
        // görünsün. Yayın yönetişimi kaydı da doldurulur — makale "kim
        // gönderdi, kim onayladı" bilgisi olmadan yayında durmasın (md. 7.5.2).
        foreach (var m in makaleler)
        {
            m.Status = KnowledgeArticleStatus.Published;
            m.SubmittedById = yazar.Id;
            m.SubmittedAt = m.CreatedAt;
            m.ApprovedById = onaylayan.Id;
            m.ApprovedAt = m.CreatedAt;
        }

        db.KnowledgeArticles.AddRange(makaleler);
        await db.SaveChangesAsync();

        // Sürüm geçmişi: yayındaki hal 1. sürüm olarak kaydedilir ki geçmiş
        // ekranı ve "sürüme geri dön" örnek veriyle de çalışsın
        db.KnowledgeArticleRevisions.AddRange(makaleler.Select(m =>
            KnowledgePublishing.Snapshot(m, 1, yazar.Id, m.CreatedAt, "İlk sürüm")));

        await db.SaveChangesAsync();
    }
}
