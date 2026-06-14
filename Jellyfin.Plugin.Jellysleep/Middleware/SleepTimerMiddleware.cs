using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Jellyfin.Plugin.Jellysleep.Services;

namespace Jellyfin.Plugin.Jellysleep.Middleware;

/// <summary>
/// Intercepts HLS segment requests for sessions with a pending sleep timer pause,
/// returning 503 so the player stalls regardless of WebSocket state.
/// </summary>
public class SleepTimerMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>
    /// Initializes a new instance of the <see cref="SleepTimerMiddleware"/> class.
    /// </summary>
    public SleepTimerMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <inheritdoc />
    public async Task InvokeAsync(HttpContext context, ISleepTimerService sleepTimerService)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // Only intercept HLS segment requests
        if (path.Contains("/Videos/", StringComparison.OrdinalIgnoreCase) &&
            (path.EndsWith(".ts", StringComparison.OrdinalIgnoreCase) ||
             path.EndsWith(".m4s", StringComparison.OrdinalIgnoreCase) ||
             path.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)) &&
            context.Request.Query.TryGetValue("DeviceId", out var deviceId) &&
            sleepTimerService.ConsumePendingPause(deviceId.ToString()))
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return;
        }

        await _next(context).ConfigureAwait(false);
    }
}

/// <summary>
/// Registers <see cref="SleepTimerMiddleware"/> via <see cref="IStartupFilter"/>.
/// </summary>
public class SleepTimerStartupFilter : IStartupFilter
{
    /// <inheritdoc />
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            app.UseMiddleware<SleepTimerMiddleware>();
            next(app);
        };
    }
}