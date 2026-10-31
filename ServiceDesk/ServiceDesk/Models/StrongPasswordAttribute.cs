using System.ComponentModel.DataAnnotations;
using ServiceDesk.Services;

namespace ServiceDesk.Models;

// Form modellerindeki yeni şifre alanları için. Boş değere izin verir
// (yönetim ekranında şifre opsiyonel); doluysa PasswordRules uygulanır.
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class StrongPasswordAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var hata = PasswordRules.Validate(value as string);
        return hata == null
            ? ValidationResult.Success
            : new ValidationResult(hata);
    }
}
