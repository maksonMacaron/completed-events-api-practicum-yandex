using AutoMapper;
using EventsAPI.Contracts.Responses;
using EventsAPI.DTOs;
using EventsAPI.Models;
using EventsAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace EventsAPI.Controllers
{
    /// <summary>
    /// Контроллер для работы с мероприятиями.
    /// </summary>
    [Route("[controller]")]
    [ApiController]
    public class EventsController : ControllerBase
    {
        private readonly IEventService _eventService;
        private readonly IBookingService _bookingService;
        private readonly IMapper _mapper;

        /// <summary>
        /// Создаёт экземпляр контроллера мероприятий.
        /// </summary>
        /// <param name="eventService">Сервис для работы с мероприятиями.</param>
        /// <param name="bookingService">Сервис для работы с бронированиями.</param>
        /// <param name="mapper">Сервис маппинга DTO и моделей.</param>
        public EventsController(IEventService eventService, IBookingService bookingService, IMapper mapper)
        {
            _eventService = eventService;
            _bookingService = bookingService;
            _mapper = mapper;
        }


        /// <summary>
        /// Получить список всех мероприятий.
        /// </summary>
        /// <param name="page">Номер страницы для пагинации</param>
        /// <param name="pageSize">Размер страницы для пагинации</param>
        /// <param name="title">Наименование мероприятия (регистронезависимый, частичное совпадение)</param>
        /// <param name="from">Дата события, которые начинаются не раньше указанной</param>
        /// <param name="to">Дата событий, которые заканчиваются не позже указанной даты</param>
        /// <param name="cancellationToken">Токен отмены запроса.</param>
        /// <returns>Список мероприятий согласно фильтру или все мероприятия</returns>
        [HttpGet]
        public async Task<IActionResult> GetAllAsync(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? title = null,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null,
            CancellationToken cancellationToken = default)
        {
            var serviceResult = await _eventService.GetAllAsync(
                page,
                pageSize,
                title,
                from,
                to,
                cancellationToken);

            var dtoResult = new PaginatedResult<EventDto>
            {
                Page = serviceResult.Page,
                PageSize = serviceResult.PageSize,
                Total = serviceResult.Total,
                Count = serviceResult.Count,
                Items = _mapper.Map<IEnumerable<EventDto>>(serviceResult.Items)
            };

            return Ok(new ApiResult<PaginatedResult<EventDto>>
            {
                Data = dtoResult,
                Message = $"Список всех событий. Всего {dtoResult.Total}",
                StatusCode = System.Net.HttpStatusCode.OK,
                Success = true,
            });
        }

        /// <summary>
        /// Получить мероприятие по идентификатору.
        /// </summary>
        /// <param name="id">Идентификатор мероприятия.</param>
        /// <param name="cancellationToken">Токен отмены запроса.</param>
        /// <returns>Найденное мероприятие или ошибка 404, если мероприятие не существует.</returns>
        [HttpGet("{id:guid}", Name = "GetEventById")]
        public async Task<IActionResult> GetByIdAsync(
            [FromRoute] Guid id,
            CancellationToken cancellationToken = default)
        {
            var findModelEvent = await _eventService.GetByIdAsync(id, cancellationToken);
            return Ok(new ApiResult<EventDto>
            {
                Data = _mapper.Map<EventDto>(findModelEvent),
                Message = $"Событие по Id [{id}] получено",
                StatusCode = System.Net.HttpStatusCode.OK,
                Success = true,
            });
        }

        /// <summary>
        /// Создать новое мероприятие.
        /// </summary>
        /// <param name="createEvent">Данные нового мероприятия.</param>
        /// <param name="cancellationToken">Токен отмены запроса.</param>
        /// <returns>Созданное мероприятие.</returns>
        [HttpPost]
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
                });
        }

        /// <summary>
        /// Полностью обновить мероприятие по идентификатору.
        /// </summary>
        /// <param name="id">Идентификатор мероприятия.</param>
        /// <param name="eventDto">Новые данные мероприятия.</param>
        /// <param name="cancellationToken">Токен отмены запроса.</param>
        /// <returns>Обновлённое мероприятие или ошибка, если мероприятие не найдено.</returns>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateAsync(
            [FromRoute] Guid id,
            [FromBody] EventDto eventDto,
            CancellationToken cancellationToken = default)
        {
            var updateEventModel = await _eventService.UpdateAsync(id, eventDto, cancellationToken);

            return Ok(new ApiResult<EventDto>
            {
                Data = _mapper.Map<EventDto>(updateEventModel),
                Message = $"Cобытие успешно обновлено по Id [{updateEventModel.Id}]",
                StatusCode = System.Net.HttpStatusCode.OK,
                Success = true,
            });
        }

        /// <summary>
        /// Удалить мероприятие по идентификатору.
        /// </summary>
        /// <param name="id">Идентификатор мероприятия.</param>
        /// <param name="cancellationToken">Токен отмены запроса.</param>
        /// <returns>Пустой ответ, если удаление прошло успешно, или ошибка 404.</returns>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAsync(
            [FromRoute] Guid id,
            CancellationToken cancellationToken = default)
        {
            await _eventService.DeleteAsync(id, cancellationToken);
            return NoContent();
        }

        /// <summary>
        /// Создать бронь для мероприятия.
        /// </summary>
        /// <param name="id">Идентификатор мероприятия.</param>
        /// <param name="cancellationToken">Токен отмены запроса.</param>
        /// <returns>Созданная бронь со статусом 202 и ссылкой на её ресурс либо ошибка 404.</returns>
        [HttpPost("{id:guid}/book")]
        [ProducesResponseType(typeof(ApiResult<Booking>), StatusCodes.Status202Accepted)]
        [ProducesResponseType(typeof(ApiResult), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResult), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreateBookingAsync(
            [FromRoute] Guid id,
            CancellationToken cancellationToken = default)
        {
            var booking = await _bookingService.CreateBookingAsync(id, cancellationToken);

            return AcceptedAtRoute("GetBookingById", new { id = booking.Id }, new ApiResult<Booking>
            {
                Data = booking,
                Message = $"Бронь по Id [{booking.Id}] принята в обработку",
                StatusCode = System.Net.HttpStatusCode.Accepted,
                Success = true,
            });
        }

    }
}
