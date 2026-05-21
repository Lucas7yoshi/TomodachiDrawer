using SkiaSharp;

namespace TomodachiDrawer.UI.Avalonia
{
    public record TemplateToolResponse(bool Success, bool CouldNotLoad, SKBitmap? Result);
}
