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

using SkiaSharp;

using TomodachiDrawer.Core;
using TomodachiDrawer.Core.ImageProcessing.Denoising;
using TomodachiDrawer.Core.ImageProcessing.Quantizers;
using TomodachiDrawer.Core.Models;
using TomodachiDrawer.Core.OutputSinks;

using Button = Avalonia.Controls.Button; // conflict with the Button enum in SinkEnums

namespace TomodachiDrawer.UI.Avalonia;

public partial class MainWindow : Window
{
    private const string RP2040FirmwareFileName = "TomodachiDrawer.Firmware.rp2040.uf2";
    private const string RP2350FirmwareFileName = "TomodachiDrawer.Firmware.rp2350.uf2";
    
    private string _currentImagePath = string.Empty;
    private bool _isBoardConnected = false;
    private readonly CancellationTokenSource _cts = new();

    private bool BusyExporting = false;

    //private SwitchVersion _selectedSwitchVersion = SwitchVersion.None;
    //private int _selectedThemeIndex = 0; // 0 is System.
    private AppSettings _currentSettings = new(); // All cases will result in it being non-null but IntelliSense cant see that far.

    private UF2Flasher.BoardType _selectedBoardType => BoardTypeComboBox != null
        ? (UF2Flasher.BoardType)(BoardTypeComboBox.SelectedIndex)
        : UF2Flasher.BoardType.Unknown;

    public MainWindow()
    {
        InitializeComponent();

        var quantizers = ColourPalette.Quantizers.Keys.ToList();
        quantizers.Insert(0, "Arbitrary");
        ColourMatcherComboBox.ItemsSource = quantizers;
        ColourMatcherComboBox.SelectedIndex = 0;

        var denoiserSelection = new List<string> { "None" };
        denoiserSelection.AddRange(ImageDenoiser.Denoisers.Keys);

        DenoisingComboBox.ItemsSource = denoiserSelection;
        DenoisingComboBox.SelectedIndex = 0;
        DenoisingComboBox.SelectionChanged += (_, _) => UpdatePreview();

        GetSettings();



        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);

#if DEBUG
        this.Title = $"TomodachiDrawer.UI.Avalonia - {GetVersionString(true)}";
#else
        this.Title = $"TomodachiDrawer - {GetVersionString(false)}";
#endif

        StartBoardPolling();
        if (CheckForUpdatesCheckBox.IsChecked)
            _ = PerformAsyncUpdateCheck();

        UpdateFirmwareButtons();

