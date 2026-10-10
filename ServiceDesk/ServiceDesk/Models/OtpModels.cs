using System.ComponentModel.DataAnnotations;

namespace ServiceDesk.Models;

public class LoginVm
{
    [Required(ErrorMessage = "E-posta gerekli"), EmailAddress]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Şifre gerekli"), DataType(DataType.Password)]
    public string Password { get; set; } = "";
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

    [Required, StringLength(6, MinimumLength = 6, ErrorMessage = "6 haneli kodu girin")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "Kod yalnızca rakamlardan oluşmalı")]
    public string Code { get; set; } = "";
}
