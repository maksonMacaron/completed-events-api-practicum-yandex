using System.IdentityModel.Tokens.Jwt;
using EventsAPI.Application.DTOs;
using EventsAPI.Application.Services;
using EventsAPI.Domain.Entities;
using EventsAPI.Presentation.Contracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventsAPI.Presentation.Controllers;

/// <summary>Контроллер для работы с мероприятиями.</summary>
[Route("[controller]")]
[ApiController]
public class EventsController : ControllerBase
{
    private readonly IEventService _eventService;
    private readonly IBookingService _bookingService;
    private readonly TimeProvider _timeProvider;

    public EventsController(
        IEventService eventService,
        IBookingService bookingService,
        TimeProvider timeProvider)
    {
        _eventService = eventService;
        _bookingService = bookingService;
        _timeProvider = timeProvider;
    }

    /// <summary>Получить список мероприятий с фильтрацией и пагинацией.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResult<PaginatedResult<EventDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? title = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _eventService.GetAllAsync(
            page,
            pageSize,
            title,
            from,
            to,
            cancellationToken);

        return Ok(new ApiResult<PaginatedResult<EventDto>>
        {
            Data = result,
            Message = $"Список всех событий. Всего {result.Total}",
            StatusCode = System.Net.HttpStatusCode.OK,
            Success = true,
            DateTime = GetUtcNow()
        });
    }

    /// <summary>Получить мероприятие по идентификатору.</summary>
    [HttpGet("{id:guid}", Name = "GetEventById")]
    [ProducesResponseType(typeof(ApiResult<EventDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResult), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByIdAsync(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var eventItem = await _eventService.GetByIdAsync(id, cancellationToken);
        return Ok(new ApiResult<EventDto>
        {
            Data = eventItem,
            Message = $"Событие по Id [{id}] получено",
            StatusCode = System.Net.HttpStatusCode.OK,
            Success = true,
            DateTime = GetUtcNow()
        });
    }

    /// <summary>Создать новое мероприятие.</summary>
    [HttpPost]
    [Authorize(Roles = nameof(UserRole.Admin))]
    [ProducesResponseType(typeof(ApiResult<EventInfo>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationApiResult), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateAsync(
        [FromBody] CreateEvent createEvent,
        CancellationToken cancellationToken = default)
    {
        var eventInfo = await _eventService.CreateEventAsync(createEvent, cancellationToken);

        return CreatedAtRoute(
            "GetEventById",
            new { id = eventInfo.Id },
            new ApiResult<EventInfo>
            {
                Data = eventInfo,
                Message = $"Новое событие успешно создано с Id [{eventInfo.Id}]",
                StatusCode = System.Net.HttpStatusCode.Created,
                Success = true,
                DateTime = GetUtcNow()
            });
    }

    /// <summary>Полностью обновить мероприятие по идентификатору.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    [ProducesResponseType(typeof(ApiResult<EventDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationApiResult), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResult), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAsync(
        [FromRoute] Guid id,
        [FromBody] EventDto eventDto,
        CancellationToken cancellationToken = default)
    {
        var updatedEvent = await _eventService.UpdateAsync(id, eventDto, cancellationToken);

        return Ok(new ApiResult<EventDto>
        {
            Data = updatedEvent,
            Message = $"Cобытие успешно обновлено по Id [{updatedEvent.Id}]",
            StatusCode = System.Net.HttpStatusCode.OK,
            Success = true,
            DateTime = GetUtcNow()
        });
    }

    /// <summary>Удалить мероприятие по идентификатору.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResult), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsync(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        await _eventService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Создать бронь для мероприятия.</summary>
    [HttpPost("{id:guid}/book")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResult<BookingDto>), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ApiResult), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResult), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateBookingAsync(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var booking = await _bookingService.CreateBookingAsync(
            id,
            GetCurrentUserId(),
            cancellationToken);

        return AcceptedAtRoute(
            "GetBookingById",
            new { id = booking.Id },
            new ApiResult<BookingDto>
            {
                Data = booking,
                Message = $"Бронь по Id [{booking.Id}] принята в обработку",
                StatusCode = System.Net.HttpStatusCode.Accepted,
                Success = true,
                DateTime = GetUtcNow()
            });
    }

    private DateTime GetUtcNow() => _timeProvider.GetUtcNow().UtcDateTime;

    private Guid GetCurrentUserId()
    {
        var value = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new UnauthorizedAccessException("Идентификатор пользователя отсутствует в токене");
    }
}
