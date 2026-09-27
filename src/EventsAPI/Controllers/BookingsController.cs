using EventsAPI.Contracts.Responses;
using EventsAPI.Models;
using EventsAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace EventsAPI.Controllers
{
    /// <summary>
    /// Контроллер для работы с бронированиями.
    /// </summary>
    [Route("bookings")]
    [ApiController]
    public class BookingsController : ControllerBase
    {
        private readonly IBookingService _bookingService;


        /// <summary>
        /// Создаёт экземпляр контроллера бронирований.
        /// </summary>
        /// <param name="bookingService">Сервис для работы с бронированиями.</param>
        public BookingsController(IBookingService bookingService)
        {
            _bookingService = bookingService;
        }


        /// <summary>
        /// Получить бронь по идентификатору.
        /// </summary>
        /// <param name="id">Идентификатор брони.</param>
        /// <param name="cancellationToken">Токен отмены запроса.</param>
        /// <returns>Найденная бронь или ошибка 404, если бронь не существует.</returns>
        [HttpGet("{id:guid}", Name = "GetBookingById")]
        [ProducesResponseType(typeof(ApiResult<Booking>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetBookingByIdAsync(
            [FromRoute] Guid id,
            CancellationToken cancellationToken = default)
        {
            var booking = await _bookingService.GetBookingByIdAsync(id, cancellationToken);
            return Ok(new ApiResult<Booking>
            {
                Data = booking,
                Message = $"Бронь по Id [{id}] получена",
                StatusCode = System.Net.HttpStatusCode.OK,
                Success = true,
            });
        }
    }
}
