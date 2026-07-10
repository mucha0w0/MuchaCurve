using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;

namespace MuchaCurve;

/// <summary>
/// Hue vs Luma エディタ UI コントロール。
/// X軸=入力色相(0°~360°)、Y軸=輝度オフセット(下=暗く、中央=変更なし、上=明るく)。
/// 虹色のグラデーションバーで色相軸を表示する。
/// </summary>
public partial class HueVsLumaEditorControl : UserControl, IPropertyEditorControl2
{
    // === IPropertyEditorControl / IPropertyEditorControl2 ===
    public event EventHandler? BeginEdit;
    public event EventHandler? EndEdit;
    public void SetFocus() => CurveCanvas.Focus();
    public void SetEditorInfo(IEditorInfo editorInfo) { }

    // === バインディング ===
    private ItemProperty[]? boundProperties;
    private HueVsLumaData lumaData = new();

    // === UI 状態 ===
    private int dragIndex = -1;
    private bool isDragging;
    private const double PointRadius = 5.0;
    private const double HitRadius = 8.0;
    private const double MergePixelThreshold = 6.0;
    private const double CurveHitDistance = 50.0;
    private const double CurveHitDistanceSq = CurveHitDistance * CurveHitDistance;
    private const double ClickMoveThreshold = 4.0;
    private const int CurveResolution = 200;
    private const int DistanceCheckResolution = 50;
    private const double CurvePadding = 8.0;
    private const double RainbowBarHeight = 6.0;
    private bool lastClickAddedPoint;
    private Point dragStartPos;
    private bool dragHitExistingPoint;
    private DateTime lastClickTime = DateTime.MinValue;
    private const double DoubleClickMaxMs = 250;

    // === スポイト ===
    private bool isEyedropperActive;
    private EyedropperMagnifier? magnifier;
    private const double EyedropperAnchorOffset = 0.04; // ≈±14.4°

    // カーブ色 (輝度テーマ: ライム系)
    private static readonly SolidColorBrush CurveColor = Freeze(new(Color.FromRgb(0x80, 0xE0, 0x40)));
    private static readonly SolidColorBrush GridColor = Freeze(new(Color.FromRgb(0x33, 0x33, 0x33)));
    private static readonly SolidColorBrush CenterLineColor = Freeze(new(Color.FromRgb(0x50, 0x50, 0x50)));
    private static readonly SolidColorBrush PointFill = Freeze(new(Color.FromRgb(0x1E, 0x1E, 0x1E)));

    private static readonly Pen GridPen = FreezePen(new(GridColor, 0.5));
    private static readonly Pen CenterPen = FreezePen(new(CenterLineColor, 1.0) { DashStyle = new DashStyle(new double[] { 4, 2 }, 0) });
    private static readonly Pen CurvePen = FreezePen(new(CurveColor, 2.0));
    private static readonly Pen CurvePointPen = FreezePen(new(CurveColor, 1.5));

    // 虹色グラデーション (Hue 0°→360°)
    private static readonly LinearGradientBrush RainbowBrush = CreateRainbowBrush();

    private static SolidColorBrush Freeze(SolidColorBrush b) { b.Freeze(); return b; }
    private static Pen FreezePen(Pen p) { p.Freeze(); return p; }

    private static LinearGradientBrush CreateRainbowBrush()
    {
        var stops = new GradientStopCollection
        {
            new(Color.FromRgb(0xFF, 0x00, 0x00), 0.0 / 6.0),   // 0°   Red
            new(Color.FromRgb(0xFF, 0xFF, 0x00), 1.0 / 6.0),   // 60°  Yellow
            new(Color.FromRgb(0x00, 0xFF, 0x00), 2.0 / 6.0),   // 120° Green
            new(Color.FromRgb(0x00, 0xFF, 0xFF), 3.0 / 6.0),   // 180° Cyan
            new(Color.FromRgb(0x00, 0x00, 0xFF), 4.0 / 6.0),   // 240° Blue
            new(Color.FromRgb(0xFF, 0x00, 0xFF), 5.0 / 6.0),   // 300° Magenta
            new(Color.FromRgb(0xFF, 0x00, 0x00), 6.0 / 6.0),   // 360° Red
        };
        stops.Freeze();
        var brush = new LinearGradientBrush(stops, new Point(0, 0), new Point(1, 0));
        brush.Freeze();
        return brush;
    }

