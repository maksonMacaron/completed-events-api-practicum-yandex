using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Contracts.Authentication;
using UsersAPI.Application.Abstractions.Authentication;
using UsersAPI.Application.Abstractions.Persistence;
using UsersAPI.Infrastructure.Authentication;
using UsersAPI.Infrastructure.Persistence;
using UsersAPI.Infrastructure.Persistence.Repositories;

namespace UsersAPI.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Строка подключения ConnectionStrings:DefaultConnection не задана");
        }

        services.AddDbContext<UsersDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddSingleton(CreateJwtSettings(configuration));
        return services;
    }

    private static JwtSettings CreateJwtSettings(IConfiguration configuration)
    {
        var section = configuration.GetSection(JwtSettings.SectionName);
        var settings = new JwtSettings
        {
            Secret = section[nameof(JwtSettings.Secret)] ?? string.Empty,
            Issuer = section[nameof(JwtSettings.Issuer)] ?? string.Empty,
            Audience = section[nameof(JwtSettings.Audience)] ?? string.Empty,
            LifetimeMinutes = int.TryParse(
                section[nameof(JwtSettings.LifetimeMinutes)],
                out var lifetimeMinutes)
                ? lifetimeMinutes
                : 0
        };

        if (string.IsNullOrWhiteSpace(settings.Secret)
            || Encoding.UTF8.GetByteCount(settings.Secret) < 32
            || string.IsNullOrWhiteSpace(settings.Issuer)
            || string.IsNullOrWhiteSpace(settings.Audience)
            || settings.LifetimeMinutes <= 0)
        {
            throw new InvalidOperationException("Параметры JWT в секции Jwt заполнены некорректно");
        }

        return settings;
    }

    public static async Task ApplyInfrastructureMigrationsAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        if (dbContext.Database.IsRelational())
            await dbContext.Database.MigrateAsync(cancellationToken);
    }
}
