using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using EventsAPI.Application.DTOs;
using EventsAPI.Domain.Entities;
using EventsAPI.Presentation.Contracts.Responses;

namespace EventsAPI.HttpTests;

public sealed class EventsApiTests : IClassFixture<EventsApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _client;

    public EventsApiTests(EventsApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateEvent_ValidRequest_ReturnsCreatedResponse()
    {
        await AuthenticateAsync(UserRole.Admin);
        var request = CreateEventRequest(10);

        var response = await _client.PostAsJsonAsync("/events", request);
        var result = await response.Content.ReadFromJsonAsync<ApiResult<EventInfo>>(JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(HttpStatusCode.Created, result.StatusCode);
        Assert.Equal(EventsApiFactory.UtcNow.UtcDateTime, result.DateTime);
        Assert.NotEqual(Guid.Empty, result.Data?.Id);
        Assert.Equal(
            $"/Events/{result.Data?.Id}",
            response.Headers.Location.AbsolutePath,
            ignoreCase: true);
    }

    [Fact]
    public async Task CreateEvent_MissingTotalSeats_ReturnsValidationResponse()
    {
        await AuthenticateAsync(UserRole.Admin);
        var request = new
        {
            Title = $"Событие {Guid.NewGuid()}",
            StartAt = EventsApiFactory.UtcNow.UtcDateTime.AddDays(1),
            EndAt = EventsApiFactory.UtcNow.UtcDateTime.AddDays(1).AddHours(2)
        };

        var response = await _client.PostAsJsonAsync("/events", request);
        using var result = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(result.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("Ошибка валидации", result.RootElement.GetProperty("message").GetString());
        Assert.True(result.RootElement.GetProperty("errors").TryGetProperty("TotalSeats", out _));
    }

    [Fact]
    public async Task GetEvent_MissingEvent_ReturnsNotFoundResponse()
    {
        var response = await _client.GetAsync($"/events/{Guid.NewGuid()}");
        var result = await response.Content.ReadFromJsonAsync<ApiResult>(JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
        Assert.Equal(EventsApiFactory.UtcNow.UtcDateTime, result.DateTime);
    }

    [Fact]
    public async Task CreateBooking_WhenSeatsAreOver_ReturnsConflictResponse()
    {
        await AuthenticateAsync(UserRole.Admin);
        var createResponse = await _client.PostAsJsonAsync("/events", CreateEventRequest(1));
        var created = await createResponse.Content.ReadFromJsonAsync<ApiResult<EventInfo>>(JsonOptions);
        Assert.NotNull(created?.Data);

        await AuthenticateAsync(UserRole.User);
        var firstResponse = await _client.PostAsync($"/events/{created.Data.Id}/book", null);
        var secondResponse = await _client.PostAsync($"/events/{created.Data.Id}/book", null);
        var result = await secondResponse.Content.ReadFromJsonAsync<ApiResult>(JsonOptions);

        Assert.Equal(HttpStatusCode.Accepted, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal(HttpStatusCode.Conflict, result.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_WithoutToken_ReturnsUnauthorized()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.PostAsJsonAsync("/events", CreateEventRequest(10));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_UserToken_ReturnsForbidden()
    {
        await AuthenticateAsync(UserRole.User);

        var response = await _client.PostAsJsonAsync("/events", CreateEventRequest(10));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateBooking_WithoutToken_ReturnsUnauthorized()
    {
        await AuthenticateAsync(UserRole.Admin);
        var createResponse = await _client.PostAsJsonAsync("/events", CreateEventRequest(1));
        var created = await createResponse.Content.ReadFromJsonAsync<ApiResult<EventInfo>>(JsonOptions);
        Assert.NotNull(created?.Data);
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.PostAsync($"/events/{created.Data.Id}/book", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CancelBooking_OtherUserIsForbidden_AdminCanCancel()
    {
        var adminToken = await AuthenticateAsync(UserRole.Admin);
        var createResponse = await _client.PostAsJsonAsync("/events", CreateEventRequest(1));
        var created = await createResponse.Content.ReadFromJsonAsync<ApiResult<EventInfo>>(JsonOptions);
        Assert.NotNull(created?.Data);

        await AuthenticateAsync(UserRole.User);
        var bookingResponse = await _client.PostAsync($"/events/{created.Data.Id}/book", null);
        var booking = await bookingResponse.Content.ReadFromJsonAsync<ApiResult<BookingDto>>(JsonOptions);
        Assert.NotNull(booking?.Data);

        await AuthenticateAsync(UserRole.User);
        var forbiddenResponse = await _client.DeleteAsync($"/bookings/{booking.Data.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            adminToken);
        var adminResponse = await _client.DeleteAsync($"/bookings/{booking.Data.Id}");
        Assert.Equal(HttpStatusCode.NoContent, adminResponse.StatusCode);
    }

    private async Task<string> AuthenticateAsync(UserRole role)
    {
        var login = $"user-{Guid.NewGuid():N}";
        const string password = "password";
        var registerResponse = await _client.PostAsJsonAsync("/auth/register", new RegisterUser
        {
            Login = login,
            Password = password,
            Role = role
        });
        Assert.Equal(HttpStatusCode.NoContent, registerResponse.StatusCode);

        var loginResponse = await _client.PostAsJsonAsync("/auth/login", new LoginUser
        {
            Login = login,
            Password = password
        });
        var result = await loginResponse.Content.ReadFromJsonAsync<ApiResult<AuthToken>>(JsonOptions);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.NotNull(result?.Data);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            result.Data.Token);
        return result.Data.Token;
    }

    private static CreateEvent CreateEventRequest(int totalSeats)
    {
        var startAt = EventsApiFactory.UtcNow.UtcDateTime.AddDays(1);
        return new CreateEvent
        {
            Title = $"Событие {Guid.NewGuid()}",
            Description = "Описание события",
            StartAt = startAt,
            EndAt = startAt.AddHours(2),
            TotalSeats = totalSeats
        };
    }
}