    public HueVsLumaEditorControl()
    {
        InitializeComponent();
    }

    // === プロパティバインド ===

    public void SetBinding(ItemProperty[] itemProperties)
    {
        ClearBinding();
        boundProperties = itemProperties;

        if (boundProperties.Length > 0)
        {
            var prop = boundProperties[0];
            var json = prop.PropertyInfo.GetValue(prop.PropertyOwner) as string;
            lumaData = HueVsLumaData.Deserialize(json ?? string.Empty);
        }

        RedrawAll();
    }

    public void ClearBinding()
    {
        if (isEyedropperActive) CancelEyedropper();
        boundProperties = null;
    }

    private void CommitData()
    {
        if (boundProperties == null) return;
        var json = lumaData.Serialize();
        foreach (var prop in boundProperties)
            prop.PropertyInfo.SetValue(prop.PropertyOwner, json);
    }

    private CurveChannelData GetActiveChannelData() => lumaData.Luma;

    internal void OnResetClicked(object sender, RoutedEventArgs e)
    {
        lumaData.Luma.Points = new List<CurvePoint>();
        BeginEdit?.Invoke(this, EventArgs.Empty);
        CommitData();
        EndEdit?.Invoke(this, EventArgs.Empty);
        RedrawAll();
    }

    // === 座標変換 ===

    private double cachedCanvasW = 1, cachedCanvasH = 1;

    private void RefreshCanvasSize()
    {
        cachedCanvasW = Math.Max(CurveCanvas.ActualWidth - CurvePadding * 2, 1);
        cachedCanvasH = Math.Max(CurveCanvas.ActualHeight - CurvePadding * 2 - RainbowBarHeight, 1);
    }

    private Point NormToCanvas(double x, double y)
        => new(CurvePadding + x * cachedCanvasW, CurvePadding + (1.0 - y) * cachedCanvasH);

    private (double x, double y) CanvasToNorm(Point pt)
    {
        double x = (pt.X - CurvePadding) / cachedCanvasW;
        double y = 1.0 - (pt.Y - CurvePadding) / cachedCanvasH;
        return (Math.Clamp(x, 0, 1), Math.Clamp(y, 0, 1));
    }

    // === マウスイベント ===

    internal void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (isEyedropperActive)
        {
            PickScreenColor();
            CancelEyedropper();
            e.Handled = true;
        }

        var pos = e.GetPosition(CurveCanvas);

        var now = DateTime.UtcNow;
        bool isDoubleClick = (now - lastClickTime).TotalMilliseconds < DoubleClickMaxMs;
        lastClickTime = now;
        if (isDoubleClick && !lastClickAddedPoint)
        {
            if (isDragging)
            {
                isDragging = false;
                dragIndex = -1;
                CurveCanvas.ReleaseMouseCapture();
            }

            int idx = HitTest(pos);
            var ch = GetActiveChannelData();
            if (idx >= 0)
            {
                BeginEdit?.Invoke(this, EventArgs.Empty);
                ch.Points.RemoveAt(idx);
                CommitData();
                EndEdit?.Invoke(this, EventArgs.Empty);
                RedrawAll();
                e.Handled = true;
                return;
            }

            lastClickAddedPoint = false;
            int hitIdx = HitTest(pos);
            if (hitIdx >= 0)
            {
                BeginEdit?.Invoke(this, EventArgs.Empty);
                dragIndex = hitIdx;
                dragHitExistingPoint = true;
            }
            else if (DistanceToCurveSq(pos) <= CurveHitDistanceSq)
            {
                BeginEdit?.Invoke(this, EventArgs.Empty);
                var (nx, ny) = CanvasToNorm(pos);
                int insertIdx = 0;
                for (int i = 0; i < ch.Points.Count; i++)
                {
                    if (ch.Points[i].X < nx) insertIdx = i + 1;
                    else break;
                }
                ch.Points.Insert(insertIdx, new CurvePoint(nx, ny));
                CommitData();
                dragIndex = insertIdx;
                lastClickAddedPoint = true;
            }
            else
            {
                e.Handled = true;
                return;
            }

            isDragging = true;
            dragStartPos = pos;
            CurveCanvas.CaptureMouse();
            RedrawAll();
            e.Handled = true;
            return;
        }

