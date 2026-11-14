using Microsoft.AspNetCore.Mvc;
using ServiceDesk.Filters;
using ServiceDesk.Models;

namespace ServiceDesk.Controllers;

// Arayüz enum sözlüğü (giriş yapmış herkes). JSON döner.
//
//   Sözlük  GET  api/enums
//
// Diğer uçlar enum'ları sayısal değeriyle gönderiyor; ekranda görünecek Türkçe
// metin ve etiket rengi ise burada. İstemci sözlüğü bir kez çeker, listeleri
// ve açılır kutuları buradan doldurur: "InProgress" karşılığının "İşlemde"
// olduğu tek yerde yazılı kalır.
//
// Tek doğruluk kaynağı Models/Ui.cs'tir; metinler buraya kopyalanmaz, Ui
// metotları çağrılır. Ui'da renk eşlemesi olmayan enum'larda tag boş (null)
// gelir — uydurma renk döndürmek yerine istemci kendi varsayılanını kullanır.
//
// Yalnızca okuma yapan tek bir GET olduğu için yazma / CSRF tarafı yok.
[ApiController]
[Route("api/enums")]
[ApiAuthorizeRole]
public class EnumsController : ControllerBase
{
    // value: veritabanındaki sayısal değer (diğer uçlarla eşleşen alan)
    // name : enum sabitinin adı (kod tarafında okunabilir anahtar)
    // text : kullanıcıya gösterilecek Türkçe metin
    // tag  : site.css'teki etiket rengi sınıfı (tag-red / tag-amber / …)
    public record EnumItem(int Value, string Name, string Text, string? Tag);

    [HttpGet]
    public IActionResult Get() => Ok(new
    {
        ticketStatus = Items<TicketStatus>(Ui.StatusText, Ui.StatusTag),
        priority = Items<Priority>(Ui.PriorityText, Ui.PriorityTag),
        ticketType = Items<TicketType>(Ui.TypeText),
        resolutionCode = Items<ResolutionCode>(c => Ui.ResolutionText(c)),
        nonconformitySource = Items<NonconformitySource>(Ui.NonconformitySourceText),
        nonconformityStatus = Items<NonconformityStatus>(Ui.NonconformityStatusText, Ui.NonconformityStatusTag)
    });

    // Enum'un tüm değerlerini tanımlı sırasıyla (sayısal değere göre) döker.
    // Yeni bir enum değeri eklendiğinde bu uç onu kendiliğinden yayınlar.
    private static List<EnumItem> Items<T>(Func<T, string> text, Func<T, string>? tag = null)
        where T : struct, Enum =>
        Enum.GetValues<T>()
            .Select(v => new EnumItem(Convert.ToInt32(v), v.ToString(), text(v), tag?.Invoke(v)))
            .ToList();
}
