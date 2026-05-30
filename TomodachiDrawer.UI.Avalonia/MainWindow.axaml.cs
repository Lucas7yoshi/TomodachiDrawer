using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using Microsoft.Win32;
using SkiaSharp;
using TomodachiDrawer.Core;
using TomodachiDrawer.Core.Extensions;
using TomodachiDrawer.Core.ImageProcessing;
using TomodachiDrawer.Core.ImageProcessing.Denoising;
using TomodachiDrawer.Core.ImageProcessing.Quantizers;
using TomodachiDrawer.Core.Models;
using TomodachiDrawer.Core.OutputSinks;
using Button = Avalonia.Controls.Button; // conflict with the Button enum in SinkEnums
#if DEBUG
using TomodachiDrawer.DebugTools;
#endif

namespace TomodachiDrawer.UI.Avalonia;

public partial class MainWindow : Window
{
    private static string GetRPFirmwareFileName(RPChipType chip) =>
        chip == RPChipType.RP2350
            ? "TomodachiDrawer.Firmware.rp2350.uf2"
            : "TomodachiDrawer.Firmware.rp2040.uf2";

    private string _currentImagePath = string.Empty;
    private SKBitmap? _currentImage;
    private readonly CancellationTokenSource _cts = new();
    private readonly TelemetryService _telemetry;

    private bool BusyExporting = false;

    //private SwitchVersion _selectedSwitchVersion = SwitchVersion.None;
    //private int _selectedThemeIndex = 0; // 0 is System.
    private AppSettings _currentSettings = new(); // All cases will result in it being non-null but IntelliSense cant see that far
    private bool _loadingSettings = true;

#if DEBUG
    private readonly VirtualGamepad _debugVirtualGamepad = new();

    private MenuItem? MenuDebugConnectVirtualGamepad;
    private MenuItem? MenuDebugRunInVirtualGamepad;
    private MenuItem? MenuDebugOpenVirtualGamepadController;
#endif

    public MainWindow()
    {
        InitializeComponent();

        SetControlValueWithoutSaving(() =>
        {
            var quantizers = ColourPalette.Quantizers.Keys.ToList();
            quantizers.Insert(0, "Arbitrary");
            ColourMatcherComboBox.ItemsSource = quantizers;
            ColourMatcherComboBox.SelectedIndex = 0;

            var denoiserSelection = new List<string> { "None" };
            denoiserSelection.AddRange(ImageDenoiser.Denoisers.Keys);

            DenoisingComboBox.ItemsSource = denoiserSelection;
            DenoisingComboBox.SelectedIndex = 0;
            DenoisingComboBox.SelectionChanged += DenoisingComboBox_SelectionChanged;
        });

        InitializeTemplates();

        GetSettings();

        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);

#if DEBUG
        this.Title = $"TomodachiDrawer.UI.Avalonia - {GetVersionString(true)}";
#else
        this.Title = $"TomodachiDrawer - {GetVersionString(false)}";
#endif

        if (CheckForUpdatesCheckBox.IsChecked)
            _ = PerformAsyncUpdateCheck();

        _telemetry = new TelemetryService();

        Opened += MainWindow_Opened;
    }

    private static bool IsVCRuntimeInstalled()
    {
        if (!OperatingSystem.IsWindows())
        {
            return true;
        }

        string keyPath = @"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\X64";

        using var key = Registry.LocalMachine.OpenSubKey(keyPath);
        if (key != null)
        {
            var version = key.GetValue("Version")?.ToString();
            return !string.IsNullOrEmpty(version);
        }

        return false;
    }

    private void InitializeTemplates()
    {
        foreach (var mask in Enum.GetValues<TomodachiLifeMask>().Cast<TomodachiLifeMask>())
        {
            var desc = mask.GetDescription();
            var menuItem = new MenuItem() { Header = desc };
            menuItem.Click += (s, e) => OpenTemplate(mask);
            MenuTemplates.Items.Add(menuItem);
        }
    }

    private async void OpenTemplate(TomodachiLifeMask mask)
    {
        var templateWindow = new TemplateTool(mask);
        var templateOutput = await templateWindow.ShowDialog<TemplateToolResponse?>(this);
        if (templateOutput != null)
        {
            if (templateOutput.Success && templateOutput.Result != null)
            {
                await LoadImageFromBitmapAsync(templateOutput.Result, $"template_{mask}.png");
                AppendLog($"Loaded masked image for template {mask.GetDescription()} from editor.");
            }
            else if (templateOutput.CouldNotLoad)
            {
                AppendLog(
                    $"Template editor failed to load the template for {mask.GetDescription()}"
                );
                _ = ShowMessageAsync(
                    "Error loading template",
                    "The template tool could not find the image. This REALLY shouldn't happen... Try reinstalling?"
                );
            }
            else
            {
                AppendLog($"Template editor closed with no input. Nothing changed.");
            }
        }
        else
        {
            AppendLog($"The template editor closed unexpectedly...");
        }
    }

    private async void MainWindow_Opened(object? sender, EventArgs e)
    {
        if (_currentSettings.FirstStartId != CURRENT_WELCOME_ID)
        {
            await ShowWelcomeMessage();
            _currentSettings.FirstStartId = CURRENT_WELCOME_ID;
        }

#if DEBUG
        InsertDebugMenuItems();
#endif

        if (_currentSettings.EnableTelemetry == null)
        {
            // User hasnt agreed/disagreed.
            var accepted = await new TelemetryPrompt().ShowDialog<bool>(this);
            _currentSettings.EnableTelemetry = accepted;
        }

        SaveSettings();

        if (!IsVCRuntimeInstalled())
        {
            await ShowMessageAsync(
                "WARNING: MISSING LIBRARIES",
                $"In order for this program to run, you MUST install the VC Redistributable."
                    + $"\n\nClick the open link button to install it. "
                    + $"If you do not install it, this program will probably crash silently.",
                new Uri("https://aka.ms/vc14/vc_redist.x64.exe"),
                "Download Redistributable"
            );
        }

        if (_currentSettings.EnableTelemetry == true)
        {
            _telemetry.TelemetryEnabled = true;
            // Turn on crash reporting if not already initialized.
            // We explicitly do NOT start it on first-launch before the user has had a chance to consent.
            // This does come with the downside that we would probably not be able to receive right-at-start errors, but those
            // haven't really been an issue and can deal with those the ol fashioned way.
            CrashReporter.Init();
            // Discard to avoid blocking.
            _ = _telemetry.ReportStart();
        }

        StartPicoPolling();
    }

    // Welcome message stuff. For important changes, the ID is incremented by one by hand whenever something notable changes.
    // This is only really needed for Mac since its settings are saved in a way that persists more readily.
    private const int CURRENT_WELCOME_ID = 3;

    private async Task ShowWelcomeMessage()
    {
        await ShowMessageAsync(
            "Welcome to TomodachiDrawer!",
            "0.6.0 has added support for RP2350 based boards (RP2350-Zero, Raspberry Pi Pico 2, etc) on top of the RP2040 support."
                + "\n\n0.5.0 added a tool for helping you with more complex, non square templates."
                + "\nAt the top menu bar, select \"Templates\" and choose the item type you want, it will open an editor with a preview of the layout, and copy it to your clipboard for you to easily edit in other image editing software."
        );
    }

    private static string GetVersionString(bool includeCommit)
    {
        var currentVersion =
            Assembly
                .GetEntryAssembly()
                ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion
            ?? "dev";
        if (currentVersion.StartsWith("0.0.0"))
        {
            if (includeCommit)
            {
                return "dev+" + currentVersion.Split('+').Last();
            }
            else
            {
                return "dev";
            }
        }
        if (!includeCommit)
        {
            return currentVersion.Split('+').First();
        }
        return currentVersion;
    }