        if (_currentSettings.FirstStartId != CURRENT_WELCOME_ID)
        {
            Opened += MainWindow_Opened;
        }
    }

    private async void MainWindow_Opened(object? sender, EventArgs e)
    {
        ShowWelcomeMessage();
        _currentSettings.FirstStartId = CURRENT_WELCOME_ID;
        SaveSettings();
    }

    // Welcome message stuff. For important changes, the ID is incremented by one by hand whenever something notable changes.
    // This is only really needed for Mac since its settings are saved in a way that persists more readily.
    private const int CURRENT_WELCOME_ID = 1;
    private async void ShowWelcomeMessage()
    {
        await ShowMessageAsync(
            "Welcome to TomodachiDrawer",
            "As of 0.4.7, the Base Firmware has been tweaked to fix a slowdown introduced in 0.3.3. " +
            "You are encouraged to hit the Flash Base Firmware button again if you flashed prior to this, its harmless if you aren't sure. " +
            "\nIf this is your first time using TomodachiDrawer, you do not need to worry about this. " +
            "\n\nHappy (computer assisted) drawing!"
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

    // ── Board polling ────────────────────────────────────────────────

    private void StartBoardPolling()
    {
        _ = Task.Run(async () =>
        {
            bool lastState = false;
            while (!_cts.Token.IsCancellationRequested)
            {
                var path = UF2Flasher.FindBoardDrive();
                var detectedBoardType = path != null ? UF2Flasher.DetectBoardType(path) : UF2Flasher.BoardType.Unknown;
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    bool hasImage = !string.IsNullOrEmpty(_currentImagePath);
                    bool isBoardSupported = detectedBoardType != UF2Flasher.BoardType.Unknown;

                    if (path != null)
                    {
                        string boardName = detectedBoardType switch
                        {
                            UF2Flasher.BoardType.RP2040 => "RP2040",
                            UF2Flasher.BoardType.RP2350 => "RP2350",
                            _ => "Unknown Board",
                        };
                        BoardStatusLabel.Text = $"{boardName} found: {path}";
                        BoardStatusLabel.Foreground = Brushes.Green;

                        _isBoardConnected = true;

                        if (!lastState)
                        {
                            BoardTypeComboBox.SelectedIndex = (int)(detectedBoardType);

                            AppendLog($"{boardName} connected @ {path}");
                            lastState = true;
                        }
                    }
                    else
                    {
                        BoardStatusLabel.Text = "Board not found";
                        BoardStatusLabel.Foreground = Brushes.Red;

                        _isBoardConnected = false;

                        if (lastState)
                        {
                            BoardTypeComboBox.SelectedIndex = (int)(UF2Flasher.BoardType.Unknown);

                            AppendLog("Board disconnected...");
                            lastState = false;
                        }
                    }

                    UpdateFirmwareButtons();
                });

                try
                {
                    await Task.Delay(1000, _cts.Token);
                }
                catch (System.OperationCanceledException)
                {
                    break;
                }
            }
        });
    }

    #region Image/Preview
    private void LoadImage(string path)
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
            img.Dispose();
            img = resized;

            string tempPath = Path.Combine(
                Path.GetTempPath(),
                $"tomodachi_{Path.GetFileName(path)}"
            );
            using var data = SKImage.FromBitmap(img).Encode(SKEncodedImageFormat.Png, 100);
            using var stream = File.OpenWrite(tempPath);
            data.SaveTo(stream);

            path = tempPath;
            AppendLog($"Image resized to {newWidth}x{newHeight}, saved to temp: {tempPath}");
        }

        _currentImagePath = path;
        ImagePathBox.Text = path;
        UpdateFirmwareButtons();

        if (img.Width == 256 && img.Height == 256)
        {
            AppendLog("Image is full canvas size, so enabling auto home by default.\nYou can disable it if it causes you trouble and manually home before connecting.");
            EnableHomeCanvas.IsChecked = true;
        }

        UpdatePreview();
        TSPTimeLimitUpDown.Value = (decimal)
            CanvasDrawer.GetRecommendedTSPSolveTime(img.Width, img.Height);
        AppendLog($"Loaded image: {Path.GetFileName(path)} ({img.Width}x{img.Height})");
        img.Dispose();
    }

    private SKBitmap GetPreview()
    {
        var pal = new ColourPalette(new DummySink());
        var denoiser = DenoisingComboBox.SelectedItem?.ToString();
        var quantizerSettings = GetQuantizerSettings();
        var preview = pal.PreviewColourMapping(
            SKBitmap.Decode(_currentImagePath),
            quantizerSettings,
            denoiser
        );
        return preview;
    }

    private void UpdatePreview()
    {
        if (!File.Exists(_currentImagePath))
        {
            AppendLog($"File does not exist, cannot update preview: {_currentImagePath}");
            return;
        }

        var quantizerSettings = GetQuantizerSettings();
        var preview = GetPreview();

        PreviewImage.Source = ToAvaloniaBitmap(preview);
        AppendLog(
            $"Updated preview for {Path.GetFileName(_currentImagePath)} using {quantizerSettings.quantizerName}"
        );
    }

    private static Bitmap? ToAvaloniaBitmap(SKBitmap skBitmap)
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

    private void UpdateFirmwareButtons()
    {
        bool hasImage = !string.IsNullOrEmpty(_currentImagePath);
        bool isBoardSupported = _selectedBoardType != UF2Flasher.BoardType.Unknown;

        FlashFirmwareButton?.IsEnabled = _isBoardConnected && isBoardSupported && !BusyExporting;
        ExportToBoardButton?.IsEnabled = _isBoardConnected && isBoardSupported && hasImage && !BusyExporting;
        ExportUF2Button?.IsEnabled = isBoardSupported && hasImage && !BusyExporting;
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
            LoadImage(files[0].TryGetLocalPath() ?? "");
    }

    private void ColourMatcherComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_currentImagePath))
            UpdatePreview();
        ColourLimitUpDown.IsEnabled =
            ColourMatcherComboBox?.SelectedValue?.ToString() == "Arbitrary";
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

    private async void ExportToBoardButton_Click(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentImagePath))
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

        var imagePath = _currentImagePath;
        var denoiser = DenoisingComboBox.SelectedItem?.ToString();
        var tspLimit = (float)(TSPTimeLimitUpDown.Value ?? 0.5m);
        var boardType = _selectedBoardType;

        BusyExporting = true;
        UpdateFirmwareButtons();

        TimeSpan totalTime = TimeSpan.MaxValue;
        var settings = GetQuantizerSettings();
        var enableExperimental = EnableExperimentalCheckBox.IsChecked ?? false;
        var enableHome = EnableHomeCanvas.IsChecked ?? false;

        await Task.Run(async () =>
        {
            string tempPath = Path.Combine(
                Path.GetTempPath(),
                $"rp2040output{System.Random.Shared.Next(1000000, 9999999)}.tdld"
            );

            AppendLog($"Exporting to board's flash ({Path.GetFileName(tempPath)})");
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
                HomeToTopLeft = enableHome,
            };
            await drawer.DrawImage(SKBitmap.Decode(imagePath), drawSettings);
            AppendLog($"True complete overall time is: {timingSink.TotalTime.TotalSeconds}s");

            var fileSink = new FileControllerSink(tempPath);
            timingSink.ReplayTo(fileSink);
            fileSink.Dispose();

            var tdldBytes = File.ReadAllBytes(tempPath);
            var uf2Bytes = UF2Flasher.BuildTDLDUF2(tdldBytes, boardType);
            var drivePath = UF2Flasher.FindBoardDrive();

            if (uf2Bytes != null && uf2Bytes.Length > 0 && drivePath != null)
            {
                File.WriteAllBytes(Path.Combine(drivePath, "tdld_image.uf2"), uf2Bytes);
                AppendLog(
                    "Wrote to board's flash. Unplug the board and plug it into your Nintendo Switch without holding any buttons."
                );
            }

            if (File.Exists(tempPath))
                File.Delete(tempPath);
            totalTime = timingSink.TotalTime;
        });

        BusyExporting = false;
        UpdateFirmwareButtons();

        SetEstimate(totalTime);
    }

    private void SetEstimate(TimeSpan time)
    {
        var estimateStr = $"{time:h\\hm\\ms\\s}";
        DrawTimeLabel.Text = $"Draw Time Estimate: {estimateStr}";
    }

    private async void ExportUF2Button_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentImagePath))
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

        var imagePath = _currentImagePath;
        var denoiser = DenoisingComboBox.SelectedItem?.ToString();
        var tspLimit = (float)(TSPTimeLimitUpDown.Value ?? 0.5m);

        BusyExporting = true;
        UpdateFirmwareButtons();

        TimeSpan totalTime = TimeSpan.MaxValue;
        var settings = GetQuantizerSettings();
        var enableExperimental = EnableExperimentalCheckBox.IsChecked ?? false;

        await Task.Run(async () =>
        {
            string tempPath = Path.Combine(
                Path.GetTempPath(),
                $"rp2040output{System.Random.Shared.Next(1000000, 9999999)}.tdld"
            );

            AppendLog($"Exporting to UF2 ({Path.GetFileName(tempPath)})");
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
            };
            await drawer.DrawImage(SKBitmap.Decode(imagePath), drawSettings);
            AppendLog($"True complete overall time is: {timingSink.TotalTime.TotalSeconds}s");

            var fileSink = new FileControllerSink(tempPath);
            timingSink.ReplayTo(fileSink);
            fileSink.Dispose();

            var tdldBytes = File.ReadAllBytes(tempPath);
            var uf2Bytes = UF2Flasher.BuildTDLDUF2(tdldBytes, _selectedBoardType);

            if (uf2Bytes != null && uf2Bytes.Length > 0)
            {
                File.WriteAllBytes(outputPath, uf2Bytes);
                AppendLog($"Saved UF2 to {outputPath}");
            }

            if (File.Exists(tempPath))
                File.Delete(tempPath);
            totalTime = timingSink.TotalTime;
        });

        BusyExporting = false;
        UpdateFirmwareButtons();

        SetEstimate(totalTime);
    }

    private static string GetBaseFirmwareFileName(UF2Flasher.BoardType boardType) => boardType switch
    {
        UF2Flasher.BoardType.RP2350 => RP2350FirmwareFileName,
        // RP2040 is the most common board, even if detection fails.
        _ => RP2040FirmwareFileName,
    };

    private static string GetBaseFirmwareFilePath(UF2Flasher.BoardType boardType)
    {
        var firmwareFileName = GetBaseFirmwareFileName(boardType);

        // Check if we're running on macOS and the app is running from app bundle, not CLI.
        var baseDirectory = AppContext.BaseDirectory;
        if (OperatingSystem.IsMacOS() && baseDirectory.Contains(".app/Contents/MacOS"))
        {
            // In macOS, when you launch `.app` from Finder, the current working directory is root directory `/` (Gemini said),
            // and the firmware file isn't located there (`/TomodachiDrawer.Firmware.(board).uf2`).
            // So we need to find the firmware file in the app bundle.
            // `AppContext.BaseDirectory` resolves to `/path/to/TomodachiDrawer.app/Contents/MacOS/`, so we can get the path to the firmware file from there.
            // The firmware file should locate at `/path/to/TomodachiDrawer.app/Contents/MacOS/TomodachiDrawer.Firmware.(board).uf2`
            return Path.Combine(baseDirectory, firmwareFileName);
        }
        else
        {
            // Simply use the file in current working directory
            return firmwareFileName;
        }
    }

    private void BoardTypeComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        UpdateFirmwareButtons();
    }

    private void FlashFirmwareButton_Click(object? sender, RoutedEventArgs e)
    {
        string firmwareFilePath = GetBaseFirmwareFilePath(_selectedBoardType);
        var drivePath = UF2Flasher.FindBoardDrive();

        if (!File.Exists(firmwareFilePath))
        {
            _ = ShowMessageAsync(
                "Error flashing base firmware",
                $"For some reason could not locate {firmwareFilePath}"
                + $"\nPlease ensure that you extracted the program to a zip folder, and ran the executable from that extracted folder."
                + $"\nIf you can still not flash with this button, you can manually drag the {firmwareFilePath} file to the RPI-RP2 or RP2350 drive on your system to flash it."
            );
            return;
        }
        if (drivePath == null)
        {
            _ = ShowMessageAsync("Error", "No board detected. Connect it in BOOT mode first.");
            return;
        }

        File.Copy(firmwareFilePath, Path.Combine(drivePath, GetBaseFirmwareFileName(_selectedBoardType)), overwrite: true);

        var timeout = System.DateTime.Now.AddSeconds(10);
        while (UF2Flasher.FindBoardDrive() != null)
        {
            if (System.DateTime.Now > timeout)
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
        AppendLog($"Flashed base firmware to board\r\n");
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

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (!e.DataTransfer.Contains(DataFormat.File))
            return;
        var first = e.DataTransfer.TryGetFiles()?.FirstOrDefault();
        if (first != null)
            LoadImage(first.TryGetLocalPath() ?? "");
    }

    private void ColourLimitUpDown_ValueChanged(
        object? sender,
        NumericUpDownValueChangedEventArgs e
    ) => UpdatePreview();

    private void AppThemeComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // Why does avalonia call this before AppThemeComboBox exists?? lol
        if (AppThemeComboBox == null)
            return;

        SetTheme(AppThemeComboBox.SelectedIndex);
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
        AppThemeComboBox.SelectedIndex = _currentSettings.SelectedThemeIndex;

        EnableExperimentalCheckBox.IsChecked =
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

    private void EnableExperimentalCheckBox_IsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (EnableExperimentalCheckBox.IsChecked == true)
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
        _currentSettings.EnableExperimentalFeatures = EnableExperimentalCheckBox.IsChecked ?? true;
        SaveSettings();
    }

    private void CheckForUpdatesCheckBox_Click(object? sender, RoutedEventArgs e)
    {
        _currentSettings.CheckForUpdatesOnStart = CheckForUpdatesCheckBox.IsChecked;
        SaveSettings();
    }

    private async void MenuSavePreview_Click(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentImagePath))
            return;
        // very scientific
        var img = GetPreview();
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

    private void MenuHelpOpenWelcome_Click(object? sender, RoutedEventArgs e) => ShowWelcomeMessage();

    private void MenuHelpCheckForUpdate_Click(object? sender, RoutedEventArgs e) => _ = PerformAsyncUpdateCheck();

    private void EnableHomeCanvas_IsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        // TODO: Notify if non 256x256 image.
    }
}