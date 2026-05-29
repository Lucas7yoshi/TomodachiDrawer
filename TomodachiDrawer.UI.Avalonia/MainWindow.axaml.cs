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

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

using TomodachiDrawer.Core;
using TomodachiDrawer.Core.Extensions;
using TomodachiDrawer.Core.ImageProcessing;
using TomodachiDrawer.Core.ImageProcessing.Denoising;
using TomodachiDrawer.Core.ImageProcessing.Quantizers;
using TomodachiDrawer.Core.Models;
using TomodachiDrawer.Core.OutputSinks;
#if DEBUG
using TomodachiDrawer.DebugTools;
#endif
using Button = Avalonia.Controls.Button; // conflict with the Button enum in SinkEnums

namespace TomodachiDrawer.UI.Avalonia;

public partial class MainWindow : Window
{
    private static string GetRPFirmwareFileName(RPChipType chip) =>
        chip == RPChipType.RP2350 ? "TomodachiDrawer.Firmware.rp2350.uf2" : "TomodachiDrawer.Firmware.rp2040.uf2";

    private string _currentImagePath = string.Empty;
    private SKBitmap? _currentImage;
    private readonly CancellationTokenSource _cts = new();
    private TelemetryService _telemetry;

    private bool BusyExporting = false;

    //private SwitchVersion _selectedSwitchVersion = SwitchVersion.None;
    //private int _selectedThemeIndex = 0; // 0 is System.
    private AppSettings _currentSettings = new(); // All cases will result in it being non-null but IntelliSense cant see that far.

#if DEBUG
    private readonly VirtualGamepad _debugVirtualGamepad = new();

    private MenuItem? MenuDebugConnectVirtualGamepad;
    private MenuItem? MenuDebugRunInVirtualGamepad;
    private MenuItem? MenuDebugOpenVirtualGamepadController;
#endif

