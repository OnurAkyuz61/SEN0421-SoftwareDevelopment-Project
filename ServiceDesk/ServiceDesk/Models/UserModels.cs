using System.ComponentModel.DataAnnotations;
using ServiceDesk.Services;

namespace ServiceDesk.Models;

// YÖNETİM: kullanıcı oluşturma / düzenleme formu.
//
// Entity'yi doğrudan forma bağlamak yerine ayrı bir model kullanılıyor:
// PasswordHash, CreatedAt gibi alanların formdan gelmesi istenmiyor, şifre
// ise iki kez (yeni + tekrar) alınıyor.
public class UserFormVm
{
    // 0 = yeni kayıt
    public int Id { get; set; }

    [Required(ErrorMessage = "Ad girin")]
    [StringLength(50, ErrorMessage = "Ad en fazla 50 karakter olabilir")]
    [Display(Name = "Ad")]
    public string FirstName { get; set; } = "";

    [Required(ErrorMessage = "Soyad girin")]
    [StringLength(50, ErrorMessage = "Soyad en fazla 50 karakter olabilir")]
    [Display(Name = "Soyad")]
    public string LastName { get; set; } = "";

    [Required(ErrorMessage = "E-posta girin")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi girin")]
    [StringLength(150, ErrorMessage = "E-posta en fazla 150 karakter olabilir")]
    [Display(Name = "E-posta")]
    public string Email { get; set; } = "";

    // Telefon opsiyonel ama OTP bu numaraya gider: boşsa kullanıcı giriş yapamaz
    [StringLength(20, ErrorMessage = "Telefon en fazla 20 karakter olabilir")]
    [Phone(ErrorMessage = "Geçerli bir telefon numarası girin")]
    [Display(Name = "Telefon")]
    public string? PhoneNumber { get; set; }

    [Display(Name = "Rol")]
    public UserRole Role { get; set; } = UserRole.User;

    [Display(Name = "Birim")]
    public int? DepartmentId { get; set; }

    [Display(Name = "Ekip")]
    public int? TeamId { get; set; }

    [Display(Name = "Hesap aktif")]
    public bool IsActive { get; set; } = true;

    // Yeni kayıtta zorunlu, düzenlemede boş bırakılırsa şifre değişmez
    // (zorunluluk UsersController içinde kontrol edilir). Boş değerde
    // StrongPassword geçerlidir; doluysa karmaşıklık kuralları uygulanır.
    [DataType(DataType.Password)]
    [StringLength(PasswordRules.MaxLength)]
    [StrongPassword]
    [Display(Name = "Şifre")]
    public string? Password { get; set; }

    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Şifreler eşleşmiyor")]
    [Display(Name = "Şifre tekrar")]
    public string? ConfirmPassword { get; set; }

    public bool IsNew => Id == 0;

    // Salt okunur gösterim (düzenleme ekranındaki bilgi kartı)
    public DateTime? CreatedAt { get; set; }
    public int OpenTicketCount { get; set; }

    public static UserFormVm From(AppUser u) => new()
    {
        Id = u.Id,
        FirstName = u.FirstName,
        LastName = u.LastName,
        Email = u.Email,
        PhoneNumber = u.PhoneNumber,
        Role = u.Role,
        DepartmentId = u.DepartmentId,
        TeamId = u.TeamId,
        IsActive = u.IsActive,
        CreatedAt = u.CreatedAt
    };
}

// Yönetici: e-posta ile davet formu (hesap henüz oluşmaz)
public class UserInviteVm
{
    [Required(ErrorMessage = "Ad girin")]
    [StringLength(50)]
    [Display(Name = "Ad")]
    public string FirstName { get; set; } = "";

    [Required(ErrorMessage = "Soyad girin")]
    [StringLength(50)]
    [Display(Name = "Soyad")]
    public string LastName { get; set; } = "";

    [Required(ErrorMessage = "E-posta girin")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi girin")]
    [StringLength(150)]
    [Display(Name = "E-posta")]
    public string Email { get; set; } = "";

    [Display(Name = "Rol")]
    public UserRole Role { get; set; } = UserRole.User;

    [Display(Name = "Birim")]
    public int? DepartmentId { get; set; }

    [Display(Name = "Ekip")]
    public int? TeamId { get; set; }
}

// Davetli: bağlantıdan şifre + telefon belirleyerek hesabı tamamlar
public class AcceptInviteVm
{
    [Required]
    public string Token { get; set; } = "";

    // Salt gösterim (formdan gelmez; GET'te doldurulur)
    public string Email { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string RoleText { get; set; } = "";

    [Required(ErrorMessage = "Telefon girin")]
    [StringLength(20)]
    [Display(Name = "Telefon")]
    public string PhoneNumber { get; set; } = "";

    [Required(ErrorMessage = "Şifrenizi girin"), DataType(DataType.Password)]
    [StringLength(PasswordRules.MaxLength)]
    [StrongPassword]
    [Display(Name = "Şifre")]
    public string Password { get; set; } = "";

    [Required(ErrorMessage = "Şifreyi tekrar girin"), DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Şifreler eşleşmiyor")]
    [Display(Name = "Şifre tekrar")]
    public string ConfirmPassword { get; set; } = "";
}
