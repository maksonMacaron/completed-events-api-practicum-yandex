namespace EventsAPI.Application.DTOs;

/// <summary>Страница результатов.</summary>
public class PaginatedResult<T> where T : class
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int Count { get; set; }
    public int Total { get; set; }
    public IEnumerable<T> Items { get; set; } = [];
}