#if DEBUG
    private void InsertDebugMenuItems()
    {
        var debugMenuItem = new MenuItem() { Header = "_Debug" };
        Menu.Items.Add(debugMenuItem);

        MenuDebugConnectVirtualGamepad = new MenuItem() { Header = "_Connect Virtual Gamepad" };
        MenuDebugConnectVirtualGamepad.Click += MenuDebugConnectVirtualGamepad_Click;
        debugMenuItem.Items.Add(MenuDebugConnectVirtualGamepad);

        MenuDebugRunInVirtualGamepad = new MenuItem()
        {
            Header = "_Run in Virtual Gamepad",
            IsEnabled = false,
        };
        MenuDebugRunInVirtualGamepad.Click += MenuDebugRunInVirtualGamepad_Click;
        debugMenuItem.Items.Add(MenuDebugRunInVirtualGamepad);

        MenuDebugOpenVirtualGamepadController = new MenuItem()
        {
            Header = "_Control Virtual Gamepad",
            IsEnabled = false,
        };
        MenuDebugOpenVirtualGamepadController.Click += MenuDebugOpenVirtualGamepadController_Click;
        debugMenuItem.Items.Add(MenuDebugOpenVirtualGamepadController);
    }
#endif

    private async Task PerformAsyncUpdateCheck()
    {
        try
        {
            var ourVersion = GetVersionString(false);
            if (ourVersion == "dev")
            {
                AppendLog("Skipping update check for dev.");
                return;
            }
            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd($"TomodachiDrawer {ourVersion}");

            using var response = await http.GetAsync(
                "https://api.github.com/repos/Lucas7yoshi/TomodachiDrawer/releases/latest"
            );
            response.EnsureSuccessStatusCode();
            using var responseStream = await response.Content.ReadAsStreamAsync();

            using var responseJsonObject = JsonDocument.Parse(responseStream);

            // 0.0.0 format, no v, no -.
            var releaseVersionTag =
                responseJsonObject.RootElement.GetProperty("tag_name").GetString() ?? "0.0.0";

            // see if its newer. TODO: Actually check that, only really effects using the artifacts from the release build before
            // i've published the release though.
            if (releaseVersionTag != null)
            {
                if (releaseVersionTag != ourVersion)
                {
                    _ = ShowMessageAsync(
                        "Update available",
                        "A new update is available on GitHub."
                            + $"\nCurrent Version: {ourVersion}"
                            + $"\nLatest Version: {releaseVersionTag}"
                            + $"\nVersion title: {responseJsonObject.RootElement.GetProperty("name").GetString() ?? "N/A"}"
                            + $"\n\nDownload at:\nhttps://github.com/Lucas7yoshi/TomodachiDrawer",
                        new Uri("https://github.com/Lucas7yoshi/TomodachiDrawer/releases"),
                        "Open Releases"
                    );
                }
                else
                {
                    AppendLog($"Up to date! {ourVersion}");
                }
            }
        }
        catch (Exception ex)
        {
            AppendLog($"Failed to check for updates: {ex.Message}");
        }
    }

    protected override void OnClosed(System.EventArgs e)
    {
        _cts.Cancel();
        base.OnClosed(e);
    }

    // Check if we can access a RP2040 or RP2350 drive.
    // Also triggers the permission prompt on macOS if permissions haven't been granted yet.
    private bool CanAccessPicoDrive(string drivePath)
    {
        try
        {
            // Try to access the drive by listing its files.
            // This also triggers the permission prompt on macOS.
            _ = Directory.GetFiles(drivePath);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            // macOS: User (probably) clicked "Don't Allow".
            if (OperatingSystem.IsMacOS())
            {
                _ = ShowMessageAsync(
                    "Permission Denied",
                    $"Permission to access the microcontrollers drive ({drivePath}) was denied.\n\n"
                        + "Please open System Settings -> Privacy & Security -> Files & Folders, find \"TomodachiDrawer\", and make sure \"Removable Volumes\" is enabled.\n\n"
                        + "This is required for the app to write the firmware directly to your Pico drive.\r"
                        + $"Or you can manually copy the .uf2 file to {drivePath} if you want to avoid granting permissions.",
                    new Uri(
                        "x-apple.systempreferences:com.apple.preference.security?Privacy_FilesAndFolders"
                    ),
                    "Open System Settings"
                );
            }
            AppendLog($"Permission to access microcontrollers drive ({drivePath}) was denied");
            return false;
        }
        catch (Exception ex)
        {
            AppendLog($"Could not access the microcontrollers drive ({drivePath}): {ex.Message}");
            return false;
        }
    }

    // ── RP2040/RP2350 polling ─────────────────────────────────────────────────

    private void StartPicoPolling()
    {
        _ = Task.Run(async () =>
        {
            bool lastRp2040 = false,
                lastRp2350 = false;
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    var rp2040Path = UF2Flasher.FindRP2040Drive();
                    var rp2350Path = UF2Flasher.FindRP2350Drive();

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        bool hasImage = _currentImage != null;
                        lastRp2040 = UpdateChipUI(
                            RPChipType.RP2040,
                            rp2040Path,
                            hasImage,
                            lastRp2040
                        );
                        lastRp2350 = UpdateChipUI(
                            RPChipType.RP2350,
                            rp2350Path,
                            hasImage,
                            lastRp2350
                        );
                    });
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch { }

                try
                {
                    await Task.Delay(1000, _cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        });
    }

    private bool UpdateChipUI(RPChipType chip, string? path, bool hasImage, bool wasSeen)
    {
        bool found = path != null;
        string chipName = chip == RPChipType.RP2350 ? "RP2350" : "RP2040";

        TextBlock statusLabel;
        Button flashButton,
            exportButton,
            exportUF2Button;
        TabItem tab;

        if (chip == RPChipType.RP2350) // very high tech way to avoid repeating code lol
        {
            statusLabel = RP2350StatusLabel;
            flashButton = RP2350FlashButton;
            exportButton = RP2350ExportButton;
            exportUF2Button = RP2350ExportUF2Button;
            tab = RP2350Tab;
        }
        else
        {
            statusLabel = RP2040StatusLabel;
            flashButton = RP2040FlashButton;
            exportButton = RP2040ExportButton;
            exportUF2Button = RP2040ExportUF2Button;
            tab = RP2040Tab;
        }

        exportUF2Button.IsEnabled = hasImage && !BusyExporting;

        if (found)
        {
            statusLabel.Text = $"{chipName} found: {path}";
            statusLabel.Foreground = Brushes.Green;
            flashButton.IsEnabled = !BusyExporting;
            exportButton.IsEnabled = hasImage && !BusyExporting;

            if (!wasSeen)
            {
                AppendLog($"{chipName} connected @ {path}");
                tab.Header = $">{chipName}<";
                ChipTabControl.SelectedItem = tab;
            }
        }
        else
        {
            statusLabel.Text = $"{chipName}: Not Found";
            statusLabel.Foreground = Brushes.Red;
            flashButton.IsEnabled = false;
            exportButton.IsEnabled = false;

            if (wasSeen)
            {
                AppendLog($"{chipName} disconnected...");
                tab.Header = chipName;
            }
        }

        return found;
    }

    #region Image/Preview
    private async Task LoadImageAsync(string path)
    {
        if (!File.Exists(path))
        {
            AppendLog($"File does not exist..? {path}");
            return;
        }

        var img = SKBitmap.Decode(path);
        if (img == null)
        {
            AppendLog($"Failed to decode image: {path}");
            return;
        }

        if (img.Width > 256 || img.Height > 256)
        {
            float scale = Math.Min(256f / img.Width, 256f / img.Height);
            int newWidth = (int)(img.Width * scale);
            int newHeight = (int)(img.Height * scale);

            var resized = img.Resize(
                new SKImageInfo(newWidth, newHeight),
                new SKSamplingOptions(SKCubicResampler.CatmullRom)
            );
            img = resized;
            AppendLog($"Image resized to {newWidth}x{newHeight}");
        }

        await LoadImageFromBitmapAsync(img, Path.GetFileName(path));
    }

    /// <summary>
    /// Stores <paramref name="img"/> as the active image and refreshes all dependent UI.
    /// Takes ownership of <paramref name="img"/> — do not dispose it after calling this.
    /// </summary>
    private async Task LoadImageFromBitmapAsync(SKBitmap img, string displayName)
    {
        _currentImage?.Dispose();
        _currentImage = img;
        _currentImagePath = displayName; // kept for log messages / ImagePathBox

        ImagePathBox.Text = displayName;
        RP2040ExportUF2Button.IsEnabled = true;
        RP2350ExportUF2Button.IsEnabled = true;

        if (img.Width == 256 && img.Height == 256)
        {
            AppendLog(
                "Image is full canvas size, so enabling auto home by default.\nYou can disable it if it causes you trouble and manually home before connecting."
            );
            EnableHomeCanvas.IsChecked = true;
        }

        await UpdatePreviewAsync();
        TSPTimeLimitUpDown.Value = (decimal)
            CanvasDrawer.GetRecommendedTSPSolveTime(img.Width, img.Height);
        AppendLog($"Loaded image: {displayName} ({img.Width}x{img.Height})");
    }

    private static SKBitmap GetPreview(
        SKBitmap source,
        QuantizerSettings quantizerSettings,
        string? denoiser
    )
    {
        var pal = new ColourPalette(new DummySink());
        return pal.PreviewColourMapping(source, quantizerSettings, denoiser);
    }

    private async Task UpdatePreviewAsync()
    {
        if (_currentImage == null)
        {
            AppendLog($"No image loaded, cannot update preview.");
            return;
        }

        var quantizerSettings = GetQuantizerSettings();
        var denoiser = DenoisingComboBox.SelectedItem?.ToString();
        var source = _currentImage;

        var preview = await Task.Run(() => GetPreview(source, quantizerSettings, denoiser))
            .ConfigureAwait(true);

        PreviewImage.Source = ToAvaloniaBitmap(preview);
        // update the preview label to indicate the size of the image just for user reference
        PreviewHeader.Text = $"Preview ({_currentImage.Width}x{_currentImage.Height})";
        AppendLog(
            $"Updated preview for {_currentImagePath} using {quantizerSettings.QuantizerName}"
        );
    }

    public static Bitmap ToAvaloniaBitmap(SKBitmap skBitmap)
    {
        using var image = SKImage.FromBitmap(skBitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = new MemoryStream(data.ToArray());
        return new Bitmap(stream);
    }
    #endregion

    private void AppendLog(string msg)
    {
        Dispatcher.UIThread.Post(() =>
        {
            LogBox.Text = (LogBox.Text ?? "") + msg + "\n";
            LogBox.CaretIndex = LogBox.Text?.Length ?? 0;
        });
    }

    // messagebox replacement
    private async Task ShowMessageAsync(
        string title,
        string message,
        Uri? link = null,
        string? linkButtonText = null
    )
    {
        var buttonRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
        };

        var okButton = new Button
        {
            Content = "OK",
            Margin = new Thickness(0, 10, 0, 0),
            MinWidth = 80,
        };

        var stack = new StackPanel() { Margin = new Thickness(16) };
        buttonRow.Children.Add(okButton);

        Button? linkButton = null;

        if (link != null)
        {
            linkButton = new Button
            {
                Content = linkButtonText ?? "Open Link",
                Margin = new Thickness(0, 10, 0, 0),
                MinWidth = 80,
            };
            buttonRow.Children.Add(linkButton);
        }

        stack.Children.Insert(
            0,
            new SelectableTextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 400,
            }
        );
        stack.Children.Add(buttonRow);

        var dialog = new Window
        {
            Title = title,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Width = 440,
            SizeToContent = SizeToContent.Height,
            Content = stack,
        };

        okButton.Click += (_, _) => dialog.Close();
        linkButton?.Click += (_, _) =>
        {
            // Link button is only non-null if link is non-null so ! to indicate its safe.
            Launcher.LaunchUriAsync(link!);
        };
        await dialog.ShowDialog(this);
    }

    private async void OpenImageButton_Click(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Open Image",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("Images")
                    {
                        Patterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif", "*.webp"],
                    },
                    new FilePickerFileType("All Files") { Patterns = ["*.*"] },
                ],
            }
        );

        if (files.Count > 0)
            await LoadImageAsync(files[0].TryGetLocalPath() ?? "");
    }

    private async void ColourMatcherComboBox_SelectionChanged(
        object? sender,
        SelectionChangedEventArgs e
    )
    {
        if (_currentImage != null)
            await UpdatePreviewAsync();
        ColourLimitUpDown.IsEnabled =
            ColourMatcherComboBox?.SelectedValue?.ToString() == "Arbitrary";

        if (
            !_loadingSettings
            && ColourMatcherComboBox?.SelectedItem?.ToString() is { } selectedColourMatcher
        )
        {
            _currentSettings.SelectedColourMatcher = selectedColourMatcher;
            SaveSettings();
        }
    }

    private async void DenoisingComboBox_SelectionChanged(
        object? sender,
        SelectionChangedEventArgs e
    )
    {
        if (_currentImage != null)
            await UpdatePreviewAsync();

        if (
            !_loadingSettings && DenoisingComboBox?.SelectedItem?.ToString() is { } selectedDenoiser
        )
        {
            _currentSettings.SelectedDenoiser = selectedDenoiser;
            SaveSettings();
        }
    }

    private void TSPHelpButton_Click(object? sender, RoutedEventArgs e)
    {
        const string message =
            "TSP Solver Time Limit refers to how much time is alloted to the TSP solver.\n"
            + "TSP refers to the Travelling Sales Person problem, which is finding the optimal route among a set of points.\n"
            + "This is used to find the optimal path for the pen tool to take while drawing to minimize drawing time.\n\n"
            + "For larger images, the TSP solver can take longer to find an optimal route, its also possible it will never even find an optimal route if there is too many points.\n"
            + "For 64x64, 0.5s is generally fine, anything largest you should consider giving it more time.\n\n"
            + "This time is how long it is alloted PER colour, so if an image has 30 different colours used, 0.5s will take 15 seconds.\n"
            + "The TSP solve is not used always, a simpler \"snaking\" algorithm is used if its quicker, or if TSP didnt find anything in time, which it sometimes is, mostly for large continuous areas of colour.";

        _ = ShowMessageAsync("TSP Solver Time Limit", message);
    }

    private QuantizerSettings GetQuantizerSettings()
    {
        string quantizerName = ColourMatcherComboBox.SelectedItem!.ToString()!;
        if (quantizerName == "Arbitrary")
        {
            var colourCount = (int)(ColourLimitUpDown.Value ?? 32);
            return new QuantizerSettings(quantizerName, colourCount, default);
        }
        return new QuantizerSettings(quantizerName, default, default);
    }

    // Common Click method for Export to [device] buttons.
    // Diverges based on sender.
    // to avoid repeated code.
    private async void ExportToDeviceButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_currentImage == null)
            return;

        if (_currentSettings.SelectedSwitchVersion == SwitchVersion.None)
        {
            _ = ShowMessageAsync(
                "Select Switch Version",
                "For compatibility, you must select a switch version in the dropdown."
                    + "\n\nSwitch 1 is more prone to desyncs, so this avoids certain things that are particularly prone to desyncing."
                    + "\nPlease be aware that even with Switch 1 selected, desyncs are unfortunately expected due to inconsistent and unpredictable lag in the drawing UI."
            );
            return;
        }

        var chip = sender == RP2350ExportButton ? RPChipType.RP2350 : RPChipType.RP2040;
        var exportButton = chip == RPChipType.RP2350 ? RP2350ExportButton : RP2040ExportButton;
        string chipName = chip == RPChipType.RP2350 ? "RP2350" : "RP2040";

        var colourCount = CountDistinctColours(_currentImage);
        var imageWidth = _currentImage.Width;
        var imageHeight = _currentImage.Height;
        var imageSnapshot = _currentImage!.Copy();
        var drawSettings = GetDrawImageSettings();
        string quantizerName = ColourMatcherComboBox.SelectedItem!.ToString()!;
        int? colourLimit =
            quantizerName == "Arbitrary" ? (int)(ColourLimitUpDown.Value ?? 32) : (int?)null;

        BusyExporting = true;
        exportButton.IsEnabled = false;

        try
        {
            var (uf2Bytes, totalTime) = await GenerateUF2Async(
                chip,
                imageSnapshot,
                drawSettings,
                $"Exporting to {chipName} flash"
            );

            if (uf2Bytes != null && uf2Bytes.Length > 0)
            {
                var drivePath = UF2Flasher.FindDriveForChip(chip);
                if (drivePath != null && CanAccessPicoDrive(drivePath))
                {
                    File.WriteAllBytes(Path.Combine(drivePath, "tdld_image.uf2"), uf2Bytes);
                    AppendLog(
                        $"Wrote to {chipName} flash. Unplug it and plug it into the switch without holding any button."
                    );
                }
            }

            _ = _telemetry.ReportImage(
                new ImageEventDto(
                    imageWidth,
                    imageHeight,
                    colourCount,
                    quantizerName,
                    colourLimit,
                    _currentSettings.SelectedSwitchVersion.ToString(),
                    drawSettings.EnableExperimentalFeatures,
                    totalTime.TotalSeconds,
                    drawSettings.TSPTimeLimit,
                    GetVersionString(true),
                    chipName
                )
            );

            SetEstimate(totalTime);
        }
        finally
        {
            BusyExporting = false;
            exportButton.IsEnabled = true;
        }
    }

    private void SetEstimate(TimeSpan time)
    {
        var estimateStr = $"{time:h\\hm\\ms\\s}";
        DrawTimeLabel.Text = $"Draw Time Estimate: {estimateStr}";
    }

    private async Task<(byte[]? uf2Bytes, TimeSpan totalTime)> GenerateUF2Async(
        RPChipType chip,
        SKBitmap imageSnapshot,
        DrawImageSettings drawSettings,
        string logPrefix
    )
    {
        byte[]? uf2Bytes = null;
        TimeSpan totalTime = TimeSpan.MaxValue;

        await Task.Run(async () =>
        {
            using var img = imageSnapshot;
            string tempPath = Path.Combine(
                Path.GetTempPath(),
                $"rp2040output{System.Random.Shared.Next(1000000, 9999999)}.tdld"
            );

            AppendLog($"{logPrefix} ({Path.GetFileName(tempPath)})");
            var timingSink = new TimingSink();
            var drawer = new CanvasDrawer(
                timingSink,
                _currentSettings.SelectedSwitchVersion,
                AppendLog
            );
            drawer.ConnectAndConfirmController();
            AppendLog("Starting to generate inputs...");
            await drawer.DrawImage(img, drawSettings);
            AppendLog($"True complete overall time is: {timingSink.TotalTime.TotalSeconds}s");

            var fileSink = new FileControllerSink(tempPath);
            timingSink.ReplayTo(fileSink);
            fileSink.Dispose();

            var tdldBytes = File.ReadAllBytes(tempPath);
            uf2Bytes = UF2Flasher.BuildTDLDUF2(tdldBytes, chip);

#if !DEBUG
            if (File.Exists(tempPath))
                File.Delete(tempPath);
#endif
            totalTime = timingSink.TotalTime;
        });

        return (uf2Bytes, totalTime);
    }

    private DrawImageSettings GetDrawImageSettings()
    {
        var denoiser = DenoisingComboBox.SelectedItem?.ToString();
        var tspLimit = (float)(TSPTimeLimitUpDown.Value ?? 0.5m);
        var quantizerSettings = GetQuantizerSettings();
        var enableExperimental = EnableExperimentalMenuItem.IsChecked;
        var enableHome = EnableHomeCanvas.IsChecked ?? false;
        var reverseColourOrder = ReverseColourOrder.IsChecked;

        return new()
        {
            QuantizerSettings = quantizerSettings,
            DenoiserName = denoiser,
            TSPTimeLimit = tspLimit,
            DisableLargeBrush = false,
            EnableExperimentalFeatures = enableExperimental,
            HomeToTopLeft = enableHome,
            ReverseColourOrder = reverseColourOrder,
        };
    }

    private static int CountDistinctColours(SKBitmap img)
    {
        var pixels = new HashSet<SKColor>();
        for (int y = 0; y < img.Height; y++)
        for (int x = 0; x < img.Width; x++)
            pixels.Add(img.GetPixel(x, y));
        return pixels.Count;
    }

    private async void ExportUF2Button_Click(object sender, RoutedEventArgs e)
    {
        if (_currentImage == null)
            return;

        if (_currentSettings.SelectedSwitchVersion == SwitchVersion.None)
        {
            _ = ShowMessageAsync(
                "Select Switch Version",
                "For compatibility, you must select a switch version in the dropdown."
                    + "\n\nSwitch 1 is more prone to desyncs, so this avoids certain things that are particularly prone to desyncing."
                    + "\nPlease be aware that even with Switch 1 selected, desyncs are unfortunately expected due to inconsistent and unpredictable lag in the drawing UI."
            );
            return;
        }

        var chip = sender == RP2350ExportUF2Button ? RPChipType.RP2350 : RPChipType.RP2040;
        var exportUF2Button =
            chip == RPChipType.RP2350 ? RP2350ExportUF2Button : RP2040ExportUF2Button;
        var chipName = chip == RPChipType.RP2350 ? "RP2350" : "RP2040";

        var file = await StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = "Save .UF2",
                DefaultExtension = "uf2",
                FileTypeChoices =
                [
                    new FilePickerFileType("UF2 Firmware Image") { Patterns = ["*.uf2"] },
                    new FilePickerFileType("All Files") { Patterns = ["*.*"] },
                ],
            }
        );

        var outputPath = file?.TryGetLocalPath();
        if (outputPath == null)
            return;

        var colourCount = CountDistinctColours(_currentImage);
        var imageWidth = _currentImage.Width;
        var imageHeight = _currentImage.Height;
        var imageSnapshot = _currentImage!.Copy();
        var drawSettings = GetDrawImageSettings();
        string quantizerName = ColourMatcherComboBox.SelectedItem!.ToString()!;
        int? colourLimit =
            quantizerName == "Arbitrary" ? (int)(ColourLimitUpDown.Value ?? 32) : (int?)null;

        exportUF2Button.IsEnabled = false;
        BusyExporting = true;

        try
        {
            var (uf2Bytes, totalTime) = await GenerateUF2Async(
                chip,
                imageSnapshot,
                drawSettings,
                "Exporting to UF2"
            );

            if (uf2Bytes != null && uf2Bytes.Length > 0)
            {
                File.WriteAllBytes(outputPath, uf2Bytes);
                AppendLog($"Saved UF2 to {outputPath}");
            }

            _ = _telemetry.ReportImage(
                new ImageEventDto(
                    imageWidth,
                    imageHeight,
                    colourCount,
                    quantizerName,
                    colourLimit,
                    _currentSettings.SelectedSwitchVersion.ToString(),
                    drawSettings.EnableExperimentalFeatures,
                    totalTime.TotalSeconds,
                    drawSettings.TSPTimeLimit,
                    GetVersionString(true),
                    chipName
                )
            );

            SetEstimate(totalTime);
        }
        finally
        {
            exportUF2Button.IsEnabled = true;
            BusyExporting = false;
        }
    }

    private static string GetBaseFirmwareFilePath(RPChipType chip)
    {
        var fileName = GetRPFirmwareFileName(chip);
        if (OperatingSystem.IsMacOS() && AppContext.BaseDirectory.Contains(".app/Contents/MacOS"))
            return Path.Combine(AppContext.BaseDirectory, fileName);
        return fileName;
    }

    private void FlashFirmwareButton_Click(object? sender, RoutedEventArgs e)
    {
        var chip = sender == RP2350FlashButton ? RPChipType.RP2350 : RPChipType.RP2040;
        string chipName = chip == RPChipType.RP2350 ? "RP2350" : "RPI-RP2";
        var firmwareFileName = GetRPFirmwareFileName(chip);
        var firmwareFilePath = GetBaseFirmwareFilePath(chip);
        var drivePath = UF2Flasher.FindDriveForChip(chip);

        if (!File.Exists(firmwareFilePath))
        {
            _ = ShowMessageAsync(
                "Error flashing base firmware",
                $"Could not locate {firmwareFileName}."
                    + "\nPlease ensure that you extracted all files from the zip before running."
                    + $"\nAlternatively, you can manually drag {firmwareFileName} to the {chipName} drive."
            );
            return;
        }
        if (drivePath == null)
        {
            _ = ShowMessageAsync(
                "Error",
                $"{chipName} not detected. Connect it in BOOT mode first."
            );
            return;
        }
        if (!CanAccessPicoDrive(drivePath))
            return;

        File.Copy(firmwareFilePath, Path.Combine(drivePath, firmwareFileName), overwrite: true);

        var timeout = DateTime.Now.AddSeconds(10);
        while (UF2Flasher.FindDriveForChip(chip) != null)
        {
            if (DateTime.Now > timeout)
            {
                _ = ShowMessageAsync(
                    "Error flashing base firmware",
                    "Wrote file but expected it to reset itself by now, maybe try doing it manually..?"
                );
                return;
            }
            Thread.Sleep(500);
        }

        _ = ShowMessageAsync(
            "",
            "Base firmware flashed! You can now use the standard output button to output your images to it!\nIf this is your first time, its likely flashing red. Simply hold BOOT and plug it back in, or hold BOOT and press reset if you have it."
        );
        AppendLog($"Flashed base firmware to {chipName}");
    }

    private void OutputExplanationButton_Click(object? sender, RoutedEventArgs e)
    {
        _ = ShowMessageAsync(
            "",
            "Your RP2040-Zero (or similar) needs two things in its memory (it's flash):\r\n"
                + "- The code that reads the instructions to draw your image and pipe it to the switch\r\n"
                + "- The instructions to draw your image.\r\n\r\n\r\n"
                + "To connect your device for flashing, hold down the \"BOOT\" button and plug it in, or hold \"BOOT\" and press \"RESET\" while it is connected.\r\n\r\n"
                + "You only need to flash the code/\"firmware\" once.\r\n\r\n"
                + "You then flash the image data onto it for each image, without needing to reflash the firmware.\r\n\r\n"
                + "When you first install the firmware, it'll reset itself, flash yellow 3 times, and then flash red.\r\n"
                + "Flashing red is expected, as that means it cannot find the image data.\r\n"
                + "Reconnect it using the same \"BOOT\" button steps as described above, load your image, and hit \"Export to RP2040\".\r\n\r\n"
                + "Again, it will reboot, but now you can unplug it and plug it into your switch.\r\n\r\n"
                + "YOU MUST HAVE \"Pro Controller Wired Commmunication\" ENABLED.\r\n"
                + "Go to system settings -> Controllers & Accessories -> Pro Controller Wired Communication\r\n"
        );
    }

    private void InGameSetupButton_Click(object? sender, RoutedEventArgs e)
    {
        _ = ShowMessageAsync(
            "In Game Setup",
            "Setup in game is fairly straightforward.\r\n"
                + "- Navigate to the palette house\r\n"
                + "- Ensure you are on the \"advanced\" drawing UI\r\n"
                + "- Ensure your top colour is set to Black (it is by default)\r\n"
                + "- Set your cursor to the TOP LEFT of where you want the drawing to be.\r\n"
                + "- Ensure the full area of the canvas that will be drawn is on screen.\r\n\r\n"
                + "If the canvas is zoomed in, it will cause the cursor to desync as the canvas moves when the cursor gets on the edges. Zooming out fully avoids this.\r\n\r\n"
                + "If your image is 256x256 or larger, set it all the way in the top left. If your image is smaller, set your cursor to where you want the topleft most pixel of your drawing to be."
        );
    }

    // this doesnt seem to work >:|
    // atleast on windows.

    private static void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Contains(DataFormat.File)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (!e.DataTransfer.Contains(DataFormat.File))
            return;
        var first = e.DataTransfer.TryGetFiles()?.FirstOrDefault();
        if (first != null)
            await LoadImageAsync(first.TryGetLocalPath() ?? "");
    }

    private void ColourLimitUpDown_ValueChanged(
        object? sender,
        NumericUpDownValueChangedEventArgs e
    )
    {
        if (!_loadingSettings)
        {
            _currentSettings.ColourLimit = (int)(ColourLimitUpDown.Value ?? 16);
            SaveSettings();
        }

        if (_currentImage != null)
            _ = UpdatePreviewAsync();
    }

    private void ThemeMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        int index =
            sender == ThemeLightMenuItem ? 1
            : sender == ThemeDarkMenuItem ? 2
            : 0;
        ThemeSystemMenuItem.IsChecked = index == 0;
        ThemeLightMenuItem.IsChecked = index == 1;
        ThemeDarkMenuItem.IsChecked = index == 2;
        SetTheme(index);
        SaveSettings();
    }

    private void SetTheme(int index)
    {
        var desiredTheme = index switch
        {
            1 => ThemeVariant.Light,
            2 => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };

        if (Application.Current is { } app)
        {
            app.RequestedThemeVariant = desiredTheme;
            _currentSettings.SelectedThemeIndex = index;
        }
    }

    private void ColourMatcherHelpButton_Click(object? sender, RoutedEventArgs e)
    {
        _ = ShowMessageAsync(
            "Colour Matchers",
            "You have 4 options for colour matchers."
                + "\nEuclidean, Redmean, and CieLab work using the Pro modes default palette."
                + "\n\nArbitrary on the other hands works using the full colour range, selecting colours in-game is slower but you can achieve much better results."
                + "\nYou can tweak the number of colours it has by changing the value to the right of this button."
                + "\nTry and pick the lowest number that looks good to your standards to minimize draw time."
                + "\nLess colours means quicker drawing, and more opportunities for the solver to find large continous blocks it can draw quickly."
                + "\nIf time is of the essence, you can also enable Denoising which can increase the number of large spots for the larger brushes."
        );
    }

    private void SaveSettings()
    {
        var json = JsonSerializer.Serialize(
            _currentSettings,
            AppSettingsContext.Default.AppSettings
        );
        File.WriteAllText(AppSettings.GetSettingsFilePath(), json);
    }

    private void GetSettings()
    {
        var settings = AppSettings.TryLoad();
        if (settings != null)
        {
            _currentSettings = settings;
        }
        else if (File.Exists(AppSettings.GetSettingsFilePath()))
        {
            // File exists but failed to parse; keep defaults but let the user know.
            AppendLog("Failed to load settings. Using defaults.");
        }

        // if no images or we fail, fall to defaults in the appsettings class.
        _currentSettings ??= new AppSettings();

        _loadingSettings = true;
        try
        {
            SwitchVersionComboBox.SelectedIndex = (int)_currentSettings.SelectedSwitchVersion - 1;
            SetTheme(_currentSettings.SelectedThemeIndex);
            ThemeSystemMenuItem.IsChecked = _currentSettings.SelectedThemeIndex == 0;
            ThemeLightMenuItem.IsChecked = _currentSettings.SelectedThemeIndex == 1;
            ThemeDarkMenuItem.IsChecked = _currentSettings.SelectedThemeIndex == 2;

            EnableExperimentalMenuItem.IsChecked = _currentSettings.EnableExperimentalFeatures;
            CheckForUpdatesCheckBox.IsChecked = _currentSettings.CheckForUpdatesOnStart;

            SelectComboBoxItem(ColourMatcherComboBox, _currentSettings.SelectedColourMatcher);
            ColourLimitUpDown.Value = _currentSettings.ColourLimit;
            ColourLimitUpDown.IsEnabled =
                ColourMatcherComboBox?.SelectedValue?.ToString() == "Arbitrary";
            SelectComboBoxItem(DenoisingComboBox, _currentSettings.SelectedDenoiser);
        }
        finally
        {
            _loadingSettings = false;
        }
    }

    private static void SelectComboBoxItem(ComboBox comboBox, string selectedItem)
    {
        var index = 0;
        foreach (var item in comboBox.Items)
        {
            if (item?.ToString() == selectedItem)
            {
                comboBox.SelectedIndex = index;
                return;
            }

            index++;
        }
    }

    private void SetControlValueWithoutSaving(Action setControlValue)
    {
        var wasLoadingSettings = _loadingSettings;
        _loadingSettings = true;
        try
        {
            setControlValue();
        }
        finally
        {
            _loadingSettings = wasLoadingSettings;
        }
    }

    private async void SwitchVersionComboBox_SelectionChanged(
        object? sender,
        SelectionChangedEventArgs e
    )
    {
        if (_loadingSettings)
            return;

        if (SwitchVersionComboBox.SelectedIndex == 0)
        {
            _currentSettings.SelectedSwitchVersion = SwitchVersion.Switch1;
            await ShowMessageAsync(
                "Switch 1 Warning",
                "Unfortunately the Switch 1 is significantly more prone to desyncing than the Switch 2."
                    + "\n\nOur leading theory as to why is that it is experiencing thermal issues whilst docked. The switch 2 by comparison has a fan in its dock, the switch 1 does not."
                    + "\nSome users have reported they could avoid the desyncs by limiting drawing to 45~ minutes or less, although the most successful method is seemingly to just undock the switch and plug the microcontroller in directly"
                    + "\nHandheld runs at 1280x720 as opposed to 1920x1080 which can reduce the power draw, and being out of the dock it can get better airflow."
                    + "\nUnfortuantely, this is still not a guarantee to avoid desyncs."
                    + "\n\nPlease keep this in mind when using the Switch 1 with this program. In a somewhat magical world, maybe Nintendo will mitigate this themselves."
            );
        }
        else if (SwitchVersionComboBox.SelectedIndex == 1)
            _currentSettings.SelectedSwitchVersion = SwitchVersion.Switch2;
        else
            _currentSettings.SelectedSwitchVersion = SwitchVersion.None;

        SaveSettings();
    }

    private void EnableExperimentalMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        if (EnableExperimentalMenuItem.IsChecked)
        {
            _ = ShowMessageAsync(
                "Experimental Features",
                "WARNING: Enabling experimental features may induce more common desyncs. Things that are prone to desyncs, but that are desired to be made stable are put here."
                    + "\nNamely, this includes bucket filling dynamic areas on the switch 2."
                    + "\nOnly enable this if you are okay with the increased chance of desyncs. Having this disabled does not guarantee it will work, but that is the goal and in 99% of cases it will work.",
                new Uri("https://github.com/Lucas7yoshi/TomodachiDrawer/issues/34"),
                "Open Experimental Feature Info"
            );
        }
        _currentSettings.EnableExperimentalFeatures = EnableExperimentalMenuItem.IsChecked;
        SaveSettings();
    }

    private void CheckForUpdatesCheckBox_Click(object? sender, RoutedEventArgs e)
    {
        _currentSettings.CheckForUpdatesOnStart = CheckForUpdatesCheckBox.IsChecked;
        SaveSettings();
    }

    private async void MenuSavePreview_Click(object? sender, RoutedEventArgs e)
    {
        if (_currentImage == null)
            return;
        // very scientific — capture UI state before going async
        var quantizerSettings = GetQuantizerSettings();
        var denoiser = DenoisingComboBox.SelectedItem?.ToString();
        var source = _currentImage;
        var img = await Task.Run(() => GetPreview(source, quantizerSettings, denoiser));
        // save it to disk... wherever desired.
        var file = await StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = "Save preview .png",
                DefaultExtension = "png",
                FileTypeChoices =
                [
                    new FilePickerFileType("Portable Network Graphics Image")
                    {
                        Patterns = ["*.png"],
                    },
                    new FilePickerFileType("All Files") { Patterns = ["*.*"] },
                ],
            }
        );

        var outputPath = file?.TryGetLocalPath();
        if (outputPath == null)
            return;

        using var data = SKImage.FromBitmap(img).Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(outputPath, data.ToArray());

        AppendLog($"Saved current preview to {outputPath}");
    }

    private void MenuToolsOpenColourToHSVStepsTool_Click(object? sender, RoutedEventArgs e) =>
        new ColourToHSVStepsTool().Show(this);

