using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

using SkiaSharp;

using TomodachiDrawer.Core;
using TomodachiDrawer.Core.ImageProcessing;
using TomodachiDrawer.Core.OutputSinks;

using Button = Avalonia.Controls.Button; // conflict with the Button enum in SinkEnums

namespace TomodachiDrawer.UI.Avalonia;

public partial class MainWindow : Window
{
    private string _currentImagePath = string.Empty;
    private Dictionary<PaletteColour, SKBitmap> _colourLayersDebug = new();
    private readonly CancellationTokenSource _cts = new();

    public MainWindow()
    {
        InitializeComponent();

        ColorMatcherComboBox.ItemsSource = ColourPalette.Quantizers.Keys.ToList();
        ColorMatcherComboBox.SelectedIndex = 0;

        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);

        StartRP2040Polling();
    }

    protected override void OnClosed(System.EventArgs e)
    {
        _cts.Cancel();
        base.OnClosed(e);
    }

    // ── RP2040 polling ────────────────────────────────────────────────

    private void StartRP2040Polling()
    {
        _ = Task.Run(async () =>
        {
            bool lastState = false;
            while (!_cts.Token.IsCancellationRequested)
            {
                var path = UF2Flasher.FindRP2040Drive();
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    bool hasImage = !string.IsNullOrEmpty(_currentImagePath);

                    // ExportUF2 only needs an image — no RP2040 required
                    ExportUF2Button.IsEnabled = hasImage;

                    if (path != null)
                    {
                        RP2040StatusLabel.Text = $"RP2040 found: {path}";
                        RP2040StatusLabel.Foreground = Brushes.Green;

                        FlashFirmwareButton.IsEnabled = true;
                        ExportRP2040Button.IsEnabled = hasImage;
                        if (!lastState) { AppendLog($"RP2040 connected @ {path}"); lastState = true; }
                    }
                    else
                    {
                        RP2040StatusLabel.Text = "RP2040 not found";
                        RP2040StatusLabel.Foreground = Brushes.Red;

                        FlashFirmwareButton.IsEnabled = false;
                        ExportRP2040Button.IsEnabled = false;
                        if (lastState) { AppendLog("RP2040 disconnected..."); lastState = false; }
                    }
                });

                try { await Task.Delay(1000, _cts.Token); }
                catch (System.OperationCanceledException) { break; }
            }
        });
    }

    // ── Image loading & preview ───────────────────────────────────────

    private void LoadImage(string path)
    {
        if (!File.Exists(path)) { AppendLog($"File does not exist..? {path}"); return; }

        var img = SKBitmap.Decode(path);
        if (img == null) { AppendLog($"Failed to decode image: {path}"); return; }

        if (img.Width > 256 || img.Height > 256)
        {
            _ = ShowMessageAsync("Image too big", $"{Path.GetFileName(path)} is too big! Max of 256x256.");
            return;
        }

        _currentImagePath = path;
        ImagePathBox.Text = path;
        ExportUF2Button.IsEnabled = true;
        UpdatePreview();
        AppendLog($"Loaded image: {Path.GetFileName(path)} ({img.Width}x{img.Height})");
    }

    private void UpdatePreview()
    {
        if (!File.Exists(_currentImagePath))
        {
            AppendLog($"File does not exist, cannot update preview: {_currentImagePath}");
            return;
        }

        var quantizer = ColorMatcherComboBox.SelectedItem?.ToString();
        if (quantizer == null) return;

        var pal = new ColourPalette(new DummySink());
        var preview = pal.PreviewColourMapping(SKBitmap.Decode(_currentImagePath), quantizer);
        PreviewImage.Source = ToAvaloniaBitmap(preview);
        AppendLog($"Updated preview for {Path.GetFileName(_currentImagePath)} using {quantizer}");
    }

    private static Bitmap? ToAvaloniaBitmap(SKBitmap skBitmap)
    {
        using var image = SKImage.FromBitmap(skBitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = new MemoryStream(data.ToArray());
        return new Bitmap(stream);
    }

    private void AppendLog(string msg)
    {
        Dispatcher.UIThread.Post(() =>
        {
            LogBox.Text = (LogBox.Text ?? "") + msg + "\n";
            LogBox.CaretIndex = LogBox.Text?.Length ?? 0;
        });
    }

    // messagebox replacement
    private async Task ShowMessageAsync(string title, string message)
    {
        var okButton = new Button
        {
            Content = "OK",
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 10, 0, 0),
            MinWidth = 80
        };

        var dialog = new Window
        {
            Title = title,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Width = 440,
            SizeToContent = SizeToContent.Height,
            Content = new StackPanel
            {
                Margin = new Thickness(16),
                Children =
                {
                    new SelectableTextBlock
                    {
                        Text = message,
                        TextWrapping = TextWrapping.Wrap,
                        MaxWidth = 400
                    },
                    okButton
                }
            }
        };

        okButton.Click += (_, _) => dialog.Close();
        await dialog.ShowDialog(this);
    }

    private async void OpenImageButton_Click(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Image",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Images")
                {
                    Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif", "*.webp" }
                },
                new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
            }
        });

        if (files.Count > 0)
            LoadImage(files[0].TryGetLocalPath() ?? "");
    }

    private void ColorMatcherComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_currentImagePath))
            UpdatePreview();
    }

    private void TSPHelpButton_Click(object? sender, RoutedEventArgs e)
    {
        const string message =
            "TSP Solver Time Limit refers to how much time is alloted to the TSP solver.\n" +
            "TSP refers to the Travelling Sales Person problem, which is finding the optimal route among a set of points.\n" +
            "This is used to find the optimal path for the pen tool to take while drawing to minimize drawing time.\n\n" +
            "For larger images, the TSP solver can take longer to find an optimal route, its also possible it will never even find an optimal route if there is too many points.\n" +
            "For 64x64, 0.5s is generally fine, anything largest you should consider giving it more time.\n\n" +
            "This time is how long it is alloted PER colour, so if an image has 30 different colours used, 0.5s will take 15 seconds.\n" +
            "The TSP solve is not used always, a simpler \"snaking\" algorithm is used if its quicker, or if TSP didnt find anything in time, which it sometimes is, mostly for large continuous areas of colour.";

        _ = ShowMessageAsync("TSP Solver Time Limit", message);
    }

    private async void SaveTDLDButton_Click(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentImagePath)) return;

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save .TDLD",
            DefaultExtension = "tdld",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Tomodachi Life Drawer file") { Patterns = new[] { "*.tdld" } },
                new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
            }
        });

        var outputPath = file?.TryGetLocalPath();
        if (outputPath == null) return;

        var imagePath = _currentImagePath;
        var quantizer = ColorMatcherComboBox.SelectedItem!.ToString()!;
        var tspLimit = (float)(TSPTimeLimitUpDown.Value ?? 0.5m);

        if (sender is Button btn) btn.IsEnabled = false;
        AppendLog("Starting export...\r\n");

        await Task.Run(async () =>
        {
            var fileOutput = new FileControllerSink(outputPath);
            var drawer = new CanvasDrawer(fileOutput, AppendLog);
            drawer.ConnectAndConfirmController();
            await drawer.DrawImage(SKBitmap.Decode(imagePath), quantizer, tspLimit);
            fileOutput.Dispose();
        });

        if (sender is Button btn2) btn2.IsEnabled = true;
        AppendLog("Export complete.\r\n");
    }

    private async void ExportRP2040Button_Click(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentImagePath)) return;

        var imagePath = _currentImagePath;
        var quantizer = ColorMatcherComboBox.SelectedItem!.ToString()!;
        var tspLimit = (float)(TSPTimeLimitUpDown.Value ?? 0.5m);

        ExportRP2040Button.IsEnabled = false;
        TimeSpan totalTime = TimeSpan.MaxValue;

        await Task.Run(async () =>
        {
            string tempPath = Path.Combine(
                Path.GetTempPath(),
                $"rp2040output{System.Random.Shared.Next(1000000, 9999999)}.tdld");

            AppendLog($"Exporting to RP2040 flash ({Path.GetFileName(tempPath)})");
            var timingSink = new TimingSink();
            var drawer = new CanvasDrawer(timingSink, AppendLog);
            drawer.ConnectAndConfirmController();
            AppendLog("Starting to generate inputs...");
            await drawer.DrawImage(SKBitmap.Decode(imagePath), quantizer, tspLimit, false);
            AppendLog($"True complete overall time is: {timingSink.TotalTime.TotalSeconds}s");

            var fileSink = new FileControllerSink(tempPath);
            timingSink.ReplayTo(fileSink);
            fileSink.Dispose();

            var tdldBytes = File.ReadAllBytes(tempPath);
            var uf2Bytes = UF2Flasher.BuildTDLDUF2(tdldBytes);
            var drivePath = UF2Flasher.FindRP2040Drive();

            if (uf2Bytes != null && uf2Bytes.Length > 0 && drivePath != null)
            {
                File.WriteAllBytes(Path.Combine(drivePath, "tdld_image.uf2"), uf2Bytes);
                AppendLog("Wrote to RP2040 flash. Unplug the RP2040 and plug it into the switch without holding any button.");
            }

            if (File.Exists(tempPath)) File.Delete(tempPath);
            totalTime = timingSink.TotalTime;
        });

        ExportRP2040Button.IsEnabled = true;

        var estimateStr = $"{totalTime:h\\hm\\ms\\s}";
        DrawTimeLabel.Text = $"Draw Time Estimate: {estimateStr}";
    }

    private async void ExportUF2Button_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentImagePath)) return;

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save .UF2",
            DefaultExtension = "uf2",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("UF2 Firmware Image") { Patterns = new[] { "*.uf2" } },
                new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
            }
        });

        var outputPath = file?.TryGetLocalPath();
        if (outputPath == null) return;

        var imagePath = _currentImagePath;
        var quantizer = ColorMatcherComboBox.SelectedItem!.ToString()!;
        var tspLimit = (float)(TSPTimeLimitUpDown.Value ?? 0.5m);

        ExportUF2Button.IsEnabled = false;
        TimeSpan totalTime = TimeSpan.MaxValue;

        await Task.Run(async () =>
        {
            string tempPath = Path.Combine(
                Path.GetTempPath(),
                $"rp2040output{System.Random.Shared.Next(1000000, 9999999)}.tdld");

            AppendLog($"Exporting to UF2 ({Path.GetFileName(tempPath)})");
            var timingSink = new TimingSink();
            var drawer = new CanvasDrawer(timingSink, AppendLog);
            drawer.ConnectAndConfirmController();
            AppendLog("Starting to generate inputs...");
            await drawer.DrawImage(SKBitmap.Decode(imagePath), quantizer, tspLimit, false);
            AppendLog($"True complete overall time is: {timingSink.TotalTime.TotalSeconds}s");

            var fileSink = new FileControllerSink(tempPath);
            timingSink.ReplayTo(fileSink);
            fileSink.Dispose();

            var tdldBytes = File.ReadAllBytes(tempPath);
            var uf2Bytes = UF2Flasher.BuildTDLDUF2(tdldBytes);

            if (uf2Bytes != null && uf2Bytes.Length > 0)
            {
                File.WriteAllBytes(outputPath, uf2Bytes);
                AppendLog($"Saved UF2 to {outputPath}");
            }

            if (File.Exists(tempPath)) File.Delete(tempPath);
            totalTime = timingSink.TotalTime;
        });

        ExportUF2Button.IsEnabled = true;

        var estimateStr = $"{totalTime:h\\hm\\ms\\s}";
        DrawTimeLabel.Text = $"Draw Time Estimate: {estimateStr}";
    }

    private void FlashFirmwareButton_Click(object? sender, RoutedEventArgs e)
    {
        const string firmwareFile = "TomodachiDrawer.Firmware.uf2";
        var drivePath = UF2Flasher.FindRP2040Drive();

        if (!File.Exists(firmwareFile))
        {
            _ = ShowMessageAsync("Error flashing base firmware",
                "For some reason could not locate TomodachiDrawer.Firmware.uf2");
            return;
        }
        if (drivePath == null)
        {
            _ = ShowMessageAsync("Error", "RP2040 not detected. Connect it in BOOT mode first.");
            return;
        }

        File.Copy(firmwareFile, Path.Combine(drivePath, firmwareFile), overwrite: true);

        var timeout = System.DateTime.Now.AddSeconds(10);
        while (UF2Flasher.FindRP2040Drive() != null)
        {
            if (System.DateTime.Now > timeout)
            {
                _ = ShowMessageAsync("Error flashing base firmware",
                    "Wrote file but expected it to reset itself by now, maybe try doing it manually..?");
                return;
            }
            Thread.Sleep(500);
        }

        _ = ShowMessageAsync("", "Base firmware flashed! You can now use the standard output button to output your images to it!\nIf this is your first time, its likely flashing red. Simply hold BOOT and plug it back in, or hold BOOT and press reset if you have it.");
        AppendLog("Flashed base firmware to RP2040\r\n");
    }

    private void OutputExplanationButton_Click(object? sender, RoutedEventArgs e)
    {
        _ = ShowMessageAsync("",
            "Your RP2040-Zero (or similar) needs two things in its memory (it's flash):\r\n" +
            "- The code that reads the instructions to draw your image and pipe it to the switch\r\n" +
            "- The instructions to draw your image.\r\n\r\n\r\n" +
            "To connect your device for flashing, hold down the \"BOOT\" button and plug it in, or hold \"BOOT\" and press \"RESET\" while it is connected.\r\n\r\n" +
            "You only need to flash the code/\"firmware\" once.\r\n\r\n" +
            "You then flash the image data onto it for each image, without needing to reflash the firmware.\r\n\r\n" +
            "When you first install the firmware, it'll reset itself, flash yellow 3 times, and then flash red.\r\n" +
            "Flashing red is expected, as that means it cannot find the image data.\r\n" +
            "Reconnect it using the same \"BOOT\" button steps as described above, load your image, and hit \"Export to RP2040\".\r\n\r\n" +
            "Again, it will reboot, but now you can unplug it and plug it into your switch.\r\n\r\n" +
            "YOU MUST HAVE \"Pro Controller Wired Commmunication\" ENABLED.\r\n" +
            "Go to system settings -> Controllers & Accessories -> Pro Controller Wired Communication\r\n");
    }

    private void InGameSetupButton_Click(object? sender, RoutedEventArgs e)
    {
        _ = ShowMessageAsync("In Game Setup",
            "Setup in game is fairly straightforward.\r\n" +
            "- Navigate to the palette house\r\n" +
            "- Ensure you are on the \"advanced\" drawing UI\r\n" +
            "- Ensure your top colour is set to Black (it is by default)\r\n" +
            "- Set your cursor to the TOP LEFT of where you want the drawing to be.\r\n" +
            "- Ensure the full area of the canvas that will be drawn is on screen.\r\n\r\n" +
            "If the canvas is zoomed in, it will cause the cursor to desync as the canvas moves when the cursor gets on the edges. Zooming out fully avoids this.\r\n\r\n" +
            "If your image is 256x256, set it all the way in the top left. If your image is smaller, set your cursor to where you want the topleft most pixel of your drawing to be.");
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
        if (!e.DataTransfer.Contains(DataFormat.File)) return;
        var first = e.DataTransfer.TryGetFiles()?.FirstOrDefault();
        if (first != null)
            LoadImage(first.TryGetLocalPath() ?? "");
    }
}
