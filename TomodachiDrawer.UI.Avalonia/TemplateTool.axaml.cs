using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;

using SkiaSharp;

using TomodachiDrawer.Core.Extensions;
using TomodachiDrawer.Core.ImageProcessing;

namespace TomodachiDrawer.UI.Avalonia;

public partial class TemplateTool : Window
{
    private readonly TomodachiLifeMask _mask;
    private readonly Bitmap? _betterMask;

    private SKBitmap _currentPreview = new(256, 256); // this is just to shut up a warning :<

    // For preview
    public TemplateTool() : this(TomodachiLifeMask.BodyTopsLongH) { }

    public TemplateTool(TomodachiLifeMask mask)
    {
        _mask = mask;
        InitializeComponent();

        this.Title = "Template Tool - " + _mask.GetDescription();

        // Load the mask
        var skiaMask = ImageMasker.GetMask(_mask);
        if (skiaMask == null)
        {
            // Couldnt load the mask...
            this.Close(new TemplateToolResponse(false, true, null));
        }
        else
        {
            _currentPreview = skiaMask;

            var checker = GenerateCheckerboard(256, 256, cellSize: 8);
            CheckerboardPreview.Source = MainWindow.ToAvaloniaBitmap(checker);
            checker.Dispose();
            var betterMask = MakeBetterMask(skiaMask);
            _betterMask = MainWindow.ToAvaloniaBitmap(betterMask);
            TemplatePreview.Source = _betterMask;
            betterMask.Dispose();

            Opened += TemplateTool_Opened;
        }

    }

    private async Task SetClipboardBitmap(Bitmap bitmap)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard is { } clipboard)
            await clipboard.SetBitmapAsync(bitmap);
    }

    private async Task ShowMessageAsync(
        string title,
        string message
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
        await dialog.ShowDialog(this);
    }

    private async void TemplateTool_Opened(object? sender, EventArgs e)
    {
        if (TemplatePreview.Source is Bitmap bitmap)
            await SetClipboardBitmap(bitmap);
    }

    private async void CopyClipboardButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_betterMask != null)
        {
            await SetClipboardBitmap(_betterMask);
        }
    }

    private async void SaveTemplateButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_betterMask == null) return;
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Template",
            SuggestedFileName = $"{_mask}_template.png",
            DefaultExtension = "png",
            FileTypeChoices = [new FilePickerFileType("PNG Image") { Patterns = ["*.png"] }],
        });
        var path = file?.TryGetLocalPath();
        if (path != null)
            _betterMask.Save(path);
    }

    private async void OpenDrawingButton_Click(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Drawing",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("PNG Image") { Patterns = ["*.png"] }],
        });
        if (files.Count == 0) return;
        var path = files[0].TryGetLocalPath();
        if (path == null) return;

        using var bitmap = new Bitmap(path);
        var skiaBitmap = ToSKBitmap(bitmap);
        if (skiaBitmap.Width != 256 || skiaBitmap.Height != 256)
        {
            await ShowMessageAsync("Error", "The image must be 256x256. You can save the template with the Save Template To File button to use as a starting point.");
            return;
        }
        var masked = ImageMasker.MaskImage(skiaBitmap, ImageMasker.GetMask(_mask)!);
        _currentPreview.Dispose();
        _currentPreview = masked;
        TemplatePreview.Source = MainWindow.ToAvaloniaBitmap(masked);
        ConfirmButton.IsEnabled = true;
    }

    private static SKBitmap GenerateCheckerboard(int width, int height, int cellSize = 8)
    {
        var bmp = new SKBitmap(256, 256);
        var colA = new SKColor(42, 42, 42, 255);
        var colB = new SKColor(56, 56, 56, 255);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool even = ((x / cellSize) + (y / cellSize)) % 2 == 0;
                bmp.SetPixel(x, y, even ? colA : colB);
            }
        }
        return bmp;
    }

    private static SKBitmap MakeBetterMask(SKBitmap mask)
    {
        using var nodrawStream = AssetLoader.Open(new Uri("avares://TomodachiDrawer.UI.Avalonia/Assets/nodraw.png"));
        using var nodraw = SKBitmap.Decode(nodrawStream);
        var tinted = mask.Copy();
        for (int y = 0; y < 256; y++)
        {
            for (int x = 0; x < 256; x++)
            {
                var px = tinted.GetPixel(x, y);
                if (px.Alpha > 0)
                    tinted.SetPixel(x, y, nodraw.GetPixel(x, y));
            }
        }
        return tinted;
    }

    // This could probably be more efficent.
    public static SKBitmap ToSKBitmap(Bitmap avaloniaBitmap)
    {
        ArgumentNullException.ThrowIfNull(avaloniaBitmap);
        using var stream = new MemoryStream();
        avaloniaBitmap.Save(stream);
        stream.Seek(0, SeekOrigin.Begin);
        return SKBitmap.Decode(stream);
    }

    private async void PasteButton_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard is { } clipboard)
        {
            var bitmap = await clipboard.TryGetBitmapAsync();
            if (bitmap == null) // fallback to try and read a file off disk... If they have a path to a file on clipboard instead.
            {
                var path = await clipboard.TryGetFilesAsync();
                if (path?.Length > 0 && File.Exists(path[0].Path.LocalPath))
                {
                    bitmap = new Bitmap(path.First().Path.LocalPath);
                }
            }

            if (bitmap != null)
            {
                // Convert to SKBitmap and mask it off
                var skiaBitmap = ToSKBitmap(bitmap);
                bitmap.Dispose();
                if (skiaBitmap.Width != 256 || skiaBitmap.Height != 256)
                {
                    await ShowMessageAsync($"Error", "The image you had on your clipboard was not 256x256. You can copy the template again with the Copy Template To Clipboard button.");
                }
                else
                {
                    var masked = ImageMasker.MaskImage(skiaBitmap, ImageMasker.GetMask(_mask)!);
                    // update preview image — composite the masked result with the red mask overlay
                    _currentPreview.Dispose();
                    _currentPreview = masked;
                    TemplatePreview.Source = MainWindow.ToAvaloniaBitmap(masked);
                    ConfirmButton.IsEnabled = true;
                }
            }
            else
            {
                await ShowMessageAsync(
                    $"Error",
                    "Could not find an image on your clipboard. Make sure you copied it."
                );
            }
        }
    }

    private void ConfirmButton_Click(object? sender, RoutedEventArgs e)
    {
        var trimmedImage = _currentPreview.Copy();
        _currentPreview.Dispose();
        this.Close(new TemplateToolResponse(true, false, trimmedImage));
    }

    private void ExitButton_Click(object? sender, RoutedEventArgs e)
    {
        this.Close(new TemplateToolResponse(false, false, null));
    }
}