#if DEBUG
    private void MenuDebugConnectVirtualGamepad_Click(object? sender, RoutedEventArgs e)
    {
        if (
            MenuDebugConnectVirtualGamepad == null
            || MenuDebugRunInVirtualGamepad == null
            || MenuDebugOpenVirtualGamepadController == null
        )
            return;

        if (!_debugVirtualGamepad.CheckDriver())
        {
            _ = ShowMessageAsync(
                "ViGEmBus driver not found",
                "To use this feature, you must install the ViGEmBus driver.",
                new Uri("https://github.com/nefarius/ViGEmBus/releases"),
                "Download it here"
            );
            return;
        }

        if (!_debugVirtualGamepad.IsConnected)
        {
            _debugVirtualGamepad.Connect();
            MenuDebugConnectVirtualGamepad.Header = "Disconnect Virtual Gamepad";
        }
        else
        {
            MenuDebugConnectVirtualGamepad.Header = "Re-connect Virtual Gamepad";
            _debugVirtualGamepad.Disconnect();
        }

        MenuDebugRunInVirtualGamepad.IsEnabled = _debugVirtualGamepad.IsConnected;
        MenuDebugOpenVirtualGamepadController.IsEnabled = _debugVirtualGamepad.IsConnected;
    }

    private async void MenuDebugRunInVirtualGamepad_Click(object? sender, RoutedEventArgs e)
    {
        if (!_debugVirtualGamepad.IsConnected)
            return;

        if (string.IsNullOrEmpty(_currentImagePath))
        {
            _ = ShowMessageAsync("No image selected", "Select an image first.");
            return;
        }

        var imageSnapshot = _currentImage!.Copy();
        var drawSettings = GetDrawImageSettings();

        AppendLog(
            "Starting to draw with the Virtual Gamepad. Keep focus on the window you want to draw on for the duration of the drawing."
        );

        await Task.Run(async () =>
        {
            using var img = imageSnapshot;
            var drawer = new CanvasDrawer(
                new VirtualGamepadSink(_debugVirtualGamepad),
                _currentSettings.SelectedSwitchVersion,
                AppendLog
            );
            await drawer.DrawImage(img, drawSettings);
        });

        AppendLog("Virtual Gamepad is not longer being controller by the drawer.");
    }

    private void MenuDebugOpenVirtualGamepadController_Click(object? sender, RoutedEventArgs e)
    {
        if (!_debugVirtualGamepad.IsConnected)
            return;

        var window = new VirtualGamepadControllerWindow { VirtualGamepad = _debugVirtualGamepad };
        window.Show(this);
    }
