using EventsAPI.Application.DTOs;
using EventsAPI.Application.Services;
using EventsAPI.Domain.Entities;
using EventsAPI.Presentation.Contracts.Responses;
using EventsAPI.Presentation.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventsAPI.Presentation.Controllers;

/// <summary>Контроллер для работы с бронированиями.</summary>
[Route("bookings")]
[ApiController]
[Authorize]
public class BookingsController : ControllerBase
{
    private readonly IBookingService _bookingService;
    private readonly TimeProvider _timeProvider;

    public BookingsController(IBookingService bookingService, TimeProvider timeProvider)
    {
        _bookingService = bookingService;
        _timeProvider = timeProvider;
    }

    /// <summary>Получить бронь по идентификатору.</summary>
    [HttpGet("{id:guid}", Name = "GetBookingById")]
    [ProducesResponseType(typeof(ApiResult<BookingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResult), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBookingByIdAsync(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var booking = await _bookingService.GetBookingByIdAsync(id, cancellationToken);
        return Ok(new ApiResult<BookingDto>
        {
            Data = booking,
            Message = $"Бронь по Id [{id}] получена",
            StatusCode = System.Net.HttpStatusCode.OK,
            Success = true,
            DateTime = _timeProvider.GetUtcNow().UtcDateTime
        });
    }

    /// <summary>Отменить бронирование.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResult), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResult), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelBookingAsync(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        await _bookingService.CancelBookingAsync(
            id,
            User.GetUserId(),
            User.IsInRole(nameof(UserRole.Admin)),
            cancellationToken);

        return NoContent();
    }
}
