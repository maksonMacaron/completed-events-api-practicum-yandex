using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using EventsAPI.Application.DTOs;
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
        var createResponse = await _client.PostAsJsonAsync("/events", CreateEventRequest(1));
        var created = await createResponse.Content.ReadFromJsonAsync<ApiResult<EventInfo>>(JsonOptions);
        Assert.NotNull(created?.Data);

        var firstResponse = await _client.PostAsync($"/events/{created.Data.Id}/book", null);
        var secondResponse = await _client.PostAsync($"/events/{created.Data.Id}/book", null);
        var result = await secondResponse.Content.ReadFromJsonAsync<ApiResult>(JsonOptions);

        Assert.Equal(HttpStatusCode.Accepted, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal(HttpStatusCode.Conflict, result.StatusCode);
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
