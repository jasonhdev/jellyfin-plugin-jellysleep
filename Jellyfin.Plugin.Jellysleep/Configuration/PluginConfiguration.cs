using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Jellysleep.Configuration;

/// <summary>
/// Plugin configuration for Jellysleep.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether to auto-start a sleep timer on playback start.
    /// </summary>
    public bool AutoTimerEnabled { get; set; } = false;

    /// <summary>
    /// Gets or sets the auto-timer type: "duration" or "episode".
    /// </summary>
    public string AutoTimerType { get; set; } = "duration";

    /// <summary>
    /// Gets or sets the auto-timer duration in minutes (for duration type).
    /// </summary>
    public int AutoTimerDurationMinutes { get; set; } = 60;

    /// <summary>
    /// Gets or sets the auto-timer episode count (for episode type, null = stop after current).
    /// </summary>
    public int? AutoTimerEpisodeCount { get; set; } = null;

    /// <summary>
    /// Gets or sets a comma-separated list of device IDs to restrict auto-timer to.
    /// Leave empty to apply to all devices.
    /// </summary>
    public string AutoTimerDeviceIds { get; set; } = string.Empty;
}