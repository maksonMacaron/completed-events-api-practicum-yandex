using System.ComponentModel.DataAnnotations;
using UsersAPI.Domain.Entities;

namespace UsersAPI.Application.DTOs;

public sealed class RegisterUser
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "Логин обязателен для заполнения")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "Логин должен содержать от 3 до 100 символов")]
    public required string Login { get; init; }

    [Required(AllowEmptyStrings = false, ErrorMessage = "Пароль обязателен для заполнения")]
    [MinLength(8, ErrorMessage = "Пароль должен содержать не менее 8 символов")]
    public required string Password { get; init; }

    public UserRole Role { get; init; } = UserRole.User;
}