    public MainWindow()
    {
        InitializeComponent();

        var quantizers = ColourPalette.Quantizers.Keys.ToList();
        quantizers.Insert(0, Strings.ColourMatcher_Arbitrary);
        ColourMatcherComboBox.ItemsSource = quantizers;
        ColourMatcherComboBox.SelectedIndex = 0;

        var denoiserSelection = new List<string> { Strings.Denoising_None };
        denoiserSelection.AddRange(ImageDenoiser.Denoisers.Keys);

        DenoisingComboBox.ItemsSource = denoiserSelection;
        DenoisingComboBox.SelectedIndex = 0;
        DenoisingComboBox.SelectionChanged += async (_, _) => await UpdatePreviewAsync();

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
            var menuItem = new MenuItem()
            {
                Header = desc
            };
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
                AppendLog($"Template editor failed to load the template for {mask.GetDescription()}");
                _ = ShowMessageAsync("Error loading template", "The template tool could not find the image. This REALLY shouldn't happen... Try reinstalling?");
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
                Strings.Dialog_MissingVCRuntime_Title,
                Strings.Dialog_MissingVCRuntime_Message,
                new Uri("https://aka.ms/vc14/vc_redist.x64.exe"),
                Strings.Dialog_MissingVCRuntime_DownloadButton
            );
        }

        if (_currentSettings.EnableTelemetry == true)
        {
            _telemetry.TelemetryEnabled = true;
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
            "0.6.0 has added support for RP2350 based boards (RP2350-Zero, Raspberry Pi Pico 2, etc) on top of the RP2040 support." +
            "\n\n0.5.0 added a tool for helping you with more complex, non square templates." +
            "\nAt the top menu bar, select \"Templates\" and choose the item type you want, it will open an editor with a preview of the layout, and copy it to your clipboard for you to easily edit in other image editing software."
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
        var debugMenuItem = new MenuItem()
        {
            Header = "_Debug",
        };
        Menu.Items.Add(debugMenuItem);

        MenuDebugConnectVirtualGamepad = new MenuItem()
        {
            Header = "_Connect Virtual Gamepad",
        };
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
                        Strings.Dialog_UpdateAvailable_Title,
                        "A new update is available on GitHub."
                            + $"\nCurrent Version: {ourVersion}"
                            + $"\nLatest Version: {releaseVersionTag}"
                            + $"\nVersion title: {responseJsonObject.RootElement.GetProperty("name").GetString() ?? "N/A"}"
                            + $"\n\nDownload at:\nhttps://github.com/Lucas7yoshi/TomodachiDrawer",
                        new Uri("https://github.com/Lucas7yoshi/TomodachiDrawer/releases"),
                        Strings.Dialog_UpdateAvailable_OpenReleases
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
                    Strings.Dialog_PermissionDenied_Title,
                    $"Permission to access the microcontrollers drive ({drivePath}) was denied.\n\n"
                        + "Please open System Settings -> Privacy & Security -> Files & Folders, find \"TomodachiDrawer\", and make sure \"Removable Volumes\" is enabled.\n\n"
                        + "This is required for the app to write the firmware directly to your Pico drive.\r"
                        + $"Or you can manually copy the .uf2 file to {drivePath} if you want to avoid granting permissions.",
                    new Uri("x-apple.systempreferences:com.apple.preference.security?Privacy_FilesAndFolders"),
                    Strings.Dialog_OpenSystemSettings
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
            bool lastRp2040 = false, lastRp2350 = false;
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    var rp2040Path = UF2Flasher.FindRP2040Drive();
                    var rp2350Path = UF2Flasher.FindRP2350Drive();

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        bool hasImage = _currentImage != null;
                        lastRp2040 = UpdateChipUI(RPChipType.RP2040, rp2040Path, hasImage, lastRp2040);
                        lastRp2350 = UpdateChipUI(RPChipType.RP2350, rp2350Path, hasImage, lastRp2350);
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
        Button flashButton, exportButton, exportUF2Button;
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
            AppendLog("Image is full canvas size, so enabling auto home by default.\nYou can disable it if it causes you trouble and manually home before connecting.");
            EnableHomeCanvas.IsChecked = true;
        }

        await UpdatePreviewAsync();
        TSPTimeLimitUpDown.Value = (decimal)
            CanvasDrawer.GetRecommendedTSPSolveTime(img.Width, img.Height);
        AppendLog($"Loaded image: {displayName} ({img.Width}x{img.Height})");
    }

    private SKBitmap GetPreview(SKBitmap source, QuantizerSettings quantizerSettings, string? denoiser)
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

        var preview = await Task.Run(() => GetPreview(source, quantizerSettings, denoiser)).ConfigureAwait(true);

        PreviewImage.Source = ToAvaloniaBitmap(preview);
        // update the preview label to indicate the size of the image just for user reference
        PreviewHeader.Text = string.Format(Strings.Preview_Header_WithSize, _currentImage.Width, _currentImage.Height);
        AppendLog(
            $"Updated preview for {_currentImagePath} using {quantizerSettings.quantizerName}"
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
            Content = Strings.Dialog_OK,
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
                Content = linkButtonText ?? Strings.Dialog_OpenLink,
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
                Title = Strings.Dialog_OpenImage_Title,
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType(Strings.Dialog_OpenImage_FilterImages)
                    {
                        Patterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif", "*.webp"],
                    },
                    new FilePickerFileType(Strings.Dialog_OpenImage_FilterAll) { Patterns = ["*.*"] },
                ],
            }
        );

        if (files.Count > 0)
            await LoadImageAsync(files[0].TryGetLocalPath() ?? "");
    }

    private async void ColourMatcherComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_currentImage != null)
            await UpdatePreviewAsync();
        ColourLimitUpDown.IsEnabled =
            ColourMatcherComboBox?.SelectedValue?.ToString() == "Arbitrary";
    }

    private void TSPHelpButton_Click(object? sender, RoutedEventArgs e)
    {
        _ = ShowMessageAsync(Strings.Dialog_TSPHelp_Title, Strings.Dialog_TSPHelp_Message);
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
            _ = ShowMessageAsync(Strings.Dialog_SelectSwitchVersion_Title, Strings.Dialog_SelectSwitchVersion_Message);
            return;
        }

        var chip = sender == RP2350ExportButton ? RPChipType.RP2350 : RPChipType.RP2040;
        var exportButton = chip == RPChipType.RP2350 ? RP2350ExportButton : RP2040ExportButton;
        string chipName = chip == RPChipType.RP2350 ? "RP2350" : "RP2040";

        var colourCount = CountDistinctColours(_currentImage);
        var imageWidth = _currentImage.Width;
        var imageHeight = _currentImage.Height;
        var imageSnapshot = _currentImage!.Copy();
        var denoiser = DenoisingComboBox.SelectedItem?.ToString();
        var tspLimit = (float)(TSPTimeLimitUpDown.Value ?? 0.5m);
        var settings = GetQuantizerSettings();
        var enableExperimental = EnableExperimentalMenuItem.IsChecked;
        var enableHome = EnableHomeCanvas.IsChecked ?? false;
        string quantizerName = ColourMatcherComboBox.SelectedItem!.ToString()!;
        int? colourLimit = quantizerName == "Arbitrary" ? (int)(ColourLimitUpDown.Value ?? 32) : (int?)null;

        BusyExporting = true;
        exportButton.IsEnabled = false;

        try
        {
            var (uf2Bytes, totalTime) = await GenerateUF2Async(
                chip, imageSnapshot, settings, denoiser, tspLimit, enableExperimental, enableHome,
                $"Exporting to {chipName} flash");

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

            _ = _telemetry.ReportImage(new ImageEventDto(
                imageWidth, imageHeight, colourCount, quantizerName, colourLimit,
                _currentSettings.SelectedSwitchVersion.ToString(),
                enableExperimental, totalTime.TotalSeconds, tspLimit
            ));

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
        DrawTimeLabel.Text = string.Format(Strings.DrawTimeEstimate_Format, estimateStr);
    }

    private async Task<(byte[]? uf2Bytes, TimeSpan totalTime)> GenerateUF2Async(
        RPChipType chip,
        SKBitmap imageSnapshot,
        QuantizerSettings settings,
        string? denoiser,
        float tspLimit,
        bool enableExperimental,
        bool homeToTopLeft,
        string logPrefix)
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
            var drawSettings = new DrawImageSettings()
            {
                QuantizerSettings = settings,
                DenoiserName = denoiser,
                TSPTimeLimit = tspLimit,
                DisableLargeBrush = false,
                EnableExperimentalFeatures = enableExperimental,
                HomeToTopLeft = homeToTopLeft,
            };
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

        return new()
        {
            QuantizerSettings = quantizerSettings,
            DenoiserName = denoiser,
            TSPTimeLimit = tspLimit,
            DisableLargeBrush = false,
            EnableExperimentalFeatures = enableExperimental,
            HomeToTopLeft = enableHome,
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
            _ = ShowMessageAsync(Strings.Dialog_SelectSwitchVersion_Title, Strings.Dialog_SelectSwitchVersion_Message);
            return;
        }

        var chip = sender == RP2350ExportUF2Button ? RPChipType.RP2350 : RPChipType.RP2040;
        var exportUF2Button = chip == RPChipType.RP2350 ? RP2350ExportUF2Button : RP2040ExportUF2Button;

        var file = await StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = Strings.Dialog_SaveUF2_Title,
                DefaultExtension = "uf2",
                FileTypeChoices =
                [
                    new FilePickerFileType(Strings.Dialog_SaveUF2_FilterUF2) { Patterns = ["*.uf2"] },
                    new FilePickerFileType(Strings.Dialog_OpenImage_FilterAll) { Patterns = ["*.*"] },
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
        var denoiser = DenoisingComboBox.SelectedItem?.ToString();
        var tspLimit = (float)(TSPTimeLimitUpDown.Value ?? 0.5m);
        var settings = GetQuantizerSettings();
        var enableExperimental = EnableExperimentalMenuItem.IsChecked;
        string quantizerName = ColourMatcherComboBox.SelectedItem!.ToString()!;
        int? colourLimit = quantizerName == "Arbitrary" ? (int)(ColourLimitUpDown.Value ?? 32) : (int?)null;

        exportUF2Button.IsEnabled = false;
        BusyExporting = true;

        try
        {
            var (uf2Bytes, totalTime) = await GenerateUF2Async(
                chip, imageSnapshot, settings, denoiser, tspLimit, enableExperimental, false,
                "Exporting to UF2");

            if (uf2Bytes != null && uf2Bytes.Length > 0)
            {
                File.WriteAllBytes(outputPath, uf2Bytes);
                AppendLog($"Saved UF2 to {outputPath}");
            }

            _ = _telemetry.ReportImage(new ImageEventDto(
                imageWidth, imageHeight, colourCount, quantizerName, colourLimit,
                _currentSettings.SelectedSwitchVersion.ToString(),
                enableExperimental, totalTime.TotalSeconds, tspLimit
            ));

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
                Strings.Dialog_FlashFirmwareError_Title,
                $"Could not locate {firmwareFileName}."
                    + "\nPlease ensure that you extracted all files from the zip before running."
                    + $"\nAlternatively, you can manually drag {firmwareFileName} to the {chipName} drive."
            );
            return;
        }
        if (drivePath == null)
        {
            _ = ShowMessageAsync("Error", $"{chipName} not detected. Connect it in BOOT mode first.");
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

        _ = ShowMessageAsync("", Strings.Dialog_FlashFirmwareSuccess_Message);
        AppendLog($"Flashed base firmware to {chipName}");
    }

    private void OutputExplanationButton_Click(object? sender, RoutedEventArgs e)
    {
        _ = ShowMessageAsync("", Strings.Dialog_SetupExplanation_Message);
    }

    private void InGameSetupButton_Click(object? sender, RoutedEventArgs e)
    {
        _ = ShowMessageAsync(Strings.Dialog_InGameSetup_Title, Strings.Dialog_InGameSetup_Message);
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
    ) => _ = UpdatePreviewAsync();

    private void ThemeMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        int index = sender == ThemeLightMenuItem ? 1 : sender == ThemeDarkMenuItem ? 2 : 0;
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
        _ = ShowMessageAsync(Strings.Dialog_ColourMatcherHelp_Title, Strings.Dialog_ColourMatcherHelp_Message);
    }

    private static string GetSettingsFilePath()
    {
        const string settingsFileName = "settings.json";

        // Check if we're running on macOS and the app is running from the app bundle, not CLI.
        if (OperatingSystem.IsMacOS() && AppContext.BaseDirectory.Contains(".app/Contents/MacOS"))
        {
            // In macOS, when you launch `.app` from Finder, the current working directory is root directory `/` (Gemini said),
            // which is read-only and not a good place to store our settings file.
            // We need to place the settings file somewhere else.
            // `~/Library/Application Support` is a common place to store app data on macOS (like `%APPDATA%` on Windows).
            // So first, ensure `~/Library/Application Support/TomodachiDrawer` exists
            var appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TomodachiDrawer");
            if (!Directory.Exists(appDataFolder))
            {
                Directory.CreateDirectory(appDataFolder);
            }
            // Returns `~/Library/Application Support/TomodachiDrawer/settings.json`
            return Path.Combine(appDataFolder, settingsFileName);
        }
        else
        {
            // Simply place it in the current working directory
            return settingsFileName;
        }
    }

    private void SaveSettings()
    {
        var json = JsonSerializer.Serialize(_currentSettings, AppSettingsContext.Default.AppSettings);
        File.WriteAllText(GetSettingsFilePath(), json);
    }

    private void GetSettings()
    {
        var settingsFilePath = GetSettingsFilePath();

        if (File.Exists(settingsFilePath))
        {
            try
            {
                var json = File.ReadAllText(settingsFilePath);
                var settings = JsonSerializer.Deserialize(json, AppSettingsContext.Default.AppSettings);

                if (settings != null)
                {
                    _currentSettings = settings;
                }
            }
            catch (Exception)
            {
                AppendLog("Failed to load settings. Using defaults.");
            }
        }

        // if no images or we fail, fall to defaults in the appsettings class.
        _currentSettings ??= new AppSettings();

        SwitchVersionComboBox.SelectedIndex =
            (int)_currentSettings.SelectedSwitchVersion - 1;
        SetTheme(_currentSettings.SelectedThemeIndex);
        ThemeSystemMenuItem.IsChecked = _currentSettings.SelectedThemeIndex == 0;
        ThemeLightMenuItem.IsChecked = _currentSettings.SelectedThemeIndex == 1;
        ThemeDarkMenuItem.IsChecked = _currentSettings.SelectedThemeIndex == 2;

        EnableExperimentalMenuItem.IsChecked =
            _currentSettings.EnableExperimentalFeatures;
        CheckForUpdatesCheckBox.IsChecked = _currentSettings.CheckForUpdatesOnStart;
    }

    private void SwitchVersionComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (SwitchVersionComboBox.SelectedIndex == 0)
            _currentSettings.SelectedSwitchVersion = SwitchVersion.Switch1;
        else
            _currentSettings.SelectedSwitchVersion = SwitchVersion.Switch2;
        SaveSettings();
    }

    private void EnableExperimentalMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        if (EnableExperimentalMenuItem.IsChecked)
        {
            _ = ShowMessageAsync(
                Strings.Dialog_ExperimentalFeatures_Title,
                Strings.Dialog_ExperimentalFeatures_Message,
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
                Title = Strings.Dialog_SavePreview_Title,
                DefaultExtension = "png",
                FileTypeChoices =
                [
                    new FilePickerFileType("Portable Network Graphics Image")
                    {
                        Patterns = ["*.png"],
                    },
                    new FilePickerFileType(Strings.Dialog_OpenImage_FilterAll) { Patterns = ["*.*"] },
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
        ) return;

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
            _ = ShowMessageAsync(
                "No image selected",
                "Select an image first."
            );
            return;
        }

        var imageSnapshot = _currentImage!.Copy();
        var drawSettings = GetDrawImageSettings();

        AppendLog("Starting to draw with the Virtual Gamepad. Keep focus on the window you want to draw on for the duration of the drawing.");

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

        var window = new VirtualGamepadControllerWindow
        {
            VirtualGamepad = _debugVirtualGamepad
        };
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
        _ = ShowMessageAsync(Strings.Dialog_About_Title, message);
    }

    private void MenuExit_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void MenuHelpOpenWelcome_Click(object? sender, RoutedEventArgs e) => await ShowWelcomeMessage();

    private void MenuHelpCheckForUpdate_Click(object? sender, RoutedEventArgs e) => _ = PerformAsyncUpdateCheck();

    private void EnableHomeCanvas_IsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        // TODO: Notify if non 256x256 image.
    }

    private async void OpenTelemetryPrompt_Click(object? sender, RoutedEventArgs e)
    {
        var answer = await new TelemetryPrompt().ShowDialog<bool>(this);
        _currentSettings.EnableTelemetry = answer;
        _telemetry.TelemetryEnabled = answer;
        SaveSettings();
    }
}
