using System.ComponentModel.DataAnnotations;

namespace EventsAPI.Application.DTOs;

/// <summary>Учётные данные для входа.</summary>
public class LoginUser
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "Логин обязателен для заполнения")]
    public required string Login { get; set; }

    [Required(AllowEmptyStrings = false, ErrorMessage = "Пароль обязателен для заполнения")]
    public required string Password { get; set; }
}