#endif

    private void MenuHelpOpenGitHub_Click(object? sender, RoutedEventArgs e) =>
        Launcher.LaunchUriAsync(new Uri("https://github.com/Lucas7yoshi/TomodachiDrawer"));

    private void MenuHelpAbout_Click(object? sender, RoutedEventArgs e)
    {
        var message = $"TomodachiDrawer {GetVersionString(false)}";
        var commit = GetVersionString(true).Split("+").Last();
        message += $"\nBuilt from commit: {commit}";

        message +=
            $"\n\nCreated by Lucas7yoshi and contributors.\nThis project is Free and Open Source Software licensed under the GPLv3.0 License."
            + $"\nSource code is available on GitHub"
            + $"\n\nThis program is in no way affiliated, endorsed, sponsored or created by Nintendo.";
        _ = ShowMessageAsync("About TomodachiDrawer", message);
    }

    private void MenuExit_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void MenuHelpOpenWelcome_Click(object? sender, RoutedEventArgs e) =>
        await ShowWelcomeMessage();

    private void MenuHelpCheckForUpdate_Click(object? sender, RoutedEventArgs e) =>
        _ = PerformAsyncUpdateCheck();

    private void EnableHomeCanvas_IsCheckedChanged(object? sender, RoutedEventArgs e) { }

    private async void OpenTelemetryPrompt_Click(object? sender, RoutedEventArgs e)
    {
        var answer = await new TelemetryPrompt().ShowDialog<bool>(this);
        _currentSettings.EnableTelemetry = answer;
        _telemetry.TelemetryEnabled = answer;
        // Inform the crash reporter if we change anything
        if (answer)
            CrashReporter.Init();
        else
            CrashReporter.Disable();
        SaveSettings();
    }

    private void ReverseColourOrder_Click(object? sender, RoutedEventArgs e) { }
}
