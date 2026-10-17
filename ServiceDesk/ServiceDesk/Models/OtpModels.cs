using System.ComponentModel.DataAnnotations;

namespace ServiceDesk.Models;

public class LoginVm
{
    [Required(ErrorMessage = "E-posta adresinizi girin")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi girin")]
    [StringLength(150)]
    [Display(Name = "E-posta")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Şifrenizi girin"), DataType(DataType.Password)]
    [StringLength(128)]
    [Display(Name = "Şifre")]
    public string Password { get; set; } = "";

    // Oturum gerektiren bir sayfadan gelindiyse girişten sonra oraya dönülür
    public string? ReturnUrl { get; set; }
}

public class OtpChannelVm
{
    public string MaskedPhone { get; set; } = "";

    [Required(ErrorMessage = "Bir kanal seçin")]
    public OtpDeliveryChannel Channel { get; set; } = OtpDeliveryChannel.Sms;
}

public class OtpVerifyVm
{
    public string MaskedPhone { get; set; } = "";
    public OtpDeliveryChannel Channel { get; set; }

    // Ekrandaki geri sayımlar için (sunucu saatine göre hesaplanır,
    // böylece kullanıcının bilgisayar saati yanlış olsa da doğru çalışır)
    public int SecondsLeft { get; set; }
    public int ResendInSeconds { get; set; }
    public int SendsLeft { get; set; }
    public int AttemptsLeft { get; set; }

    [Required(ErrorMessage = "6 haneli kodu girin")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "6 haneli kodu girin")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "Kod yalnızca rakamlardan oluşmalı")]
    public string Code { get; set; } = "";
}
