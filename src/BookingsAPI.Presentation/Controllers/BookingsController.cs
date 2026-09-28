using System.Net;
using BookingsAPI.Application.DTOs;
using BookingsAPI.Application.Services;
using BookingsAPI.Presentation.Contracts.Responses;
using BookingsAPI.Presentation.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookingsAPI.Presentation.Controllers;

[Route("bookings")]
[ApiController]
[Authorize]
public sealed class BookingsController : ControllerBase
{
    private readonly IBookingService _bookingService;
    private readonly TimeProvider _timeProvider;

    public BookingsController(
        IBookingService bookingService,
        TimeProvider timeProvider)
    {
        _bookingService = bookingService;
        _timeProvider = timeProvider;
    }

    /// <summary>Создать бронь.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResult<BookingDto>), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ValidationApiResult), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateAsync(
        [FromBody] CreateBooking request,
        CancellationToken cancellationToken = default)
    {
        var booking = await _bookingService.CreateAsync(
            request,
            User.GetUserId(),
            cancellationToken);

        return AcceptedAtRoute(
            "GetBookingById",
            new { id = booking.Id },
            new ApiResult<BookingDto>
            {
                Data = booking,
                Message = $"Бронь по Id [{booking.Id}] принята в обработку",
                StatusCode = HttpStatusCode.Accepted,
                Success = true,
                DateTime = GetUtcNow()
            });
    }

    /// <summary>Получить бронь по идентификатору.</summary>
    [HttpGet("{id:guid}", Name = "GetBookingById")]
    [ProducesResponseType(typeof(ApiResult<BookingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResult), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByIdAsync(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var booking = await _bookingService.GetByIdAsync(id, cancellationToken);
        return Ok(new ApiResult<BookingDto>
        {
            Data = booking,
            Message = $"Бронь по Id [{id}] получена",
            StatusCode = HttpStatusCode.OK,
            Success = true,
            DateTime = GetUtcNow()
        });
    }

    /// <summary>Отменить бронь.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResult), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResult), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelAsync(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        await _bookingService.CancelAsync(
            id,
            User.GetUserId(),
            User.IsInRole("Admin"),
            cancellationToken);
        return NoContent();
    }

    private DateTime GetUtcNow() => _timeProvider.GetUtcNow().UtcDateTime;
}
