using System.Net;

namespace EventsAPI.Presentation.Contracts.Responses
{
    /// <summary>
    /// Базовая модель ответа API.
    /// </summary>
    public class ApiBaseResult
    {
        /// <summary>
        /// Признак успешности выполнения запроса.
        /// </summary>
        public required bool Success { get; set; }

        /// <summary>
        /// HTTP-статус ответа.
        /// </summary>
        public required HttpStatusCode StatusCode { get; set; }

        /// <summary>
        /// Дата и время формирования ответа в UTC.
        /// </summary>
        public required DateTime DateTime { get; set; }

        /// <summary>
        /// Дополнительное сообщение ответа.
        /// </summary>
        public string? Message { get; set; }
    }

    /// <summary>
    /// Ответ API без возвращаемых данных.
    /// </summary>
    public class ApiResult : ApiBaseResult { }

    /// <summary>
    /// Ответ API с возвращаемыми данными.
    /// </summary>
    /// <typeparam name="T">Тип возвращаемых данных.</typeparam>
    public class ApiResult<T> : ApiBaseResult
    {
        /// <summary>
        /// Возвращаемые данные.
        /// </summary>
        public required T? Data { get; set; }
    }

    /// <summary>
    /// Ответ API при ошибках валидации.
    /// </summary>
    public class ValidationApiResult : ApiBaseResult
    {
        /// <summary>
        /// Словарь ошибок валидации по полям модели.
        /// </summary>
        public required Dictionary<string, IEnumerable<string>> Errors { get; set; }
    }
}
