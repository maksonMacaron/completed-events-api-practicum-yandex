using System.ComponentModel.DataAnnotations;

namespace UsersAPI.Application.DTOs;

public sealed class LoginUser
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "Логин обязателен для заполнения")]
    public required string Login { get; init; }

    [Required(AllowEmptyStrings = false, ErrorMessage = "Пароль обязателен для заполнения")]
    public required string Password { get; init; }
}
