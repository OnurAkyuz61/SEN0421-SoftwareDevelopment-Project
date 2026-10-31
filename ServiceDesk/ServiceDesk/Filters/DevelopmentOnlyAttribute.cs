using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ServiceDesk.Filters;

// Geliştirme kolaylıkları (DevLogin gibi) için kilit. Action'ın kendi içinde
// yapılan kontrolün önüne geçer: model bağlama, antiforgery ve action kodu
// hiç çalışmadan 404 döner, yani adresin varlığı bile anlaşılmaz.
//
// İki şart birlikte aranır:
//   1. Ortam adı tam olarak "Development" olmalı. ASPNETCORE_ENVIRONMENT
//      tanımlı değilse ASP.NET Core "Production" kabul eder; Staging, Test
//      gibi ortamlarda da kapalıdır.
//   2. İstek bu bilgisayardan (loopback) gelmeli. "dotnet run" komutu
//      launchSettings.json'daki Development ayarını sunucuda da uygular; bu
//      şart, yanlışlıkla Development'ta ayağa kalkmış bir sunucuda bile
//      dışarıdan şifresiz giriş yapılmasını engeller.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class DevelopmentOnlyAttribute : Attribute, IResourceFilter, IOrderedFilter
{
    // Diğer filtrelerden (antiforgery dahil) önce çalışsın
    public int Order => int.MinValue;

    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        if (!IsAllowed(context.HttpContext))
            context.Result = new NotFoundResult();
    }

    public void OnResourceExecuted(ResourceExecutedContext context) { }

    // Görünümler de aynı kuralla karar verir (giriş sayfasındaki dev kutusu)
    public static bool IsAllowed(HttpContext http)
    {
        var env = http.RequestServices.GetRequiredService<IWebHostEnvironment>();
        return env.IsDevelopment() && IsLocal(http.Connection);
    }

    private static bool IsLocal(ConnectionInfo c) =>
        c.RemoteIpAddress is null                    // test sunucusu / in-process
        || IPAddress.IsLoopback(c.RemoteIpAddress)
        || (c.LocalIpAddress != null && c.RemoteIpAddress.Equals(c.LocalIpAddress));
}
