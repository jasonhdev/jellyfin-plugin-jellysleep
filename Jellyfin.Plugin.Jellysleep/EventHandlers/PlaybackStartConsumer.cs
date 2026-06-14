using Jellyfin.Plugin.Jellysleep.Services;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Jellysleep.EventHandlers;

/// <summary>
/// Handles playback start events to prevent new playback when episode timer is active,
/// and auto-starts a sleep timer when configured to do so.
/// </summary>
public class PlaybackStartConsumer : IEventConsumer<PlaybackStartEventArgs>
{
    private readonly ILogger<PlaybackStartConsumer> _logger;
    private readonly ISleepTimerService _sleepTimerService;
    private readonly ISessionManager _sessionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaybackStartConsumer"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="sleepTimerService">The sleep timer service.</param>
    /// <param name="sessionManager">The session manager.</param>
    public PlaybackStartConsumer(
        ILogger<PlaybackStartConsumer> logger,
        ISleepTimerService sleepTimerService,
        ISessionManager sessionManager)
    {
        _logger = logger;
        _sleepTimerService = sleepTimerService;
        _sessionManager = sessionManager;
    }

    /// <inheritdoc />
    public async Task OnEvent(PlaybackStartEventArgs eventArgs)
    {
        try
        {
            var session = eventArgs.Session;

            if (session?.UserId == null || session.UserId == Guid.Empty)
            {
                _logger.LogDebug("Playback started with no valid user or session ID.");
                return;
            }

            // Check if we're in a cooldown period after timer completion
            var inCooldown = await _sleepTimerService.IsInCooldownAsync(session.UserId, session.DeviceId).ConfigureAwait(false);
            if (inCooldown)
            {
                _logger.LogInformation(
                    "Stopping playback in session {SessionId} for user {UserId} - in cooldown period after timer completion. Item: {ItemName}",
                    session.Id,
                    session.UserId,
                    eventArgs.Item?.Name ?? "Unknown");

                // Pass null as controlling session — server-originated command
                await _sessionManager.SendPlaystateCommand(
                    null,
                    session.Id,
                    new MediaBrowser.Model.Session.PlaystateRequest
                    {
                        Command = MediaBrowser.Model.Session.PlaystateCommand.Stop
                    },
                    CancellationToken.None).ConfigureAwait(false);

                return;
            }
            else
            {
                _logger.LogDebug(
                    "No cooldown for user {UserId} on device {DeviceId}. Continuing playback.",
                    session.UserId,
                    session.DeviceId);
            }

            // Check if there is an active sleep timer for this user and device
            var timerStatus = await _sleepTimerService.GetTimerStatusAsync(session.UserId, session.DeviceId).ConfigureAwait(false);

            if (timerStatus != null && timerStatus.IsActive)
            {
                _logger.LogInformation(
                    "Playback started for user {UserId} in session {SessionId}, item: {ItemName}",
                    session.UserId,
                    session.Id,
                    eventArgs.Item?.Name ?? "Unknown");

                if (timerStatus.Type == "episode" && timerStatus.EpisodeCount >= 1)
                {
                    if (timerStatus.EpisodesPlayed >= timerStatus.EpisodeCount)
                    {
                        _logger.LogInformation(
                            "Stopping playback in session {SessionId} for user {UserId} - episode timer target already reached. Episodes: {EpisodesPlayed}/{EpisodeCount}, Item: {ItemName}",
                            session.Id,
                            session.UserId,
                            timerStatus.EpisodesPlayed,
                            timerStatus.EpisodeCount,
                            eventArgs.Item?.Name ?? "Unknown");

                        // Pass null as controlling session — server-originated command
                        await _sessionManager.SendPlaystateCommand(
                            null,
                            session.Id,
                            new MediaBrowser.Model.Session.PlaystateRequest
                            {
                                Command = MediaBrowser.Model.Session.PlaystateCommand.Stop
                            },
                            CancellationToken.None).ConfigureAwait(false);

                        await _sleepTimerService.HandlePlaybackStopAsync(session.UserId, session.Id).ConfigureAwait(false);
                    }
                    else
                    {
                        _logger.LogDebug(
                            "Allowing new playback in session {SessionId} for user {UserId} - episode timer still has episodes remaining. Episodes: {EpisodesPlayed}/{EpisodeCount}, Item: {ItemName}",
                            session.Id,
                            session.UserId,
                            timerStatus.EpisodesPlayed,
                            timerStatus.EpisodeCount,
                            eventArgs.Item?.Name ?? "Unknown");
                    }
                }
            }
            else
            {
                // No active timer — check if we should auto-start one
                if (ShouldAutoStart(session.DeviceId))
                {
                    var config = JellysleepPlugin.Instance!.Configuration;
                    var request = new Models.SleepTimerRequest
                    {
                        Type = config.AutoTimerType,
                        Duration = config.AutoTimerType == "duration" ? config.AutoTimerDurationMinutes : null,
                        EpisodeCount = config.AutoTimerType == "episode" ? config.AutoTimerEpisodeCount : null,
                        Label = "Auto"
                    };

                    await _sleepTimerService.StartTimerAsync(session.UserId, session.DeviceId, request)
                        .ConfigureAwait(false);

                    _logger.LogInformation(
                        "Auto-started {Type} sleep timer on playback start for user {UserId} on device {DeviceId}",
                        config.AutoTimerType, session.UserId, session.DeviceId);
                }
                else
                {
                    _logger.LogDebug("No active sleep timer for user {UserId} on device {DeviceId}.", session.UserId, session.DeviceId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling playback started event");
        }
    }

    /// <summary>
    /// Determines whether an auto sleep timer should be started for the given device,
    /// based on the plugin configuration.
    /// </summary>
    /// <param name="deviceId">The device ID to check.</param>
    /// <returns><c>true</c> if an auto timer should be started; otherwise <c>false</c>.</returns>
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