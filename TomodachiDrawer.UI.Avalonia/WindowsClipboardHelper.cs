using System.Runtime.InteropServices;
using System.Runtime.Versioning;

using Avalonia;
using Avalonia.Media.Imaging;

namespace TomodachiDrawer.UI.Avalonia;

// FULL TRANSPARENCY
// This is a slop mitigation for DPI aware things on Windows.
// This is isolated here from the rest of the logic for the sake of the... "purity" of this program.
// Everything below was written by AI - DllImport stuff is *way* outside my comfort level.

// Writes CF_DIB (with explicit 96 DPI) and "PNG" to the Windows clipboard directly via P/Invoke.
// SetBitmapAsync omits biXPelsPerMeter/biYPelsPerMeter from the CF_DIB header, which causes
// DPI-aware apps (MS Paint) to scale the canvas down by the display scale factor.
// Writing a "PNG" format entry alongside CF_DIB lets apps that check for it (Photoshop) get the
// alpha channel, since CF_DIB BI_RGB has no alpha support.
[SupportedOSPlatform("windows")]
internal static class WindowsClipboardHelper
{
    private const uint CF_DIB = 8;
    private const uint GMEM_MOVEABLE = 0x0002;
    private const int PixelsPerMeter96Dpi = 3780; // 96 / 0.0254 ≈ 3780 px/m

    [DllImport("user32.dll")] private static extern bool OpenClipboard(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool CloseClipboard();
    [DllImport("user32.dll")] private static extern bool EmptyClipboard();
    [DllImport("user32.dll")] private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint RegisterClipboardFormatW(string lpszFormat);
    [DllImport("kernel32.dll")] private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);
    [DllImport("kernel32.dll")] private static extern IntPtr GlobalLock(IntPtr hMem);
    [DllImport("kernel32.dll")] private static extern bool GlobalUnlock(IntPtr hMem);
    [DllImport("kernel32.dll")] private static extern IntPtr GlobalFree(IntPtr hMem);

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression; // 0 = BI_RGB
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    public static void SetBitmap(Bitmap bitmap)
    {
        // PNG bytes — preserves alpha for apps that check the "PNG" clipboard format
        using var pngStream = new MemoryStream();
        bitmap.Save(pngStream);
        var pngBytes = pngStream.ToArray();

        // Raw pixel bytes for CF_DIB
        var pixelSize = bitmap.PixelSize;
        int width = pixelSize.Width;
        int height = pixelSize.Height;
        int bpp = bitmap.Format?.BitsPerPixel ?? 32;
        int stride = (bpp / 8) * width;
        var pixelBytes = new byte[stride * height];
        var pin = GCHandle.Alloc(pixelBytes, GCHandleType.Pinned);
        try
        {
            bitmap.CopyPixels(new PixelRect(0, 0, width, height), pin.AddrOfPinnedObject(), pixelBytes.Length, stride);
        }
        finally
        {
            pin.Free();
        }

        uint pngFormatId = RegisterClipboardFormatW("PNG");

        if (!OpenClipboard(IntPtr.Zero))
            return;

        try
        {
            EmptyClipboard();
            WriteDib(width, height, bpp, stride, pixelBytes);
            WritePng(pngBytes, pngFormatId);
        }
        finally
        {
            CloseClipboard();
        }
    }

    private static void WriteDib(int width, int height, int bpp, int stride, byte[] pixelBytes)
    {
        int headerSize = Marshal.SizeOf<BITMAPINFOHEADER>();
        var header = new BITMAPINFOHEADER
        {
            biSize = (uint)headerSize,
            biWidth = width,
            biHeight = -height, // negative = top-down DIB
            biPlanes = 1,
            biBitCount = (ushort)bpp,
            biCompression = 0,
            biSizeImage = (uint)pixelBytes.Length,
            biXPelsPerMeter = PixelsPerMeter96Dpi,
            biYPelsPerMeter = PixelsPerMeter96Dpi,
        };

        var hMem = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)(headerSize + pixelBytes.Length));
        if (hMem == IntPtr.Zero) return;

        var ptr = GlobalLock(hMem);
        if (ptr == IntPtr.Zero)
        {
            GlobalFree(hMem);
            return;
        }

        Marshal.StructureToPtr(header, ptr, false);
        Marshal.Copy(pixelBytes, 0, IntPtr.Add(ptr, headerSize), pixelBytes.Length);
        GlobalUnlock(hMem);

        if (SetClipboardData(CF_DIB, hMem) == IntPtr.Zero)
            GlobalFree(hMem);
    }

    private static void WritePng(byte[] pngBytes, uint formatId)
    {
        var hMem = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)pngBytes.Length);
        if (hMem == IntPtr.Zero) return;

        var ptr = GlobalLock(hMem);
        if (ptr == IntPtr.Zero)
        {
            GlobalFree(hMem);
            return;
        }

        Marshal.Copy(pngBytes, 0, ptr, pngBytes.Length);
        GlobalUnlock(hMem);

        if (SetClipboardData(formatId, hMem) == IntPtr.Zero)
            GlobalFree(hMem);
    }
}
