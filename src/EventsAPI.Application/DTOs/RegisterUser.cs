using System.ComponentModel.DataAnnotations;
using EventsAPI.Domain.Entities;

namespace EventsAPI.Application.DTOs;

/// <summary>Данные для регистрации пользователя.</summary>
public class RegisterUser
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "Логин обязателен для заполнения")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "Логин должен содержать от 3 до 100 символов")]
    public required string Login { get; set; }

    [Required(AllowEmptyStrings = false, ErrorMessage = "Пароль обязателен для заполнения")]
    public required string Password { get; set; }

    public UserRole Role { get; set; } = UserRole.User;
}
