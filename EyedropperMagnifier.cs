using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace MuchaCurve;

/// <summary>
/// スポイト用の拡大モニターウィンドウ。
/// マウスカーソル周辺をズーム表示し、右下に選択中のカラーを表示する。
/// </summary>
internal sealed class EyedropperMagnifier : IDisposable
{
    // 拡大領域: キャプチャするピクセル範囲 (奇数で中心ピクセルを含む)
    private const int CaptureSize = 15;
    // 拡大表示サイズ (px)
    private const int MagnifierSize = 120;
    // カラースウォッチのサイズ
    private const int SwatchSize = 20;
    // 枠線
    private const double BorderWidth = 1.0;
    private static readonly Color BorderColor = Color.FromRgb(0x88, 0x88, 0x88);
    // カーソルからのオフセット
    private const int OffsetX = 20;
    private const int OffsetY = 20;
    // 中央の十字線
    private static readonly Color CrosshairColor = Color.FromRgb(0xCC, 0xCC, 0xCC);

    private readonly Window magnifierWindow;
    private readonly Image magnifierImage;
    private readonly Rectangle colorSwatch;
    private readonly Border swatchBorder;
    private readonly DispatcherTimer updateTimer;
    private readonly WriteableBitmap bitmap;
    private readonly SolidColorBrush swatchBrush;
    private readonly byte[] pixelBuf;
    private IntPtr magnifierHwnd;
    private bool disposed;

    // 現在のピクセル色
    public Color CurrentColor { get; private set; }

    public EyedropperMagnifier()
    {
        bitmap = new WriteableBitmap(CaptureSize, CaptureSize, 96, 96, PixelFormats.Bgr32, null);
        pixelBuf = new byte[CaptureSize * 4 * CaptureSize];
        swatchBrush = new SolidColorBrush(Colors.Black);

        magnifierImage = new Image
        {
            Source = bitmap,
            Stretch = Stretch.Fill,
        };
        // ニアレストネイバー (ドット絵風に拡大)
        RenderOptions.SetBitmapScalingMode(magnifierImage, BitmapScalingMode.NearestNeighbor);

        colorSwatch = new Rectangle
        {
            Width = SwatchSize,
            Height = SwatchSize,
            Fill = swatchBrush,
        };

        swatchBorder = new Border
        {
            BorderBrush = new SolidColorBrush(BorderColor),
            BorderThickness = new Thickness(BorderWidth),
            Child = colorSwatch,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 4, 4),
        };

        // Grid は明示サイズで固定。Image は Stretch.Fill でこれを完全に充填。
        // 枠線はオーバーレイ Border として Grid 内に配置 (親子の DPI 端数不一致を排除)。
        var grid = new Grid
        {
            Width = MagnifierSize,
            Height = MagnifierSize,
        };
        grid.Children.Add(magnifierImage);
        grid.Children.Add(CreateCrosshair());
        grid.Children.Add(swatchBorder);
        grid.Children.Add(new Border
        {
            BorderBrush = new SolidColorBrush(BorderColor),
            BorderThickness = new Thickness(BorderWidth),
            IsHitTestVisible = false,
        });

        magnifierWindow = new Window
        {
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            Topmost = true,
            ShowInTaskbar = false,
            ShowActivated = false,
            IsHitTestVisible = false,
            UseLayoutRounding = true,
            SizeToContent = SizeToContent.WidthAndHeight,
            Content = grid,
        };

        updateTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(16), // ~60fps
        };
        updateTimer.Tick += OnTimerTick;
    }

    private static Canvas CreateCrosshair()
    {
        var canvas = new Canvas
        {
            Width = MagnifierSize,
            Height = MagnifierSize,
            IsHitTestVisible = false,
        };

        double pixelSize = (double)MagnifierSize / CaptureSize;
        double centerStart = (CaptureSize / 2) * pixelSize;
        double centerEnd = centerStart + pixelSize;
        var brush = new SolidColorBrush(CrosshairColor);
        brush.Freeze();
        var pen = new Pen(brush, 1.0);

        // 中心ピクセルの枠を描画
        var rect = new Rectangle
        {
            Width = pixelSize,
            Height = pixelSize,
            Stroke = brush,
            StrokeThickness = 1.0,
            Fill = Brushes.Transparent,
        };
        Canvas.SetLeft(rect, centerStart);
        Canvas.SetTop(rect, centerStart);
        canvas.Children.Add(rect);

        return canvas;
    }

    public void Show()
    {
        if (disposed) return;
        UpdateCapture();
        magnifierWindow.Show();
        magnifierHwnd = new WindowInteropHelper(magnifierWindow).Handle;
        updateTimer.Start();
    }

    public void Hide()
    {
        updateTimer.Stop();
        magnifierWindow.Hide();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        updateTimer.Stop();
        magnifierWindow.Close();
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        UpdateCapture();
    }

    private void UpdateCapture()
    {
        GetCursorPos(out var cursor);

        // カーソル周辺のスクリーンをキャプチャ
        int halfSize = CaptureSize / 2;
        int srcX = cursor.X - halfSize;
        int srcY = cursor.Y - halfSize;

        IntPtr hdcScreen = GetDC(IntPtr.Zero);
        IntPtr hdcMem = CreateCompatibleDC(hdcScreen);
        IntPtr hBitmap = CreateCompatibleBitmap(hdcScreen, CaptureSize, CaptureSize);
        IntPtr hOld = SelectObject(hdcMem, hBitmap);

        BitBlt(hdcMem, 0, 0, CaptureSize, CaptureSize, hdcScreen, srcX, srcY, SRCCOPY);

        // 中央ピクセルの色を取得
        uint centerPixel = GetPixelNative(hdcScreen, cursor.X, cursor.Y);
        if (centerPixel != 0xFFFFFFFF)
        {
            byte r = (byte)(centerPixel & 0xFF);
            byte g = (byte)((centerPixel >> 8) & 0xFF);
            byte b = (byte)((centerPixel >> 16) & 0xFF);
            CurrentColor = Color.FromRgb(r, g, b);
            swatchBrush.Color = CurrentColor;
        }

        ReleaseDC(IntPtr.Zero, hdcScreen);

        // GetDIBits で明示的に 32bpp top-down BGR フォーマットで取得
        bmiHeader.biWidth = CaptureSize;
        bmiHeader.biHeight = -CaptureSize; // 負値 = top-down
        GetDIBits(hdcMem, hBitmap, 0, (uint)CaptureSize, pixelBuf, ref bmiHeader, 0);

        bitmap.Lock();
        bitmap.WritePixels(new Int32Rect(0, 0, CaptureSize, CaptureSize), pixelBuf, CaptureSize * 4, 0);
        bitmap.Unlock();

        SelectObject(hdcMem, hOld);
        DeleteObject(hBitmap);
        DeleteDC(hdcMem);

        // Win32 で直接物理ピクセル座標で配置 (WPFのDPI座標変換をバイパス)
        if (magnifierHwnd != IntPtr.Zero)
        {
            SetWindowPos(magnifierHwnd, IntPtr.Zero,
                cursor.X + OffsetX, cursor.Y + OffsetY, 0, 0,
                SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
        }
    }

    // === Win32 P/Invoke ===

    private const uint SRCCOPY = 0x00CC0020;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;

    // GetDIBits 用ヘッダー (フィールドに保持して再利用)
    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    private BITMAPINFOHEADER bmiHeader = new()
    {
        biSize = 40,
        biPlanes = 1,
        biBitCount = 32,
        biCompression = 0, // BI_RGB
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

    [DllImport("gdi32.dll", EntryPoint = "GetPixel")]
    private static extern uint GetPixelNative(IntPtr hdc, int x, int y);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int cx, int cy);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hgdiobj);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr hdcDest, int x, int y, int cx, int cy,
        IntPtr hdcSrc, int x1, int y1, uint rop);

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, uint start, uint cLines,
        byte[] lpvBits, ref BITMAPINFOHEADER lpbmi, uint usage);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int x, int y, int cx, int cy, uint uFlags);
}
