using System.Net;
using System.Reflection;
using System.Text.Json.Serialization;
using EventsAPI.Application;
using EventsAPI.Infrastructure;
using EventsAPI.Presentation.Contracts.Responses;
using EventsAPI.Presentation.Middlewares;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen(options =>
{
    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));
});

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(item => item.Value?.Errors.Count > 0)
                .ToDictionary(
                    item => item.Key,
                    item => item.Value!.Errors.Select(error => error.ErrorMessage));

            var response = new ValidationApiResult
            {
                StatusCode = HttpStatusCode.BadRequest,
                Success = false,
                Errors = errors,
                Message = "Ошибка валидации"
            };

            return new BadRequestObjectResult(response);
        };
    });

var app = builder.Build();

try
{
    await app.Services.ApplyInfrastructureMigrationsAsync();
}
catch (Exception exception)
{
    app.Logger.LogCritical(exception, "Не удалось применить миграции базы данных");
}

app.UseMiddleware<GlobalExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();
