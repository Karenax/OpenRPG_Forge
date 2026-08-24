using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forge.Domain.Modules;
using Forge.ModuleFormat.Launcher;

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

    private readonly GameLauncherVerification _verification;
    private readonly string _settingsPath;
    private bool _initializing = true;
    private bool _captureMode;
    private bool _reduceMotion;
    private bool _atmosphereStarted;
    private string _activePage = "home";
    private double _parallaxTargetX;
    private double _parallaxTargetY;
    private double _parallaxX;
    private double _parallaxY;

    public LauncherWindow(
        GameLauncherVerification verification,
        GameLauncherUserSettings settings,
        string settingsPath)
    {
        _verification = verification ?? throw new ArgumentNullException(nameof(verification));
        ArgumentNullException.ThrowIfNull(settings);
        _settingsPath = Path.GetFullPath(settingsPath);
        InitializeComponent();

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
        MultiplayerNoticeText.Text = verification.Manifest.Multiplayer == GameLauncherMultiplayerMode.DirectConnectPreview
            ? "La connexion distante est préparée dans l’interface, mais reste verrouillée tant que le transport réseau inter-processus n’est pas branché."
            : "L’hébergement local crée une session autoritaire dans le runtime. Les connexions distantes ne sont pas exposées par ce profil.";
        NewsButton.Visibility = verification.Manifest.NewsUrl is null ? Visibility.Collapsed : Visibility.Visible;
        SupportButton.Visibility = verification.Manifest.SupportUrl is null ? Visibility.Collapsed : Visibility.Visible;

        ApplyAccent(verification.Manifest.AccentColor);
        if (verification.BackgroundImagePath is not null)
            BackdropImage.Source = LoadBitmap(verification.BackgroundImagePath);

        ResolutionSelector.ItemsSource = Resolutions.Select(item => item.Label).ToArray();
        WindowModeSelector.ItemsSource = new[]
        {
            "Fenêtré · barre de titre",
            "Plein écran · sans bordure",
            "Plein écran",
        };
        QualitySelector.ItemsSource = new[] { "Faible", "Moyenne", "Élevée", "Ultra" };
        LoadSettings(settings);
        ShowPageImmediately("home");
        _initializing = false;
        UpdateModeControls();
        UpdateWindowModeHint();
        StateChanged += (_, _) => MaximizeButton.Content = WindowState == WindowState.Maximized ? "❐" : "□";
    }

    public async Task CapturePreviewAsync(string path, string page, int width = 1440, int height = 860)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (width < 1180 || height < 720 || width > 3840 || height > 2160)
            throw new ArgumentOutOfRangeException(nameof(width), "La capture du lanceur doit rester entre 1180 × 720 et 3840 × 2160.");
        _captureMode = true;
        _reduceMotion = true;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = -32_000;
        Top = -32_000;
        Width = width;
        Height = height;
        ResizeMode = ResizeMode.NoResize;
        Show();
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

    private void OnLoaded(object sender, RoutedEventArgs eventArgs)
    {
        CompositionTarget.Rendering += OnCompositionFrame;
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
        CompositionTarget.Rendering -= OnCompositionFrame;
        StopAtmosphere(createStaticParticles: false);
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
            var ember = new System.Windows.Shapes.Ellipse
            {
                Width = size,
                Height = size,
                Opacity = 0.22 + random.NextDouble() * 0.55,
                Fill = new RadialGradientBrush(
                    Color.FromArgb(255, 255, 225, 168),
                    Color.FromArgb(8, 232, 97, 35)),
                IsHitTestVisible = false,
            };
            Canvas.SetLeft(ember, left);
            Canvas.SetTop(ember, top);
            ParticleCanvas.Children.Add(ember);
            if (!animate) continue;

            var speed = 18 + random.NextDouble() * 24;
            var travelSeconds = Math.Max(3.5, (top + 40) / speed);
            ember.BeginAnimation(
                Canvas.TopProperty,
                new DoubleAnimation(top, -35, TimeSpan.FromSeconds(travelSeconds))
                {
                    RepeatBehavior = RepeatBehavior.Forever,
                    BeginTime = TimeSpan.FromMilliseconds(random.Next(0, 2200)),
                });
            var drift = new TranslateTransform();
            ember.RenderTransform = drift;
            drift.BeginAnimation(
                TranslateTransform.XProperty,
                new DoubleAnimation(-10 - random.NextDouble() * 8, 12 + random.NextDouble() * 14,
                    TimeSpan.FromSeconds(2.6 + random.NextDouble() * 3.8))
                {
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
                });
            ember.BeginAnimation(
                UIElement.OpacityProperty,
                new DoubleAnimation(0.15, 0.82, TimeSpan.FromSeconds(0.9 + random.NextDouble() * 1.4))
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
        WindowModeSelector.SelectedIndex = (int)settings.WindowMode;
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
        UpdateVolumeLabels();
    }

    private GameLauncherUserSettings ReadSettings()
    {
        var resolutionIndex = Math.Clamp(ResolutionSelector.SelectedIndex, 0, Resolutions.Length - 1);
        var selectedResolution = Resolutions[resolutionIndex];
        if (!int.TryParse(ServerPortText.Text.Trim(), out var serverPort))
            throw new InvalidDataException("Le port serveur doit être un nombre entre 1 et 65535.");
        var settings = new GameLauncherUserSettings(
            GameLauncherUserSettings.CurrentSchemaVersion,
            selectedResolution.Width,
            selectedResolution.Height,
            (LauncherWindowMode)Math.Max(0, WindowModeSelector.SelectedIndex),
            (LauncherGraphicsQuality)Math.Max(0, QualitySelector.SelectedIndex),
            VSyncCheck.IsChecked == true,
            (int)Math.Round(MasterVolumeSlider.Value),
            (int)Math.Round(MusicVolumeSlider.Value),
            (int)Math.Round(EffectsVolumeSlider.Value),
            ReduceMotionCheck.IsChecked == true,
            LocalHostMode.IsChecked == true
                ? LauncherSessionMode.LocalHost
                : DirectConnectMode.IsChecked == true
                    ? LauncherSessionMode.DirectConnectPreview
                    : LauncherSessionMode.Solo,
            PlayerNameText.Text.Trim(),
            ServerAddressText.Text.Trim(),
            serverPort);
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
        catch (NotSupportedException exception)
        {
            SetStatus(exception.Message, error: true);
            MessageBox.Show(exception.Message, "Connexion distante en préparation", MessageBoxButton.OK,
                MessageBoxImage.Information);
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
        ServerAddressText.IsEnabled = direct;
        ServerPortText.IsEnabled = direct || LocalHostMode.IsChecked == true;
        PlayButton.IsEnabled = !direct;
        PlayButton.Content = LocalHostMode.IsChecked == true
            ? "HÉBERGER ET JOUER"
            : direct
                ? "CONNEXION À VENIR"
                : "ENTRER DANS L’AVENTURE";
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
            LauncherSessionMode.LocalHost => "Hôte local",
            LauncherSessionMode.DirectConnectPreview => "Connexion distante en préparation",
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
            1 => "Occupe tout l’écran sans barre de titre. Ce mode ne se déplace pas.",
            2 => "Plein écran dédié. Utilisez Alt+Tab pour revenir au bureau.",
            _ => "Fenêtre classique avec barre de titre, déplaçable et redimensionnable. La taille reste dans la zone visible.",
        };
    }

    private void OnVolumeChanged(object sender, RoutedPropertyChangedEventArgs<double> eventArgs)
    {
        if (MasterVolumeValue is null || MusicVolumeValue is null || EffectsVolumeValue is null) return;
        UpdateVolumeLabels();
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
        ServerPortText.Text = Math.Clamp(current + delta, 1, 65_535).ToString();
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
        GameLauncherMultiplayerMode.DirectConnectPreview => "Hôte local et connexion préparée",
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
