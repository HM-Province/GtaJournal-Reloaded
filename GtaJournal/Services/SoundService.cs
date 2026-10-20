using Windows.Media.Core;
using Windows.Media.Playback;

namespace GtaJournal.Services;

public sealed class SoundService : IDisposable
{
    private readonly SettingsService _settings;
    private readonly Lazy<MediaPlayer?> _click;
    private readonly Lazy<MediaPlayer?> _alert;

    public SoundService(SettingsService settings)
    {
        _settings = settings;
        _click = new(() => CreatePlayer("click.mp3", 0.05));
        _alert = new(() => CreatePlayer("alert.ogg", 1.0));
    }

    public void PlayClick()
    {
        if (_settings.Current.ClickSound) Play(_click.Value);
    }

    public void PlayAlert()
    {
        if (_settings.Current.AlertSound) Play(_alert.Value);
    }

    private static void Play(MediaPlayer? player)
    {
        if (player is null) return;
        try
        {
            player.PlaybackSession.Position = TimeSpan.Zero;
            player.Play();
        }
        catch (Exception)
        {
            // Нет аудиоустройства и т.п. — звук не критичен.
        }
    }

    private static MediaPlayer? CreatePlayer(string file, double volume)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Sounds", file);
        if (!File.Exists(path)) return null;
        try
        {
            return new MediaPlayer
            {
                Source = MediaSource.CreateFromUri(new Uri(path)),
                Volume = volume,
                AutoPlay = false,
                AudioCategory = MediaPlayerAudioCategory.SoundEffects,
            };
        }
        catch (Exception)
        {
            return null;
        }
    }

    public void Dispose()
    {
        if (_click.IsValueCreated) _click.Value?.Dispose();
        if (_alert.IsValueCreated) _alert.Value?.Dispose();
    }
}
