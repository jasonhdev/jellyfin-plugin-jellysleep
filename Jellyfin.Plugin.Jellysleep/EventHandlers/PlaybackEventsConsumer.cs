// EventHandlers/PlaybackProgressConsumer.cs
using Jellyfin.Plugin.Jellysleep.Models;
using Jellyfin.Plugin.Jellysleep.Services;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Jellysleep.EventHandlers;

/// <summary>
/// Handles playback progress events to auto-start a sleep timer on session resume.
/// </summary>
public class PlaybackProgressConsumer : IEventConsumer<PlaybackProgressEventArgs>
{
    private readonly ILogger<PlaybackProgressConsumer> _logger;
    private readonly ISleepTimerService _sleepTimerService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaybackProgressConsumer"/> class.
    /// </summary>
    public PlaybackProgressConsumer(
        ILogger<PlaybackProgressConsumer> logger,
        ISleepTimerService sleepTimerService)
    {
        _logger = logger;
        _sleepTimerService = sleepTimerService;
    }

    /// <inheritdoc />
    public async Task OnEvent(PlaybackProgressEventArgs eventArgs)
    {
        try
        {
            var session = eventArgs.Session;
            if (session?.UserId == null || session.UserId == Guid.Empty)
                return;

            if (!ShouldAutoStart(session.DeviceId))
                return;

            var timerStatus = await _sleepTimerService.GetTimerStatusAsync(session.UserId, session.DeviceId)
                .ConfigureAwait(false);

            if (timerStatus?.IsActive == true)
                return; // timer already running, nothing to do

            var config = JellysleepPlugin.Instance!.Configuration;
            var request = new SleepTimerRequest
            {
                Type = config.AutoTimerType,
                Duration = config.AutoTimerType == "duration" ? config.AutoTimerDurationMinutes : null,
                EpisodeCount = config.AutoTimerType == "episode" ? config.AutoTimerEpisodeCount : null,
                Label = "Auto"
            };

            await _sleepTimerService.StartTimerAsync(session.UserId, session.DeviceId, request)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "Auto-started {Type} sleep timer on resume for user {UserId} on device {DeviceId}",
                config.AutoTimerType, session.UserId, session.DeviceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling playback progress event for auto-timer");
        }
    }

    private static bool ShouldAutoStart(string? deviceId)
    {
        var config = JellysleepPlugin.Instance?.Configuration;
        if (config == null || !config.AutoTimerEnabled) return false;

        if (string.IsNullOrWhiteSpace(config.AutoTimerDeviceIds)) return true;

        var allowed = config.AutoTimerDeviceIds
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return allowed.Contains(deviceId, StringComparer.OrdinalIgnoreCase);
    }
}