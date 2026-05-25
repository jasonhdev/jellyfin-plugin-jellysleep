using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Jellysleep.Configuration;

/// <summary>
/// Plugin configuration for Jellysleep.
/// The sleep timer is now automatic (30 minutes) and requires no user configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets the auto-pause interval in minutes.
    /// Default is 30. Admins can adjust this from the plugin config page.
    /// </summary>
    public int AutoPauseIntervalMinutes { get; set; } = 30;

    /// <summary>
    /// Gets or sets a value indicating whether to restart the timer
    /// automatically when playback resumes after a user-initiated pause.
    /// </summary>
    public bool RestartTimerOnResume { get; set; } = true;
}