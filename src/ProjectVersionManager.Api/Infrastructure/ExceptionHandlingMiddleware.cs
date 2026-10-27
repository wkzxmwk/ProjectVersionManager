using System.Net;
using Microsoft.AspNetCore.Mvc;

namespace ProjectVersionManager.Api.Infrastructure;

public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger,
    IWebHostEnvironment environment)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            await HandleExceptionAsync(context, exception);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (status, title) = exception switch
        {
            NotFoundException => (HttpStatusCode.NotFound, "记录不存在"),
            ConflictException => (HttpStatusCode.Conflict, "数据冲突"),
            RequestValidationException => (HttpStatusCode.BadRequest, "请求无效"),
            UnauthorizedException => (HttpStatusCode.Unauthorized, "认证失败"),
            ForbiddenException => (HttpStatusCode.Forbidden, "无权访问"),
            _ => (HttpStatusCode.InternalServerError, "服务器内部错误")
        };

        if (status == HttpStatusCode.InternalServerError)
        {
            logger.LogError(exception, "未处理的请求异常，TraceId: {TraceId}", context.TraceIdentifier);
        }
        else
        {
            logger.LogWarning(exception, "请求处理失败，TraceId: {TraceId}", context.TraceIdentifier);
        }

        var problem = new ProblemDetails
        {
            Status = (int)status,
            Title = title,
            Detail = status == HttpStatusCode.InternalServerError && !environment.IsDevelopment()
                ? "请求处理失败，请联系管理员并提供跟踪编号。"
                : MostSpecificMessage(exception),
            Instance = context.Request.Path
        };
        problem.Extensions["traceId"] = context.TraceIdentifier;

        context.Response.StatusCode = problem.Status.Value;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problem);
    }

    private static string MostSpecificMessage(Exception exception)
    {
        var current = exception;
        while (current.InnerException is not null)
        {
            current = current.InnerException;
        }
        return current.Message;
    }
}