        lastClickAddedPoint = false;
        int hitIdx = HitTest(pos);

        if (hitIdx >= 0)
        {
            BeginEdit?.Invoke(this, EventArgs.Empty);
            dragIndex = hitIdx;
            dragHitExistingPoint = true;
        }
        else if (DistanceToCurveSq(pos) <= CurveHitDistanceSq)
        {
            BeginEdit?.Invoke(this, EventArgs.Empty);
            var (nx, ny) = CanvasToNorm(pos);
            var ch = GetActiveChannelData();
            int insertIdx = 0;
            for (int i = 0; i < ch.Points.Count; i++)
            {
                if (ch.Points[i].X < nx) insertIdx = i + 1;
                else break;
            }
            ch.Points.Insert(insertIdx, new CurvePoint(nx, ny));
            CommitData();
            dragIndex = insertIdx;
            lastClickAddedPoint = true;
        }
        else
        {
            e.Handled = true;
            return;
        }

        isDragging = true;
        dragStartPos = pos;
        CurveCanvas.CaptureMouse();
        RedrawAll();
        e.Handled = true;
    }

    internal void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (isDragging)
        {
            var pos = e.GetPosition(CurveCanvas);
            var ch = GetActiveChannelData();

            double moveDx = pos.X - dragStartPos.X;
            double moveDy = pos.Y - dragStartPos.Y;
            bool isClick = (moveDx * moveDx + moveDy * moveDy) < ClickMoveThreshold * ClickMoveThreshold;

            if (isClick && dragHitExistingPoint && dragIndex >= 0 && dragIndex < ch.Points.Count)
            {
                var pt = ch.Points[dragIndex];
                var newType = pt.Type == CurvePointType.Smooth
                    ? CurvePointType.Anchor
                    : CurvePointType.Smooth;
                // Anchor → Y=0.5 (変更なし) に固定
                double newY = newType == CurvePointType.Anchor ? 0.5 : pt.Y;
                ch.Points[dragIndex] = new CurvePoint(pt.X, newY, newType);
            }

            if (dragIndex >= 0 && dragIndex < ch.Points.Count)
                MergeClosePoints(ch, dragIndex);
            CommitData();
            isDragging = false;
            dragIndex = -1;
            dragHitExistingPoint = false;
            CurveCanvas.ReleaseMouseCapture();
            EndEdit?.Invoke(this, EventArgs.Empty);
            RedrawAll();
            e.Handled = true;
        }
    }

    internal void Canvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!isDragging || dragIndex < 0) return;

        var pos = e.GetPosition(CurveCanvas);
        var (nx, ny) = CanvasToNorm(pos);

        var ch = GetActiveChannelData();
        var points = ch.Points;

        // Anchor ポイントは Y=0.5 (変更なし) に固定
        if (points[dragIndex].Type == CurvePointType.Anchor)
            ny = 0.5;

        points[dragIndex] = new CurvePoint(nx, ny, points[dragIndex].Type);

        // X座標でソートし、ドラッグ中のインデックスを追跡
        while (dragIndex > 0 && points[dragIndex].X < points[dragIndex - 1].X)
        {
            (points[dragIndex], points[dragIndex - 1]) = (points[dragIndex - 1], points[dragIndex]);
            dragIndex--;
        }
        while (dragIndex < points.Count - 1 && points[dragIndex].X > points[dragIndex + 1].X)
        {
            (points[dragIndex], points[dragIndex + 1]) = (points[dragIndex + 1], points[dragIndex]);
            dragIndex++;
        }

        CommitData();
        RedrawAll();
    }

    internal void Canvas_SizeChanged(object sender, SizeChangedEventArgs e) => RedrawAll();

    // === 近接ポイント統合 ===

    private void MergeClosePoints(CurveChannelData ch, int idx)
    {
        if (idx < 0 || idx >= ch.Points.Count) return;

        var pt = ch.Points[idx];
        var ptC = NormToCanvas(pt.X, pt.Y);
        double threshold2 = MergePixelThreshold * MergePixelThreshold;

        if (idx + 1 < ch.Points.Count)
        {
            var r = ch.Points[idx + 1];
            var rc = NormToCanvas(r.X, r.Y);
            double dx = rc.X - ptC.X, dy = rc.Y - ptC.Y;
            if (dx * dx + dy * dy < threshold2)
                ch.Points.RemoveAt(idx + 1);
        }

        if (idx - 1 >= 0)
        {
            var l = ch.Points[idx - 1];
            var lc = NormToCanvas(l.X, l.Y);
            double dx = ptC.X - lc.X, dy = ptC.Y - lc.Y;
            if (dx * dx + dy * dy < threshold2)
                ch.Points.RemoveAt(idx - 1);
        }
    }

    // === カーブ線との距離計算 ===

    private double DistanceToCurveSq(Point canvasPos)
    {
        RefreshCanvasSize();
        Span<double> lut = stackalloc double[DistanceCheckResolution + 1];
        CurveInterpolation.BuildWrappingLookupTable(GetActiveChannelData().Points, lut);

        double bestDist2 = double.MaxValue;
        double invRes = 1.0 / DistanceCheckResolution;

        for (int i = 0; i <= DistanceCheckResolution; i++)
        {
            var cp = NormToCanvas(i * invRes, lut[i]);
            double dx = canvasPos.X - cp.X;
            double dy = canvasPos.Y - cp.Y;
            double d2 = dx * dx + dy * dy;
            if (d2 < bestDist2) bestDist2 = d2;
        }

        return bestDist2;
    }

    // === ヒットテスト ===

    private int HitTest(Point canvasPos)
    {
        var ch = GetActiveChannelData();
        double bestDist2 = HitRadius * HitRadius;
        int bestIdx = -1;

        for (int i = 0; i < ch.Points.Count; i++)
        {
            var cp = NormToCanvas(ch.Points[i].X, ch.Points[i].Y);
            double dx = canvasPos.X - cp.X;
            double dy = canvasPos.Y - cp.Y;
            double dist2 = dx * dx + dy * dy;
            if (dist2 < bestDist2)
            {
                bestDist2 = dist2;
                bestIdx = i;
            }
        }

        return bestIdx;
    }

    // === 描画 ===

    private DrawingVisual? cachedVisual;
    private bool visualAddedToTree;

    private void RedrawAll()
    {
        if (CurveCanvas == null) return;
        if (CurveCanvas.ActualWidth < 1 || CurveCanvas.ActualHeight < 1) return;
        RefreshCanvasSize();

        cachedVisual ??= new DrawingVisual();

        Span<double> lut = stackalloc double[CurveResolution + 1];
        using (var dc = cachedVisual.RenderOpen())
        {
            DrawRainbowBar(dc);
            DrawGrid(dc);
            DrawCenterLine(dc);

            var activeData = GetActiveChannelData();
            CurveInterpolation.BuildWrappingLookupTable(activeData.Points, lut);
            var isEnabled = ChkEnabled?.IsChecked ?? true;
            DrawCurveLine(dc, lut, isEnabled);
            DrawControlPoints(dc, activeData, isEnabled);
        }

        if (!visualAddedToTree)
        {
            CurveCanvas.Children.Add(new VisualHost(cachedVisual));
            visualAddedToTree = true;
        }
    }

    private void DrawRainbowBar(DrawingContext dc)
    {
        var left = NormToCanvas(0, 0);
        var right = NormToCanvas(1, 0);
        double barTop = left.Y + 2;
        dc.DrawRectangle(RainbowBrush, null,
            new Rect(left.X, barTop, right.X - left.X, RainbowBarHeight));
    }

    private void DrawGrid(DrawingContext dc)
    {
        for (int i = 1; i <= 3; i++)
        {
            double t = i / 4.0;
            var v0 = NormToCanvas(t, 0);
            var v1 = NormToCanvas(t, 1);
            dc.DrawLine(GridPen, v0, v1);
            var h0 = NormToCanvas(0, t);
            var h1 = NormToCanvas(1, t);
            dc.DrawLine(GridPen, h0, h1);
        }
    }

    private void DrawCenterLine(DrawingContext dc)
    {
        // Y=0.5 (変更なし) のセンターライン
        dc.DrawLine(CenterPen, NormToCanvas(0, 0.5), NormToCanvas(1, 0.5));
    }

    private void DrawCurveLine(DrawingContext dc, ReadOnlySpan<double> lut)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            double invRes = 1.0 / CurveResolution;
            ctx.BeginFigure(NormToCanvas(0, lut[0]), false, false);
            for (int i = 1; i <= CurveResolution; i++)
                ctx.LineTo(NormToCanvas(i * invRes, lut[i]), true, false);
        }
        geometry.Freeze();
        dc.DrawGeometry(null, CurvePen, geometry);
    }

    private void DrawCurveLine(DrawingContext dc, ReadOnlySpan<double> lut, bool isEnabled)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            double invRes = 1.0 / CurveResolution;
            ctx.BeginFigure(NormToCanvas(0, lut[0]), false, false);
            for (int i = 1; i <= CurveResolution; i++)
                ctx.LineTo(NormToCanvas(i * invRes, lut[i]), true, false);
        }
        geometry.Freeze();
        dc.DrawGeometry(null, isEnabled ? CurvePen : new Pen(GridColor, 2.0), geometry);
    }

    private void DrawControlPoints(DrawingContext dc, CurveChannelData channelData)
    {
        foreach (var pt in channelData.Points)
        {
            var cp = NormToCanvas(pt.X, pt.Y);
            if (pt.Type == CurvePointType.Anchor)
            {
                dc.DrawEllipse(CurveColor, CurvePointPen, cp, PointRadius, PointRadius);
            }
            else
            {
                dc.DrawEllipse(PointFill, CurvePointPen, cp, PointRadius, PointRadius);
            }
        }
    }

    private void DrawControlPoints(DrawingContext dc, CurveChannelData channelData, bool isEnabled)
    {
        foreach (var pt in channelData.Points)
        {
            var cp = NormToCanvas(pt.X, pt.Y);
            if (pt.Type == CurvePointType.Anchor)
            {
                dc.DrawEllipse(isEnabled ? CurveColor : GridColor, CurvePointPen, cp, PointRadius, PointRadius);
            }
            else
            {
                dc.DrawEllipse(PointFill, CurvePointPen, cp, PointRadius, PointRadius);
            }
        }
    }

    private sealed class VisualHost : FrameworkElement
    {
        private readonly Visual visual;
        public VisualHost(Visual v) { visual = v; AddVisualChild(v); }
        protected override int VisualChildrenCount => 1;
        protected override Visual GetVisualChild(int index) => visual;
    }

    // === スポイト機能 ===

    internal void OnEyedropperClicked(object sender, RoutedEventArgs e)
    {
        if (isEyedropperActive)
        {
            CancelEyedropper();
            return;
        }
        isEyedropperActive = true;
        Mouse.Capture(CurveCanvas);
        magnifier = new EyedropperMagnifier();
        magnifier.Show();
    }

    private void CancelEyedropper()
    {
        isEyedropperActive = false;
        CurveCanvas.ReleaseMouseCapture();
        magnifier?.Dispose();
        magnifier = null;
    }

    private void PickScreenColor()
    {
        var color = magnifier?.CurrentColor ?? Colors.Black;

        double r = color.R / 255.0;
        double g = color.G / 255.0;
        double b = color.B / 255.0;

        HueVsHueLutGenerator.RgbToHsv(r, g, b, out double h, out double s, out _);

        if (s < 0.05) return;

        AddHuePoints(h);
    }

    private void AddHuePoints(double hue)
    {
        var ch = GetActiveChannelData();

        double left = hue - EyedropperAnchorOffset;
        double right = hue + EyedropperAnchorOffset;
        if (left < 0) left += 1.0;
        if (right >= 1.0) right -= 1.0;

        BeginEdit?.Invoke(this, EventArgs.Empty);

        ch.Points.Add(new CurvePoint(left, 0.5, CurvePointType.Anchor));
        ch.Points.Add(new CurvePoint(hue, 0.5, CurvePointType.Smooth));
        ch.Points.Add(new CurvePoint(right, 0.5, CurvePointType.Anchor));
        ch.Points.Sort((p1, p2) => p1.X.CompareTo(p2.X));

        CommitData();
        EndEdit?.Invoke(this, EventArgs.Empty);
        RedrawAll();
    }

    internal void Canvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (isEyedropperActive)
        {
            CancelEyedropper();
            e.Handled = true;
        }
    }

    internal void Canvas_LostMouseCapture(object sender, MouseEventArgs e)
    {
        if (isEyedropperActive)
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (isEyedropperActive)
                    Mouse.Capture(CurveCanvas);
            });
        }
    }
}
