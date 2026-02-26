using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;

namespace LearningAPI.Middleware
{
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception occurred");
                await HandleExceptionAsync(context, ex);
            }
        }

        private static Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json; charset=utf-8";

            var response = new ErrorResponse();

            switch (exception)
            {
                case UnauthorizedAccessException:
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    response.Message = "Не авторизован";
                    break;
                case KeyNotFoundException:
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    response.Message = "Ресурс не найден";
                    break;
                case InvalidOperationException when !IsDatabaseException(exception):
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    response.Message = "Некорректный запрос";
                    break;
                case SqlException:
                    context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                    response.Message = "База данных недоступна";
                    break;
                default:
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    response.Message = "Внутренняя ошибка сервера";
                    break;
            }

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All)
            };
            var json = JsonSerializer.Serialize(response, options);
            return context.Response.WriteAsync(json, Encoding.UTF8);
        }

        /// <summary>
        /// Проверяет, является ли исключение связанным с базой данных (EF Core оборачивает SqlException в InvalidOperationException).
        /// </summary>
        private static bool IsDatabaseException(Exception exception)
        {
            for (var ex = exception; ex != null; ex = ex.InnerException)
            {
                if (ex is SqlException or Microsoft.EntityFrameworkCore.DbUpdateException)
                    return true;
            }
            return false;
        }

        public class ErrorResponse
        {
            public string Message { get; set; } = "";
            public List<string> Errors { get; set; } = new();
        }
    }
}
