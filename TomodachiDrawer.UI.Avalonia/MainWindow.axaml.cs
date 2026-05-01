using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Button = Avalonia.Controls.Button; // pins away from Core.OutputSinks.Button enum
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
                    if (path != null)
                    {
                        RP2040StatusLabel.Text = $"RP2040 found: {path}";
                        RP2040StatusLabel.Foreground = Brushes.Green;
                        FlashFirmwareButton.IsEnabled = true;
                        ExportRP2040Button.IsEnabled = !string.IsNullOrEmpty(_currentImagePath);
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
        if (!File.Exists(path)) { AppendLog($"File not found: {path}"); return; }

        var img = SKBitmap.Decode(path);
        if (img == null) { AppendLog($"Failed to decode image: {path}"); return; }

        if (img.Width > 256 || img.Height > 256)
        {
            _ = ShowMessageAsync("Image too big", $"{Path.GetFileName(path)} is too big! Max 256×256.");
            return;
        }

        _currentImagePath = path;
        ImagePathBox.Text = path;
        UpdatePreview();
        AppendLog($"Loaded image: {Path.GetFileName(path)} ({img.Width}×{img.Height})");
    }

    private void UpdatePreview()
    {
        if (!File.Exists(_currentImagePath)) return;
        var quantizer = ColorMatcherComboBox.SelectedItem?.ToString();
        if (quantizer == null) return;

        var pal = new ColourPalette(new DummySink());
        var preview = pal.PreviewColourMapping(SKBitmap.Decode(_currentImagePath), quantizer);
        PreviewImage.Source = ToAvaloniaBitmap(preview);
        AppendLog($"Preview updated — {Path.GetFileName(_currentImagePath)} / {quantizer}");
    }

    private static Bitmap? ToAvaloniaBitmap(SKBitmap skBitmap)
    {
        using var image = SKImage.FromBitmap(skBitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = new MemoryStream(data.ToArray());
        return new Bitmap(stream);
    }

    // ── Logging ───────────────────────────────────────────────────────

    private void AppendLog(string msg)
    {
        Dispatcher.UIThread.Post(() =>
        {
            LogBox.Text = (LogBox.Text ?? "") + msg + "\n";
            LogBox.CaretIndex = LogBox.Text?.Length ?? 0;
        });
    }

    // ── Modal dialog ──────────────────────────────────────────────────

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

    // ── Button handlers ───────────────────────────────────────────────

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
        _ = ShowMessageAsync("TSP Solver Time Limit",
            "TSP (Travelling Salesman Problem) is used to find the optimal drawing path " +
            "to minimise the total time the pen spends moving.\n\n" +
            "For larger images the solver may need more time. For 64×64, 0.5 s is usually fine; " +
            "for larger images consider 1–2 s or more.\n\n" +
            "This limit is per colour — 30 colours at 0.5 s = 15 s total solve time.\n\n" +
            "A simpler \"snaking\" algorithm is used when it's faster, or when the TSP solver " +
            "doesn't find a solution in time.");
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
        AppendLog("Starting export…");

        await Task.Run(async () =>
        {
            var fileOutput = new FileControllerSink(outputPath);
            var drawer = new CanvasDrawer(fileOutput, AppendLog);
            drawer.ConnectAndConfirmController();
            await drawer.DrawImage(SKBitmap.Decode(imagePath), quantizer, tspLimit);
            fileOutput.Dispose();
        });

        if (sender is Button btn2) btn2.IsEnabled = true;
        AppendLog("Export complete.");
    }

    private async void ExportRP2040Button_Click(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentImagePath)) return;

        var imagePath = _currentImagePath;
        var quantizer = ColorMatcherComboBox.SelectedItem!.ToString()!;
        var tspLimit = (float)(TSPTimeLimitUpDown.Value ?? 0.5m);
        bool disableStamps = DebugDisableLargeStamps.IsChecked == true;

        ExportRP2040Button.IsEnabled = false;
        TimeSpan totalTime = TimeSpan.MaxValue;

        await Task.Run(async () =>
        {
            var tempPath = Path.Combine(
                Path.GetTempPath(),
                $"rp2040output{System.Random.Shared.Next(1000000, 9999999)}.tdld");

            AppendLog($"Generating inputs ({Path.GetFileName(tempPath)})…");
            var timingSink = new TimingSink();
            var drawer = new CanvasDrawer(timingSink, AppendLog);
            drawer.ConnectAndConfirmController();
            await drawer.DrawImage(SKBitmap.Decode(imagePath), quantizer, tspLimit, disableStamps);
            AppendLog($"Total draw time: {timingSink.TotalTime.TotalSeconds:F1} s");

            var fileSink = new FileControllerSink(tempPath);
            timingSink.ReplayTo(fileSink);
            fileSink.Dispose();

            var tdldBytes = File.ReadAllBytes(tempPath);
            var uf2Bytes = UF2Flasher.BuildTDLDUF2(tdldBytes);
            var drivePath = UF2Flasher.FindRP2040Drive();

            if (uf2Bytes is { Length: > 0 } && drivePath != null)
            {
                File.WriteAllBytes(Path.Combine(drivePath, "tdld_image.uf2"), uf2Bytes);
                AppendLog("Written to RP2040. Unplug and connect to the Switch.");
            }

            if (File.Exists(tempPath)) File.Delete(tempPath);
            totalTime = timingSink.TotalTime;
        });

        ExportRP2040Button.IsEnabled = true;
        DrawTimeLabel.Text = $"Draw Time Estimate: {totalTime:h\\hm\\ms\\s}";
    }

    private void FlashFirmwareButton_Click(object? sender, RoutedEventArgs e)
    {
        const string firmwareFile = "TomodachiDrawer.Firmware.uf2";
        var drivePath = UF2Flasher.FindRP2040Drive();

        if (!File.Exists(firmwareFile))
        {
            _ = ShowMessageAsync("Error", "Could not find TomodachiDrawer.Firmware.uf2 next to the executable.");
            return;
        }
        if (drivePath == null)
        {
            _ = ShowMessageAsync("Error", "RP2040 not detected. Connect it in BOOT mode first.");
            return;
        }

        File.Copy(firmwareFile, Path.Combine(drivePath, firmwareFile), overwrite: true);

        var timeout = System.DateTime.Now.AddSeconds(5);
        while (UF2Flasher.FindRP2040Drive() != null)
        {
            if (System.DateTime.Now > timeout)
            {
                _ = ShowMessageAsync("Warning",
                    "File written but the device hasn't reset yet — it may need to be reset manually.");
                return;
            }
            Thread.Sleep(500);
        }

        AppendLog("Base firmware flashed to RP2040.");
        _ = ShowMessageAsync("Done",
            "Base firmware flashed!\n\n" +
            "The device will reboot, flash yellow 3×, then red (no image data yet).\n" +
            "Hold BOOT, reconnect, load your image, and click Export To RP2040.");
    }

    private void OutputExplanationButton_Click(object? sender, RoutedEventArgs e)
    {
        _ = ShowMessageAsync("RP2040 Output — How It Works",
            "Your RP2040-Zero needs two things in its flash:\n" +
            "  • Firmware — the code that reads the drawing instructions and sends " +
            "controller inputs to the Switch\n" +
            "  • Image data — the instructions for your specific image\n\n" +
            "To enter flashing mode: hold BOOT and plug in (or hold BOOT and press RESET).\n\n" +
            "You only need to flash the firmware once. After that, just flash a new image " +
            "each time you want to draw something new.\n\n" +
            "⚠ REQUIRED: System Settings → Controllers & Accessories → " +
            "Pro Controller Wired Communication → ON");
    }

    private void InGameSetupButton_Click(object? sender, RoutedEventArgs e)
    {
        _ = ShowMessageAsync("In-Game Setup",
            "Before plugging in the RP2040:\n" +
            "  • Open Palette House and switch to the Advanced drawing UI\n" +
            "  • Ensure the top colour slot is Black (the default)\n" +
            "  • Move the cursor to the TOP-LEFT corner of where you want to draw\n" +
            "  • Zoom OUT fully — if the canvas scrolls as the cursor reaches the edge " +
            "it will desync\n\n" +
            "For a 256×256 image, place the cursor at the very top-left of the canvas.\n" +
            "For smaller images, place it at your desired top-left pixel.");
    }

    // ── Debug ─────────────────────────────────────────────────────────

    private void DebugColourLayersButton_Click(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentImagePath) || !File.Exists(_currentImagePath)) return;

        var cp = new ColourPalette(new DummySink());
        var quantized = cp.QuantizeImage(
            SKBitmap.Decode(_currentImagePath),
            ColorMatcherComboBox.SelectedItem?.ToString() ?? "Euclidean");
        var layers = cp.BuildFineLayers(quantized);
        _colourLayersDebug.Clear();

        bool findUniform = DebugFineTestCheckBox.IsChecked == true;
        bool showBigger = DebugShowBiggerCheckBox.IsChecked == true;

        if (findUniform)
        {
            var cd = new CanvasDrawer(new DummySink(), AppendLog);
            foreach (var l in layers) cd.DetectUniformAreas(l);
        }

        foreach (var layer in layers)
        {
            var bitmap = new SKBitmap(quantized.GetLength(0), quantized.GetLength(1));
            if (!showBigger)
            {
                foreach (var p in layer.FineDetailPoints)
                    bitmap.SetPixel(p.X, p.Y, new SKColor(layer.Colour.R, layer.Colour.G, layer.Colour.B));
            }
            else
            {
                foreach (var kv in layer.StampsBySize)
                {
                    int s = kv.Key;
                    foreach (var p in kv.Value)
                        for (int dx = -s / 2; dx <= s / 2; dx++)
                            for (int dy = -s / 2; dy <= s / 2; dy++)
                            {
                                int x = p.X + dx, y = p.Y + dy;
                                if (x >= 0 && x < bitmap.Width && y >= 0 && y < bitmap.Height)
                                    bitmap.SetPixel(x, y, new SKColor(layer.Colour.R, layer.Colour.G, layer.Colour.B));
                            }
                }
            }
            _colourLayersDebug[layer.Colour] = bitmap;
        }

        DebugColourComboBox.ItemsSource = _colourLayersDebug.Keys
            .Select(c => c.DisplayName)
            .ToList();

        AppendLog($"Built {_colourLayersDebug.Count} colour layers.");
    }

    private void DebugColourComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var idx = DebugColourComboBox.SelectedIndex;
        if (idx < 0 || _colourLayersDebug.Count == 0) return;
        var colour = _colourLayersDebug.Keys.ElementAt(idx);
        PreviewImage.Source = ToAvaloniaBitmap(_colourLayersDebug[colour]);
    }

    private async void BenchmarkButton_Click(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentImagePath)) return;

        var imagePath = _currentImagePath;
        var quantizer = ColorMatcherComboBox.SelectedItem!.ToString()!;
        var tspLimit = (float)(TSPTimeLimitUpDown.Value ?? 0.5m);
        bool disableStamps = DebugDisableLargeStamps.IsChecked == true;

        if (sender is Button btn) btn.IsEnabled = false;
        double seconds = 0;

        await Task.Run(async () =>
        {
            var timingSink = new TimingSink();
            var drawer = new CanvasDrawer(timingSink, AppendLog);
            drawer.ConnectAndConfirmController();
            await drawer.DrawImage(SKBitmap.Decode(imagePath), quantizer, tspLimit, disableStamps);
            AppendLog($"Benchmark total: {timingSink.TotalTime.TotalSeconds:F3} s");
            seconds = timingSink.TotalTime.TotalSeconds;
        });

        if (sender is Button btn2) btn2.IsEnabled = true;
        DebugBenchmarkOutput.Text = $"{seconds:F3} s";
    }

    // ── Drag & drop ───────────────────────────────────────────────────

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
