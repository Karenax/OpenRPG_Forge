using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forge.Domain.Modules;
using Forge.ModuleFormat.Launcher;
using Forge.Network.Transport;

namespace Forge.GameLauncher;

internal partial class LauncherWindow
{
    private static readonly (string Label, int Width, int Height)[] Resolutions =
    [
        ("1280 × 720", 1280, 720),
        ("1600 × 900", 1600, 900),
        ("1920 × 1080", 1920, 1080),
        ("2560 × 1440", 2560, 1440),
        ("3840 × 2160", 3840, 2160),
    ];

    private static readonly (string Label, LauncherWindowMode Mode)[] WindowModes =
    [
        ("Fenêtré plein écran · sans bordure", LauncherWindowMode.Windowed),
        ("Plein écran", LauncherWindowMode.Fullscreen),
    ];

    private readonly GameLauncherVerification _verification;
    private readonly string _settingsPath;
    private readonly HttpClient _publicAddressHttpClient;
    private readonly PublicInternetAddressResolver _publicAddressResolver;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly MediaPlayer _launcherMusicPlayer = new();
    private bool _initializing = true;
    private bool _captureMode;
    private bool _publicAddressLookupStarted;
    private bool _publicAddressLookupInProgress;
    private bool _publicAddressLookupAttempted;
    private bool _updatingPublicAddress;
    private bool _reduceMotion;
    private bool _atmosphereStarted;
    private bool _launcherMusicIsPlaying;
    private bool _syncingLauncherMusicVolume;
    private string _activePage = "home";
    private double _parallaxTargetX;
    private double _parallaxTargetY;
    private double _parallaxX;
    private double _parallaxY;
    private string? _capturePublicAddress;
    private Guid _networkAccountId;
    private string _networkJoinToken = string.Empty;
    private string _serverCertificateSha256 = string.Empty;
    private string _hostCertificatePath = string.Empty;
    private string _hostCertificatePassword = string.Empty;

    public LauncherWindow(
        GameLauncherVerification verification,
        GameLauncherUserSettings settings,
        string settingsPath)
    {
        _verification = verification ?? throw new ArgumentNullException(nameof(verification));
        ArgumentNullException.ThrowIfNull(settings);
        _settingsPath = Path.GetFullPath(settingsPath);
        _publicAddressHttpClient = new HttpClient(new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            ConnectTimeout = TimeSpan.FromSeconds(3),
        })
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        _publicAddressResolver = new PublicInternetAddressResolver(_publicAddressHttpClient);
        InitializeComponent();
        _launcherMusicPlayer.MediaEnded += OnLauncherMusicEnded;
        _launcherMusicPlayer.MediaFailed += OnLauncherMusicFailed;

        Title = verification.Manifest.DisplayName;
        GameTitleText.Text = verification.Manifest.DisplayName.ToUpperInvariant();
        TaglineText.Text = verification.Manifest.Tagline;
        VersionText.Text = $"VERSION {verification.Manifest.Version}";
        MultiplayerCardText.Text = MultiplayerTitle();
        SettingsNavigation.IsEnabled = verification.Manifest.SettingsEnabled;
        MultiplayerNavigation.IsEnabled = verification.Manifest.Multiplayer != GameLauncherMultiplayerMode.Disabled;
        LocalHostMode.IsEnabled = verification.Manifest.Multiplayer != GameLauncherMultiplayerMode.Disabled;
        DirectConnectMode.IsEnabled =
            verification.Manifest.Multiplayer == GameLauncherMultiplayerMode.DirectConnectPreview;
        var internetEnabled = verification.Manifest.Multiplayer == GameLauncherMultiplayerMode.DirectConnectPreview;
        HostModeTitleText.Text = internetEnabled ? "HÉBERGER" : "HÔTE LOCAL";
        HostModeSubtitleText.Text = internetEnabled ? "Partie Internet directe" : "Session sur cet ordinateur";
        NewsButton.Visibility = verification.Manifest.NewsUrl is null ? Visibility.Collapsed : Visibility.Visible;
        SupportButton.Visibility = verification.Manifest.SupportUrl is null ? Visibility.Collapsed : Visibility.Visible;

        ApplyAccent(verification.Manifest.AccentColor);
        if (verification.BackgroundImagePath is not null)
            BackdropImage.Source = LoadBitmap(verification.BackgroundImagePath);

