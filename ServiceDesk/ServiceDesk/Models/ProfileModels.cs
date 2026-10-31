using System.ComponentModel.DataAnnotations;
using ServiceDesk.Services;

namespace ServiceDesk.Models;

public class ProfileVm
{
    [Required(ErrorMessage = "Adınızı girin")]
    [StringLength(50, ErrorMessage = "Ad en fazla 50 karakter olabilir")]
    [Display(Name = "Ad")]
    public string FirstName { get; set; } = "";

    [Required(ErrorMessage = "Soyadınızı girin")]
    [StringLength(50, ErrorMessage = "Soyad en fazla 50 karakter olabilir")]
    [Display(Name = "Soyad")]
    public string LastName { get; set; } = "";

    [StringLength(20, ErrorMessage = "Telefon en fazla 20 karakter olabilir")]
    [Phone(ErrorMessage = "Geçerli bir telefon numarası girin")]
    [Display(Name = "Telefon")]
    public string? PhoneNumber { get; set; }

    // Salt okunur gösterim
    public string Email { get; set; } = "";
    public string RoleText { get; set; } = "";
}

public class ChangePasswordVm
{
    [Required(ErrorMessage = "Mevcut şifrenizi girin"), DataType(DataType.Password)]
    [Display(Name = "Mevcut şifre")]
    public string CurrentPassword { get; set; } = "";

    [Required(ErrorMessage = "Yeni şifrenizi girin"), DataType(DataType.Password)]
    [StringLength(PasswordRules.MaxLength)]
    [StrongPassword]
    [Display(Name = "Yeni şifre")]
    public string NewPassword { get; set; } = "";

    [Required(ErrorMessage = "Şifreyi tekrar girin"), DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "Şifreler eşleşmiyor")]
    [Display(Name = "Şifre tekrar")]
    public string ConfirmPassword { get; set; } = "";
}

public class ProfilePageVm
{
    public ProfileVm Profile { get; set; } = new();
    public ChangePasswordVm Password { get; set; } = new();
}