        ResolutionSelector.ItemsSource = Resolutions.Select(item => item.Label).ToArray();
        WindowModeSelector.ItemsSource = WindowModes.Select(item => item.Label).ToArray();
        QualitySelector.ItemsSource = new[] { "Faible", "Moyenne", "Élevée", "Ultra" };
        LoadSettings(settings);
        ConfigureLauncherMusic(settings);
        ShowPageImmediately("home");
        _initializing = false;
        UpdateModeControls();
        UpdateWindowModeHint();
        StateChanged += (_, _) => MaximizeButton.Content = WindowState == WindowState.Maximized ? "❐" : "□";
    }

    public async Task CapturePreviewAsync(
        string path,
        string page,
        int width = 1440,
        int height = 860,
        string? sessionMode = null,
        string? publicAddress = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (width < 1180 || height < 720 || width > 3840 || height > 2160)
            throw new ArgumentOutOfRangeException(nameof(width), "La capture du lanceur doit rester entre 1180 × 720 et 3840 × 2160.");
        _captureMode = true;
        _capturePublicAddress = NormalizeCapturePublicAddress(publicAddress);
        _reduceMotion = true;
        WindowStartupLocation = WindowStartupLocation.Manual;
        WindowState = WindowState.Normal;
        Left = -32_000;
        Top = -32_000;
        Width = width;
        Height = height;
        ResizeMode = ResizeMode.NoResize;
        Show();
        SelectCaptureSessionMode(sessionMode);
        ShowPageImmediately(NormalizePage(page));
        await Dispatcher.InvokeAsync(() => UpdateLayout(), DispatcherPriority.Loaded);
        await Dispatcher.InvokeAsync(() => UpdateLayout(), DispatcherPriority.ApplicationIdle);

        var destination = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var renderWidth = Math.Max(1, (int)Math.Ceiling(RootVisual.ActualWidth));
        var renderHeight = Math.Max(1, (int)Math.Ceiling(RootVisual.ActualHeight));
        var bitmap = new RenderTargetBitmap(renderWidth, renderHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(RootVisual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        await using (var stream = File.Create(destination))
            encoder.Save(stream);
        Close();
    }

    private void SelectCaptureSessionMode(string? sessionMode)
    {
        if (string.IsNullOrWhiteSpace(sessionMode)) return;
        switch (sessionMode.Trim().ToLowerInvariant())
        {
            case "solo":
                SoloMode.IsChecked = true;
                break;
            case "host" or "local-host" or "internet-host":
                LocalHostMode.IsChecked = true;
                break;
            case "join" or "internet-join":
                if (!DirectConnectMode.IsEnabled)
                    throw new InvalidOperationException("This game does not enable direct Internet connections.");
                DirectConnectMode.IsChecked = true;
                break;
            default:
                throw new ArgumentException("Le mode de capture doit être solo, host ou join.", nameof(sessionMode));
        }

        UpdateModeControls();
    }

    private void OnLoaded(object sender, RoutedEventArgs eventArgs)
    {
        CompositionTarget.Rendering += OnCompositionFrame;
        UpdateModeControls();
        if (!_captureMode) StartLauncherMusic();
        if (_captureMode || _reduceMotion)
        {
            RevealWithoutMotion();
            CreateParticles(animate: false);
            return;
        }

        StartAtmosphere();
        Animate(HeroIdentity, UIElement.OpacityProperty, 1, 760, 120);
        Animate(HeroTranslate, TranslateTransform.YProperty, 0, 760, 120);
        Animate(CommandPanel, UIElement.OpacityProperty, 1, 620, 240);
        Animate(PanelTranslate, TranslateTransform.XProperty, 0, 720, 240);
    }

    private void OnClosed(object? sender, EventArgs eventArgs)
    {
        _lifetimeCancellation.Cancel();
        _publicAddressHttpClient.Dispose();
        _lifetimeCancellation.Dispose();
        CompositionTarget.Rendering -= OnCompositionFrame;
        StopAtmosphere(createStaticParticles: false);
        _launcherMusicPlayer.Stop();
        _launcherMusicPlayer.Close();
        _launcherMusicPlayer.MediaEnded -= OnLauncherMusicEnded;
        _launcherMusicPlayer.MediaFailed -= OnLauncherMusicFailed;
    }

    private void RevealWithoutMotion()
    {
        HeroIdentity.BeginAnimation(UIElement.OpacityProperty, null);
        CommandPanel.BeginAnimation(UIElement.OpacityProperty, null);
        HeroTranslate.BeginAnimation(TranslateTransform.YProperty, null);
        PanelTranslate.BeginAnimation(TranslateTransform.XProperty, null);
        HeroIdentity.Opacity = 1;
        CommandPanel.Opacity = 1;
        HeroTranslate.Y = 0;
        PanelTranslate.X = 0;
    }

    private void StartAtmosphere()
    {
        if (_atmosphereStarted) return;
        _atmosphereStarted = true;
        CreateParticles(animate: true);
        FogTranslateOne.BeginAnimation(
            TranslateTransform.XProperty,
            new DoubleAnimation(-45, 85, TimeSpan.FromSeconds(24))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
            });
        FogTranslateTwo.BeginAnimation(
            TranslateTransform.XProperty,
            new DoubleAnimation(65, -55, TimeSpan.FromSeconds(31))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
            });
    }

    private void StopAtmosphere(bool createStaticParticles = true)
    {
        _atmosphereStarted = false;
        FogTranslateOne.BeginAnimation(TranslateTransform.XProperty, null);
        FogTranslateTwo.BeginAnimation(TranslateTransform.XProperty, null);
        FogTranslateOne.X = 0;
        FogTranslateTwo.X = 0;
        ParticleCanvas.Children.Clear();
        if (createStaticParticles) CreateParticles(animate: false);
    }

    private void CreateParticles(bool animate)
    {
        ParticleCanvas.Children.Clear();
        var random = new Random(0x0F0A6E);
        var width = ActualWidth > 0 ? ActualWidth : Width;
        var height = ActualHeight > 0 ? ActualHeight : Height;
        for (var index = 0; index < 32; index++)
        {
            var size = 1.5 + random.NextDouble() * 3.1;
            var left = random.NextDouble() * width * 0.66;
            var top = height * (0.34 + random.NextDouble() * 0.7);
            var mote = new System.Windows.Shapes.Ellipse
            {
                Width = size,
                Height = size,
                Opacity = 0.18 + random.NextDouble() * 0.42,
                Fill = new RadialGradientBrush(
                    Color.FromArgb(255, 255, 244, 188),
                    Color.FromArgb(6, 255, 220, 116)),
                IsHitTestVisible = false,
            };
            Canvas.SetLeft(mote, left);
            Canvas.SetTop(mote, top);
            ParticleCanvas.Children.Add(mote);
            if (!animate) continue;

            var speed = 7 + random.NextDouble() * 10;
            var travelSeconds = Math.Max(8, (top + 40) / speed);
            mote.BeginAnimation(
                Canvas.TopProperty,
                new DoubleAnimation(top, -35, TimeSpan.FromSeconds(travelSeconds))
                {
                    RepeatBehavior = RepeatBehavior.Forever,
                    BeginTime = TimeSpan.FromMilliseconds(random.Next(0, 2200)),
                });
            var drift = new TranslateTransform();
            mote.RenderTransform = drift;
            drift.BeginAnimation(
                TranslateTransform.XProperty,
                new DoubleAnimation(-10 - random.NextDouble() * 8, 12 + random.NextDouble() * 14,
                    TimeSpan.FromSeconds(2.6 + random.NextDouble() * 3.8))
                {
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
                });
            mote.BeginAnimation(
                UIElement.OpacityProperty,
                new DoubleAnimation(0.12, 0.58, TimeSpan.FromSeconds(1.8 + random.NextDouble() * 2.4))
                {
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                });
        }
    }

    private void OnWindowMouseMove(object sender, MouseEventArgs eventArgs)
    {
        if (_captureMode || _reduceMotion || ActualWidth <= 0 || ActualHeight <= 0) return;
        var position = eventArgs.GetPosition(this);
        _parallaxTargetX = (0.5 - position.X / ActualWidth) * 18;
        _parallaxTargetY = (0.5 - position.Y / ActualHeight) * 12;
    }

    private void OnCompositionFrame(object? sender, EventArgs eventArgs)
    {
        if (_captureMode || _reduceMotion) return;
        _parallaxX += (_parallaxTargetX - _parallaxX) * 0.065;
        _parallaxY += (_parallaxTargetY - _parallaxY) * 0.065;
        BackdropTranslate.X = _parallaxX;
        BackdropTranslate.Y = _parallaxY;
    }

    private void OnNavigationChecked(object sender, RoutedEventArgs eventArgs)
    {
        if (sender is not RadioButton { IsChecked: true, Tag: string page } ||
            HomePage is null || SettingsPage is null || MultiplayerPage is null)
            return;
        if (!IsLoaded || _initializing)
        {
            ShowPageImmediately(page);
            return;
        }

        NavigateTo(page);
    }

    private void NavigateTo(string page)
    {
        page = NormalizePage(page);
        if (string.Equals(_activePage, page, StringComparison.Ordinal)) return;
        var outgoing = PageFor(_activePage);
        var incoming = PageFor(page);
        _activePage = page;
        if (_reduceMotion || _captureMode)
        {
            ShowPageImmediately(page);
            return;
        }

        incoming.Visibility = Visibility.Visible;
        incoming.Opacity = 0;
        if (incoming.RenderTransform is TranslateTransform incomingTransform) incomingTransform.X = 25;
        var fadeOut = new DoubleAnimation(0, TimeSpan.FromMilliseconds(145));
        fadeOut.Completed += (_, _) => outgoing.Visibility = Visibility.Collapsed;
        outgoing.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        if (outgoing.RenderTransform is TranslateTransform outgoingTransform)
            Animate(outgoingTransform, TranslateTransform.XProperty, -14, 155);
        Animate(incoming, UIElement.OpacityProperty, 1, 285, 70);
        if (incoming.RenderTransform is TranslateTransform transform)
            Animate(transform, TranslateTransform.XProperty, 0, 310, 70);
    }

    private void ShowPageImmediately(string page)
    {
        page = NormalizePage(page);
        _activePage = page;
        foreach (var (name, element) in new[]
                 {
                     ("home", HomePage),
                     ("settings", SettingsPage),
                     ("multiplayer", MultiplayerPage),
                 })
        {
            element.BeginAnimation(UIElement.OpacityProperty, null);
            element.Opacity = string.Equals(name, page, StringComparison.Ordinal) ? 1 : 0;
            element.Visibility = string.Equals(name, page, StringComparison.Ordinal)
                ? Visibility.Visible
                : Visibility.Collapsed;
            if (element.RenderTransform is TranslateTransform transform)
            {
                transform.BeginAnimation(TranslateTransform.XProperty, null);
                transform.X = 0;
            }
        }

        if (HomeNavigation is not null) HomeNavigation.IsChecked = page == "home";
        if (SettingsNavigation is not null) SettingsNavigation.IsChecked = page == "settings";
        if (MultiplayerNavigation is not null) MultiplayerNavigation.IsChecked = page == "multiplayer";
    }

    private FrameworkElement PageFor(string page) => NormalizePage(page) switch
    {
        "settings" => SettingsPage,
        "multiplayer" => MultiplayerPage,
        _ => HomePage,
    };

    private static string NormalizePage(string? page) => page?.Trim().ToLowerInvariant() switch
    {
        "settings" or "configuration" => "settings",
        "multiplayer" or "multi" => "multiplayer",
        _ => "home",
    };

    private void LoadSettings(GameLauncherUserSettings settings)
    {
        var resolutionIndex = Array.FindIndex(Resolutions, item =>
            item.Width == settings.ResolutionWidth && item.Height == settings.ResolutionHeight);
        ResolutionSelector.SelectedIndex = Math.Max(0, resolutionIndex);
        var canonicalWindowMode = LauncherWindowModePolicy.Canonicalize(settings.WindowMode);
        var windowModeIndex = Array.FindIndex(WindowModes, item => item.Mode == canonicalWindowMode);
        WindowModeSelector.SelectedIndex = Math.Max(0, windowModeIndex);
        QualitySelector.SelectedIndex = (int)settings.GraphicsQuality;
        VSyncCheck.IsChecked = settings.VSync;
        MasterVolumeSlider.Value = settings.MasterVolume;
        MusicVolumeSlider.Value = settings.MusicVolume;
        EffectsVolumeSlider.Value = settings.EffectsVolume;
        ReduceMotionCheck.IsChecked = settings.ReduceMotion;
        _reduceMotion = settings.ReduceMotion;
        SoloMode.IsChecked = settings.SessionMode == LauncherSessionMode.Solo;
        LocalHostMode.IsChecked = settings.SessionMode == LauncherSessionMode.LocalHost;
        DirectConnectMode.IsChecked = settings.SessionMode == LauncherSessionMode.DirectConnectPreview;
        PlayerNameText.Text = settings.PlayerName;
        ServerAddressText.Text = settings.ServerAddress;
        ServerPortText.Text = settings.ServerPort.ToString();
        _networkAccountId = settings.NetworkAccountId;
        _networkJoinToken = settings.NetworkJoinToken;
        _serverCertificateSha256 = settings.ServerCertificateSha256;
        _hostCertificatePath = settings.HostCertificatePath;
        _hostCertificatePassword = settings.HostCertificatePassword;
        UpdateVolumeLabels();
    }

    private void ConfigureLauncherMusic(GameLauncherUserSettings settings)
    {
        if (_verification.MusicPath is null)
        {
            LauncherMusicPanel.Visibility = Visibility.Collapsed;
            return;
        }

        LauncherMusicPanel.Visibility = Visibility.Visible;
        SyncLauncherMusicVolume(settings.MusicVolume, updateSettingsSlider: false);
        _launcherMusicPlayer.Open(new Uri(_verification.MusicPath, UriKind.Absolute));
    }

    private void StartLauncherMusic()
    {
        if (_verification.MusicPath is null || _captureMode) return;
        _launcherMusicPlayer.Play();
        _launcherMusicIsPlaying = true;
        LauncherMusicToggleButton.Content = "■ STOP";
    }

    private void StopLauncherMusic()
    {
        if (_verification.MusicPath is null) return;
        _launcherMusicPlayer.Stop();
        _launcherMusicIsPlaying = false;
        LauncherMusicToggleButton.Content = "▶ PLAY";
    }

    private void OnLauncherMusicToggleClick(object sender, RoutedEventArgs eventArgs)
    {
        if (_launcherMusicIsPlaying) StopLauncherMusic();
        else StartLauncherMusic();
    }

    private void OnLauncherMusicEnded(object? sender, EventArgs eventArgs)
    {
        if (!_launcherMusicIsPlaying || _captureMode) return;
        _launcherMusicPlayer.Position = TimeSpan.Zero;
        _launcherMusicPlayer.Play();
    }

    private void OnLauncherMusicFailed(object? sender, ExceptionEventArgs eventArgs)
    {
        _launcherMusicIsPlaying = false;
        LauncherMusicToggleButton.Content = "▶ PLAY";
        SetStatus("Musique du lanceur indisponible : " + eventArgs.ErrorException.Message, error: true);
    }

    private void OnLauncherMusicVolumeChanged(
        object sender,
        RoutedPropertyChangedEventArgs<double> eventArgs)
    {
        if (_syncingLauncherMusicVolume || LauncherMusicVolumeValue is null) return;
        SyncLauncherMusicVolume(eventArgs.NewValue, updateSettingsSlider: true);
        OnSettingsChanged(sender, eventArgs);
    }

    private void SyncLauncherMusicVolume(double value, bool updateSettingsSlider)
    {
        if (_syncingLauncherMusicVolume) return;
        _syncingLauncherMusicVolume = true;
        try
        {
            var normalized = Math.Clamp(value, 0, 100);
            LauncherMusicVolumeSlider.Value = normalized;
            LauncherMusicVolumeValue.Text = $"{(int)Math.Round(normalized)} %";
            _launcherMusicPlayer.Volume = normalized / 100d;
            if (updateSettingsSlider) MusicVolumeSlider.Value = normalized;
        }
        finally
        {
            _syncingLauncherMusicVolume = false;
        }
    }

    private GameLauncherUserSettings ReadSettings()
    {
        var resolutionIndex = Math.Clamp(ResolutionSelector.SelectedIndex, 0, Resolutions.Length - 1);
        var selectedResolution = Resolutions[resolutionIndex];
        var windowModeIndex = Math.Clamp(WindowModeSelector.SelectedIndex, 0, WindowModes.Length - 1);
        var selectedWindowMode = WindowModes[windowModeIndex].Mode;
        if (!int.TryParse(ServerPortText.Text.Trim(), out var serverPort))
            throw new InvalidDataException("Le port serveur doit être un nombre entre 1024 et 65535.");
        var sessionMode = LocalHostMode.IsChecked == true
            ? LauncherSessionMode.LocalHost
            : DirectConnectMode.IsChecked == true
                ? LauncherSessionMode.DirectConnectPreview
                : LauncherSessionMode.Solo;
        var serverAddress = ServerAddressText.Text.Trim();
        if (sessionMode == LauncherSessionMode.DirectConnectPreview &&
            InternetSessionInvite.TryDecode(serverAddress, out var invite))
        {
            serverAddress = invite.Address;
            serverPort = invite.Port;
            _networkJoinToken = invite.AuthenticationToken;
            _serverCertificateSha256 = invite.CertificateSha256.ToLowerInvariant();
        }
        if (_networkAccountId == Guid.Empty) _networkAccountId = Guid.NewGuid();
        var settings = new GameLauncherUserSettings(
            GameLauncherUserSettings.CurrentSchemaVersion,
            selectedResolution.Width,
            selectedResolution.Height,
            selectedWindowMode,
            (LauncherGraphicsQuality)Math.Max(0, QualitySelector.SelectedIndex),
            VSyncCheck.IsChecked == true,
            (int)Math.Round(MasterVolumeSlider.Value),
            (int)Math.Round(MusicVolumeSlider.Value),
            (int)Math.Round(EffectsVolumeSlider.Value),
            ReduceMotionCheck.IsChecked == true,
            sessionMode,
            PlayerNameText.Text.Trim(),
            serverAddress,
            serverPort,
            _networkAccountId,
            _networkJoinToken,
            _serverCertificateSha256,
            _hostCertificatePath,
            _hostCertificatePassword);
        var diagnostics = GameLauncherUserSettingsValidator.Validate(settings);
        if (diagnostics.Count > 0)
            throw new InvalidDataException("Réglages invalides : " + string.Join(", ", diagnostics));
        return settings;
    }

    private void OnSaveClick(object sender, RoutedEventArgs eventArgs) => SaveFromControls();

    private void SaveFromControls()
    {
        try
        {
            if (IsInternetHostMode()) EnsureHostSecurityMaterial();
            var settings = ReadSettings();
            GameLauncherSettingsStore.Save(_settingsPath, settings);
            SetStatus("Configuration enregistrée.", error: false);
            UpdateLaunchSummary(settings);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException or IOException or
                                           UnauthorizedAccessException)
        {
            SetStatus("Configuration non enregistrée : " + exception.Message, error: true);
        }
    }

    private void OnPlayClick(object sender, RoutedEventArgs eventArgs)
    {
        try
        {
            if (IsInternetHostMode()) EnsureHostSecurityMaterial();
            var settings = ReadSettings();
            GameLauncherSettingsStore.Save(_settingsPath, settings);
            var plan = GameLaunchPlanBuilder.Create(_verification, settings, _settingsPath);
            var startInfo = new ProcessStartInfo
            {
                FileName = plan.ExecutablePath,
                WorkingDirectory = plan.WorkingDirectory,
                UseShellExecute = false,
            };
            foreach (var argument in plan.Arguments) startInfo.ArgumentList.Add(argument);
            _ = Process.Start(startInfo) ?? throw new InvalidOperationException("Le runtime n’a pas démarré.");
            Close();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException or IOException or
                                           UnauthorizedAccessException or InvalidOperationException)
        {
            SetStatus("Lancement impossible : " + exception.Message, error: true);
            MessageBox.Show(exception.Message, "Lancement impossible", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnSessionModeChanged(object sender, RoutedEventArgs eventArgs)
    {
        if (_initializing) return;
        UpdateModeControls();
    }

    private void UpdateModeControls()
    {
        var direct = DirectConnectMode.IsChecked == true;
        var host = LocalHostMode.IsChecked == true;
        var internetEnabled = _verification.Manifest.Multiplayer == GameLauncherMultiplayerMode.DirectConnectPreview;
        var internetHost = host && internetEnabled;
        ServerAddressLabel.Text = internetHost
            ? "IP publique à partager"
            : host
                ? "Adresse interne"
                : "Invitation openrpg2:";
        ServerAddressText.Visibility = internetHost ? Visibility.Collapsed : Visibility.Visible;
        PublicJoinAddressPanel.Visibility = internetHost ? Visibility.Visible : Visibility.Collapsed;
        ServerAddressText.IsEnabled = direct;
        ServerPortText.IsEnabled = direct || host;
        PlayButton.IsEnabled = true;
        MultiplayerPlayButton.IsEnabled = true;
        var actionContent = host
            ? internetEnabled
                ? "HÉBERGER SUR INTERNET"
                : "HÉBERGER ET JOUER"
            : direct
                ? "REJOINDRE LA PARTIE"
                : "ENTRER DANS L’AVENTURE";
        PlayButton.Content = actionContent;
        MultiplayerPlayButton.Content = actionContent;
        if (internetHost && _captureMode)
        {
            _publicAddressLookupStarted = true;
            _publicAddressLookupAttempted = true;
            SetPublicAddressText(_capturePublicAddress ?? "203.0.113.42");
        }

        UpdatePublicAddressControls();
        UpdateMultiplayerNotice();
        if (internetHost && IsLoaded && !_captureMode)
            _ = EnsurePublicAddressAsync();
        try
        {
            UpdateLaunchSummary(ReadSettings());
        }
        catch (InvalidDataException)
        {
            // The editable form shows validation when the user saves or launches.
        }
    }

    private void UpdateLaunchSummary(GameLauncherUserSettings settings)
    {
        var mode = settings.SessionMode switch
        {
            LauncherSessionMode.LocalHost when
                _verification.Manifest.Multiplayer == GameLauncherMultiplayerMode.DirectConnectPreview =>
                "Hôte Internet direct",
            LauncherSessionMode.LocalHost => "Hôte local",
            LauncherSessionMode.DirectConnectPreview => "Rejoindre par Internet",
            _ => "Mode solo",
        };
        var quality = settings.GraphicsQuality switch
        {
            LauncherGraphicsQuality.Low => "qualité faible",
            LauncherGraphicsQuality.Medium => "qualité moyenne",
            LauncherGraphicsQuality.Ultra => "qualité ultra",
            _ => "qualité élevée",
        };
        LaunchSummaryText.Text =
            $"{mode}  /  {settings.ResolutionWidth} × {settings.ResolutionHeight}  /  {quality}";
    }

    private void OnSettingsChanged(object sender, RoutedEventArgs eventArgs)
    {
        if (_initializing) return;
        UpdateWindowModeHint();
        UpdatePublicAddressControls();
        UpdateMultiplayerNotice();
        try
        {
            UpdateLaunchSummary(ReadSettings());
        }
        catch (InvalidDataException)
        {
            // Validation is reported on save or launch.
        }
    }

    private void UpdateWindowModeHint()
    {
        if (WindowModeHintText is null) return;
        WindowModeHintText.Text = WindowModeSelector.SelectedIndex switch
        {
            1 => "Plein écran par défaut. Utilisez Alt+Tab pour revenir au bureau.",
            _ => "Fenêtré plein écran sans bordure : toute la surface du moniteur, avec Alt+Tab immédiat.",
        };
    }

    private void OnVolumeChanged(object sender, RoutedPropertyChangedEventArgs<double> eventArgs)
    {
        if (MasterVolumeValue is null || MusicVolumeValue is null || EffectsVolumeValue is null) return;
        UpdateVolumeLabels();
        if (ReferenceEquals(sender, MusicVolumeSlider))
            SyncLauncherMusicVolume(MusicVolumeSlider.Value, updateSettingsSlider: false);
        OnSettingsChanged(sender, eventArgs);
    }

    private void UpdateVolumeLabels()
    {
        MasterVolumeValue.Text = $"{(int)Math.Round(MasterVolumeSlider.Value)} %";
        MusicVolumeValue.Text = $"{(int)Math.Round(MusicVolumeSlider.Value)} %";
        EffectsVolumeValue.Text = $"{(int)Math.Round(EffectsVolumeSlider.Value)} %";
    }

    private void OnReduceMotionChanged(object sender, RoutedEventArgs eventArgs)
    {
        if (_initializing) return;
        _reduceMotion = ReduceMotionCheck.IsChecked == true;
        if (IsLoaded)
        {
            if (_reduceMotion) StopAtmosphere();
            else StartAtmosphere();
        }
        OnSettingsChanged(sender, eventArgs);
    }

    private void OnPortPreviewTextInput(object sender, TextCompositionEventArgs eventArgs) =>
        eventArgs.Handled = eventArgs.Text.Any(character => !char.IsDigit(character));

    private void OnPortDownClick(object sender, RoutedEventArgs eventArgs) => AdjustPort(-1);

    private void OnPortUpClick(object sender, RoutedEventArgs eventArgs) => AdjustPort(1);

    private void AdjustPort(int delta)
    {
        var current = int.TryParse(ServerPortText.Text, out var parsed)
            ? parsed
            : _verification.Manifest.DefaultServerPort;
        ServerPortText.Text = Math.Clamp(current + delta, 1024, 65_535).ToString();
    }

    private async Task EnsurePublicAddressAsync(bool force = false)
    {
        if (_captureMode || _lifetimeCancellation.IsCancellationRequested ||
            !IsInternetHostMode() || _publicAddressLookupInProgress ||
            (_publicAddressLookupStarted && !force))
            return;

        _publicAddressLookupStarted = true;
        _publicAddressLookupInProgress = true;
        _publicAddressLookupAttempted = false;
        SetPublicAddressText("Détection en cours…");
        UpdatePublicAddressControls();
        UpdateMultiplayerNotice();

        PublicInternetAddressLookupResult result;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCancellation.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            result = await _publicAddressResolver.ResolveIpv4Async(timeout.Token);
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            return;
        }
        catch (ObjectDisposedException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            return;
        }
        catch (OperationCanceledException)
        {
            result = new PublicInternetAddressLookupResult(
                false,
                null,
                PublicInternetAddressResolver.LookupFailedCode);
        }

        if (_lifetimeCancellation.IsCancellationRequested) return;
        _publicAddressLookupInProgress = false;
        _publicAddressLookupAttempted = true;
        SetPublicAddressText(result.Succeeded ? result.Address ?? string.Empty : string.Empty);
        UpdatePublicAddressControls();
        UpdateMultiplayerNotice();
        SetStatus(
            result.Succeeded
                ? "Adresse Internet détectée. Vous pouvez la copier avec le port."
                : "Adresse Internet non détectée : saisissez une IP publique ou un DNS.",
            error: !result.Succeeded);
    }

    private async void OnRefreshPublicAddressClick(object sender, RoutedEventArgs eventArgs) =>
        await EnsurePublicAddressAsync(force: true);

    private void OnPublicJoinAddressChanged(object sender, TextChangedEventArgs eventArgs)
    {
        if (_initializing || _updatingPublicAddress) return;
        _publicAddressLookupAttempted = true;
        UpdatePublicAddressControls();
        UpdateMultiplayerNotice();
    }

    private void OnCopyPublicAddressClick(object sender, RoutedEventArgs eventArgs)
    {
        if (!TryBuildPublicJoinEndpoint(out var endpoint))
        {
            SetStatus("Adresse Internet ou port invalide : rien n’a été copié.", error: true);
            return;
        }

        try
        {
            EnsureHostSecurityMaterial();
            var separator = endpoint.LastIndexOf(':');
            var invite = new InternetSessionInvite(
                InternetSessionInvite.CurrentSchemaVersion,
                endpoint[..separator],
                int.Parse(endpoint[(separator + 1)..], System.Globalization.CultureInfo.InvariantCulture),
                _serverCertificateSha256,
                _networkJoinToken);
            Clipboard.SetText(invite.Encode());
            GameLauncherSettingsStore.Save(_settingsPath, ReadSettings());
            SetStatus($"Invitation TLS copiée pour {endpoint}. Elle contient un jeton privé : partagez-la uniquement avec vos joueurs.", error: false);
        }
        catch (Exception exception) when (exception is ExternalException or InvalidOperationException or
                                           IOException or UnauthorizedAccessException or CryptographicException or
                                           ArgumentException or InvalidDataException)
        {
            SetStatus("Invitation non copiée : " + exception.Message, error: true);
        }
    }

    private void EnsureHostSecurityMaterial()
    {
        if (_captureMode) return;
        var settingsDirectory = Path.GetDirectoryName(_settingsPath)
            ?? throw new InvalidOperationException("Le dossier des réglages du lanceur est indisponible.");
        Directory.CreateDirectory(settingsDirectory);
        var certificatePath = Path.Combine(settingsDirectory, "internet-host-certificate.pfx");
        if (string.Equals(_hostCertificatePath, certificatePath, StringComparison.OrdinalIgnoreCase) &&
            _networkJoinToken.Length is >= 24 and <= 256 &&
            _hostCertificatePassword.Length is >= 24 and <= 256 &&
            _serverCertificateSha256.Length == 64 && File.Exists(certificatePath))
        {
            try
            {
                using var existing = X509CertificateLoader.LoadPkcs12FromFile(
                    certificatePath,
                    _hostCertificatePassword,
                    X509KeyStorageFlags.Exportable | X509KeyStorageFlags.DefaultKeySet);
                if (string.Equals(
                        InternetTransportSecurity.CertificateSha256(existing),
                        _serverCertificateSha256,
                        StringComparison.OrdinalIgnoreCase) &&
                    existing.NotAfter.ToUniversalTime() > DateTime.UtcNow.AddDays(1))
                    return;
            }
            catch (CryptographicException)
            {
                // Rotate invalid or unreadable local host material below.
            }
        }

        _networkJoinToken = Base64Url(RandomNumberGenerator.GetBytes(32));
        _hostCertificatePassword = Base64Url(RandomNumberGenerator.GetBytes(32));
        using var certificate = InternetTransportSecurity.CreateSelfSignedCertificate(
            $"{_verification.Manifest.Slug}-internet-host");
        _serverCertificateSha256 = InternetTransportSecurity.CertificateSha256(certificate);
        _hostCertificatePath = certificatePath;
        var content = certificate.Export(X509ContentType.Pfx, _hostCertificatePassword);
        var temporary = Path.Combine(
            settingsDirectory,
            $".{Path.GetFileName(certificatePath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllBytes(temporary, content);
            File.Move(temporary, certificatePath, overwrite: true);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(content);
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static string Base64Url(byte[] value)
    {
        try
        {
            return Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }
        finally
        {
            CryptographicOperations.ZeroMemory(value);
        }
    }

    private void SetPublicAddressText(string value)
    {
        _updatingPublicAddress = true;
        PublicJoinAddressText.Text = value;
        _updatingPublicAddress = false;
    }

    private void UpdatePublicAddressControls()
    {
        if (PublicJoinAddressText is null || CopyPublicAddressButton is null || RefreshPublicAddressButton is null)
            return;
        PublicJoinAddressText.IsReadOnly = _publicAddressLookupInProgress;
        CopyPublicAddressButton.IsEnabled = !_publicAddressLookupInProgress &&
                                            TryBuildPublicJoinEndpoint(out _);
        RefreshPublicAddressButton.IsEnabled = IsInternetHostMode() && !_publicAddressLookupInProgress;
    }

    private void UpdateMultiplayerNotice()
    {
        if (MultiplayerNoticeText is null) return;
        var direct = DirectConnectMode.IsChecked == true;
        var host = LocalHostMode.IsChecked == true;
        var internetEnabled = _verification.Manifest.Multiplayer == GameLauncherMultiplayerMode.DirectConnectPreview;
        if (direct)
        {
            MultiplayerNoticeText.Text =
                "Collez l’invitation openrpg2: de l’hôte. Elle épingle son certificat TLS, authentifie la session et contient un jeton privé à ne pas publier.";
            return;
        }

        if (host && internetEnabled)
        {
            if (_publicAddressLookupInProgress)
            {
                MultiplayerNoticeText.Text =
                    $"Recherche de votre IPv4 publique… Le jeu écoutera sur le port TCP {ServerPortText.Text}.";
            }
            else if (TryBuildPublicJoinEndpoint(out var endpoint))
            {
                MultiplayerNoticeText.Text =
                    $"Utilisez COPIER pour partager une invitation TLS privée vers {endpoint}. Autorisez ce port TCP dans le pare-feu Windows et redirigez-le dans le routeur.";
            }
            else if (_publicAddressLookupAttempted)
            {
                MultiplayerNoticeText.Text =
                    "Adresse publique non détectée. Saisissez votre IPv4 publique ou un nom DNS ; n’utilisez pas 127.0.0.1. Le port doit aussi être redirigé dans le routeur.";
            }
            else
            {
                MultiplayerNoticeText.Text =
                    $"Le lanceur va détecter l’IPv4 publique à partager. Le jeu écoutera sur le port TCP {ServerPortText.Text}.";
            }

            return;
        }

        MultiplayerNoticeText.Text = host
            ? "Cette session n’accepte que les joueurs de cet ordinateur. Activez le profil Internet du jeu pour recevoir des connexions distantes."
            : "Le mode solo ne démarre aucun serveur et n’ouvre aucun port réseau.";
    }

    private bool TryBuildPublicJoinEndpoint(out string endpoint)
    {
        endpoint = string.Empty;
        if (PublicJoinAddressText is null || ServerPortText is null) return false;
        var address = PublicJoinAddressText.Text.Trim();
        var addressIsShareable = IsShareableInternetAddress(address) ||
                                 (_captureMode && IPAddress.TryParse(address, out var captureAddress) &&
                                  captureAddress.AddressFamily == AddressFamily.InterNetwork);
        if (!addressIsShareable ||
            !int.TryParse(ServerPortText.Text.Trim(), out var port) ||
            port is < 1024 or > 65_535)
            return false;
        endpoint = $"{address}:{port}";
        return true;
    }

    private bool IsInternetHostMode() =>
        LocalHostMode.IsChecked == true &&
        _verification.Manifest.Multiplayer == GameLauncherMultiplayerMode.DirectConnectPreview;

    private static bool IsShareableInternetAddress(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 253 || value.Any(char.IsWhiteSpace))
            return false;
        if (IPAddress.TryParse(value, out var address))
            return PublicInternetAddressResolver.IsGloballyRoutableIpv4(address);
        return value.Contains('.', StringComparison.Ordinal) &&
               !value.Equals("localhost", StringComparison.OrdinalIgnoreCase) &&
               Uri.CheckHostName(value) == UriHostNameType.Dns;
    }

    private static string NormalizeCapturePublicAddress(string? value)
    {
        var candidate = string.IsNullOrWhiteSpace(value) ? "203.0.113.42" : value.Trim();
        if (candidate.Length > 253 || candidate.Any(char.IsWhiteSpace) ||
            (!IPAddress.TryParse(candidate, out _) && Uri.CheckHostName(candidate) != UriHostNameType.Dns))
            throw new ArgumentException("L’adresse de capture doit être une adresse IP ou un nom DNS.", nameof(value));
        return candidate;
    }

    private void SetStatus(string text, bool error)
    {
        StatusText.Text = text;
        StatusText.Foreground = (Brush)FindResource(error ? "DangerBrush" : "SuccessBrush");
        StatusLight.Fill = StatusText.Foreground;
    }

    private string MultiplayerTitle() => _verification.Manifest.Multiplayer switch
    {
        GameLauncherMultiplayerMode.Disabled => "Mode solo",
        GameLauncherMultiplayerMode.DirectConnectPreview => "Héberger ou rejoindre par Internet",
        _ => "Hébergement local autoritaire",
    };

    private void OnNewsClick(object sender, RoutedEventArgs eventArgs)
    {
        if (_verification.Manifest.NewsUrl is not null) OpenUrl(_verification.Manifest.NewsUrl);
    }

    private void OnSupportClick(object sender, RoutedEventArgs eventArgs)
    {
        if (_verification.Manifest.SupportUrl is not null) OpenUrl(_verification.Manifest.SupportUrl);
    }

    private static void OpenUrl(string url) =>
        _ = Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });

    private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs eventArgs)
    {
        if (eventArgs.ChangedButton != MouseButton.Left) return;
        if (eventArgs.ClickCount == 2)
        {
            ToggleMaximize();
            return;
        }
        DragMove();
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs eventArgs) => WindowState = WindowState.Minimized;

    private void OnMaximizeClick(object sender, RoutedEventArgs eventArgs) => ToggleMaximize();

    private void ToggleMaximize() =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void OnQuitClick(object sender, RoutedEventArgs eventArgs) => Close();

    private void ApplyAccent(string accentValue)
    {
        var accent = (Color)ColorConverter.ConvertFromString(accentValue);
        var bright = Mix(accent, Colors.White, 0.34);
        Resources["AccentBrush"] = FrozenBrush(accent);
        Resources["AccentBrightBrush"] = FrozenBrush(bright);
    }

    private static SolidColorBrush FrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static Color Mix(Color first, Color second, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        return Color.FromArgb(
            255,
            (byte)Math.Round(first.R + (second.R - first.R) * amount),
            (byte)Math.Round(first.G + (second.G - first.G) * amount),
            (byte)Math.Round(first.B + (second.B - first.B) * amount));
    }

    private static BitmapImage LoadBitmap(string path)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(Path.GetFullPath(path), UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private static void Animate(
        UIElement target,
        DependencyProperty property,
        double to,
        int durationMilliseconds,
        int beginMilliseconds = 0)
    {
        target.BeginAnimation(property, CreateAnimation(to, durationMilliseconds, beginMilliseconds));
    }

    private static void Animate(
        Animatable target,
        DependencyProperty property,
        double to,
        int durationMilliseconds,
        int beginMilliseconds = 0)
    {
        target.BeginAnimation(property, CreateAnimation(to, durationMilliseconds, beginMilliseconds));
    }

    private static DoubleAnimation CreateAnimation(double to, int durationMilliseconds, int beginMilliseconds)
    {
        return new DoubleAnimation(to, TimeSpan.FromMilliseconds(durationMilliseconds))
        {
            BeginTime = TimeSpan.FromMilliseconds(beginMilliseconds),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.HoldEnd,
        };
    }
}
