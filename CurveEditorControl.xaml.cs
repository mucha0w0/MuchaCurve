using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;

namespace MuchaCurve;

/// <summary>
/// みゅしゃかーぶのエディタ UI コントロール。
/// 左クリックでポイント追加(カーブ近傍のみ)または種類切替、ダブルクリックで削除。
/// </summary>
public partial class CurveEditorControl : UserControl, IPropertyEditorControl2
{
    // === IPropertyEditorControl / IPropertyEditorControl2 ===
    public event EventHandler? BeginEdit;
    public event EventHandler? EndEdit;
    public void SetFocus() => CurveCanvas.Focus();
    public void SetEditorInfo(IEditorInfo editorInfo) { }

    // === バインディング ===
    private ItemProperty[]? boundProperties;
    private CustomCurveData curveData = new();

    // === UI 状態 ===
    private CurveChannel activeChannel = CurveChannel.Master;
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
    private bool lastClickAddedPoint;
    private Point dragStartPos;
    private bool dragHitExistingPoint;
    private DateTime lastClickTime = DateTime.MinValue;
    private const double DoubleClickMaxMs = 250;

    // カーブ色 (Frozen済み)
    private static readonly SolidColorBrush MasterColor = Freeze(new(Color.FromRgb(0xCC, 0xCC, 0xCC)));
    private static readonly SolidColorBrush RedColor = Freeze(new(Color.FromRgb(0xE0, 0x60, 0x60)));
    private static readonly SolidColorBrush GreenColor = Freeze(new(Color.FromRgb(0x70, 0xC0, 0x70)));
    private static readonly SolidColorBrush BlueColor = Freeze(new(Color.FromRgb(0x60, 0x90, 0xE0)));
    private static readonly SolidColorBrush GridColor = Freeze(new(Color.FromRgb(0x33, 0x33, 0x33)));
    private static readonly SolidColorBrush DiagonalColor = Freeze(new(Color.FromRgb(0x50, 0x50, 0x50)));
    private static readonly SolidColorBrush PointFill = Freeze(new(Color.FromRgb(0x1E, 0x1E, 0x1E)));
    private static readonly SolidColorBrush InactiveMasterColor = Freeze(new(Color.FromArgb(0x50, 0xCC, 0xCC, 0xCC)));
    private static readonly SolidColorBrush InactiveRedColor = Freeze(new(Color.FromArgb(0x50, 0xE0, 0x60, 0x60)));
    private static readonly SolidColorBrush InactiveGreenColor = Freeze(new(Color.FromArgb(0x50, 0x70, 0xC0, 0x70)));
    private static readonly SolidColorBrush InactiveBlueColor = Freeze(new(Color.FromArgb(0x50, 0x60, 0x90, 0xE0)));

    // 描画用 Pen (再利用してGCを抑制)
    private static readonly Pen GridPen = FreezePen(new(GridColor, 0.5));
    private static readonly Pen DiagonalPen = FreezePen(new(DiagonalColor, 1.0) { DashStyle = new DashStyle(new double[] { 4, 2 }, 0) });
    private static readonly Pen InactiveMasterPen = FreezePen(new(InactiveMasterColor, 1.0));
    private static readonly Pen InactiveRedPen = FreezePen(new(InactiveRedColor, 1.0));
    private static readonly Pen InactiveGreenPen = FreezePen(new(InactiveGreenColor, 1.0));
    private static readonly Pen InactiveBluePen = FreezePen(new(InactiveBlueColor, 1.0));
    private static readonly Pen MasterPen = FreezePen(new(MasterColor, 2.0));
    private static readonly Pen RedPen = FreezePen(new(RedColor, 2.0));
    private static readonly Pen GreenPen = FreezePen(new(GreenColor, 2.0));
    private static readonly Pen BluePen = FreezePen(new(BlueColor, 2.0));
    private static readonly Pen MasterPointPen = FreezePen(new(MasterColor, 1.5));
    private static readonly Pen RedPointPen = FreezePen(new(RedColor, 1.5));
    private static readonly Pen GreenPointPen = FreezePen(new(GreenColor, 1.5));
    private static readonly Pen BluePointPen = FreezePen(new(BlueColor, 1.5));

    private static SolidColorBrush Freeze(SolidColorBrush b) { b.Freeze(); return b; }
    private static Pen FreezePen(Pen p) { p.Freeze(); return p; }

    public CurveEditorControl()
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
            curveData = CustomCurveData.Deserialize(json ?? string.Empty);
        }

        RedrawAll();
    }

    public void ClearBinding()
    {
        boundProperties = null;
    }

    private void CommitData()
    {
        if (boundProperties == null) return;
        var json = curveData.Serialize();
        foreach (var prop in boundProperties)
            prop.PropertyInfo.SetValue(prop.PropertyOwner, json);
    }

    // === チャンネル選択 ===

    private enum CurveChannel { Master, Red, Green, Blue }

    private CurveChannelData GetActiveChannelData() => activeChannel switch
    {
        CurveChannel.Red => curveData.Red,
        CurveChannel.Green => curveData.Green,
        CurveChannel.Blue => curveData.Blue,
        _ => curveData.Master
    };

    private Pen GetActivePen() => activeChannel switch
    {
        CurveChannel.Red => RedPen,
        CurveChannel.Green => GreenPen,
        CurveChannel.Blue => BluePen,
        _ => MasterPen
    };

    private Pen GetActivePointPen() => activeChannel switch
    {
        CurveChannel.Red => RedPointPen,
        CurveChannel.Green => GreenPointPen,
        CurveChannel.Blue => BluePointPen,
        _ => MasterPointPen
    };

    internal void OnChannelChanged(object sender, RoutedEventArgs e)
    {
        if (sender == TabMaster) activeChannel = CurveChannel.Master;
        else if (sender == TabRed) activeChannel = CurveChannel.Red;
        else if (sender == TabGreen) activeChannel = CurveChannel.Green;
        else if (sender == TabBlue) activeChannel = CurveChannel.Blue;
        RedrawAll();
    }

    internal void OnResetClicked(object sender, RoutedEventArgs e)
    {
        var ch = GetActiveChannelData();
        ch.Points = new List<CurvePoint> { new(0.0, 0.0), new(1.0, 1.0) };
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
        cachedCanvasH = Math.Max(CurveCanvas.ActualHeight - CurvePadding * 2, 1);
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
        var pos = e.GetPosition(CurveCanvas);

        // ダブルクリック → ポイント削除 (独自タイマーで判定)
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
            if (idx > 0 && idx < ch.Points.Count - 1)
            {
                // 中間ポイント → 削除
                BeginEdit?.Invoke(this, EventArgs.Empty);
                ch.Points.RemoveAt(idx);
                CommitData();
                EndEdit?.Invoke(this, EventArgs.Empty);
            }
            // 端点はシングルクリック (MouseUp) で種類切替するため、ここでは何もしない
            RedrawAll();
            e.Handled = true;
            return;
        }

        // シングルクリック処理
        lastClickAddedPoint = false;
        int hitIdx = HitTest(pos);

        if (hitIdx >= 0)
        {
            // 既存ポイント → ドラッグ開始（種類切替はMouseUpでクリック判定時に行う）
            BeginEdit?.Invoke(this, EventArgs.Empty);
            dragIndex = hitIdx;
            dragHitExistingPoint = true;
        }
        else if (DistanceToCurveSq(pos) <= CurveHitDistanceSq)
        {
            // カーブ線に近い空き領域 → ポイント追加してドラッグ開始
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
            // カーブ線から離れすぎ → 何もしない
            e.Handled = true;
            return;
        }

        isDragging = true;
        dragStartPos = pos;
        CurveCanvas.CaptureMouse();
        RedrawAll();
    }

    
    internal void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (isDragging)
        {
            var pos = e.GetPosition(CurveCanvas);
            var ch = GetActiveChannelData();

            // クリック判定: マウスがほぼ動いていなければ種類切替
            double moveDx = pos.X - dragStartPos.X;
            double moveDy = pos.Y - dragStartPos.Y;
            bool isClick = (moveDx * moveDx + moveDy * moveDy) < ClickMoveThreshold * ClickMoveThreshold;

            if (isClick && dragHitExistingPoint && dragIndex >= 0 && dragIndex < ch.Points.Count)
            {
                var pt = ch.Points[dragIndex];
                var newType = pt.Type == CurvePointType.Smooth
                    ? CurvePointType.Corner
                    : CurvePointType.Smooth;
                ch.Points[dragIndex] = new CurvePoint(pt.X, pt.Y, newType);
            }

            if (dragIndex > 0 && dragIndex < ch.Points.Count - 1)
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

        if (dragIndex == 0)
            nx = 0.0;
        else if (dragIndex == points.Count - 1)
            nx = 1.0;
        else
        {
            double minX = points[dragIndex - 1].X + 0.001;
            double maxX = points[dragIndex + 1].X - 0.001;
            if (minX > maxX) return;
            nx = Math.Clamp(nx, minX, maxX);
        }

        points[dragIndex] = new CurvePoint(nx, ny, points[dragIndex].Type);
        CommitData();
        RedrawAll();
    }

    internal void Canvas_SizeChanged(object sender, SizeChangedEventArgs e) => RedrawAll();

    // === 近接ポイント統合 ===

    private void MergeClosePoints(CurveChannelData ch, int idx)
    {
        if (idx <= 0 || idx >= ch.Points.Count - 1) return;

        var pt = ch.Points[idx];
        var ptC = NormToCanvas(pt.X, pt.Y);
        double threshold2 = MergePixelThreshold * MergePixelThreshold;

        // 右隣をチェック（終端は統合しない）
        if (idx + 1 < ch.Points.Count - 1)
        {
            var r = ch.Points[idx + 1];
            var rc = NormToCanvas(r.X, r.Y);
            double dx = rc.X - ptC.X, dy = rc.Y - ptC.Y;
            if (dx * dx + dy * dy < threshold2)
                ch.Points.RemoveAt(idx + 1);
        }

        // 左隣をチェック（始端は統合しない）
        if (idx - 1 > 0)
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
        var ch = GetActiveChannelData();
        if (ch.Points.Count < 2) return double.MaxValue;

        RefreshCanvasSize();
        Span<double> lut = stackalloc double[DistanceCheckResolution + 1];
        CurveInterpolation.BuildLookupTable(ch.Points, lut);

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

    // === 描画 (DrawingVisual 再利用 — 毎フレーム作り直さない) ===

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
            DrawGrid(dc);
            DrawDiagonal(dc);

            DrawInactiveIfNeeded(dc, lut, CurveChannel.Master, curveData.Master, InactiveMasterPen);
            DrawInactiveIfNeeded(dc, lut, CurveChannel.Red, curveData.Red, InactiveRedPen);
            DrawInactiveIfNeeded(dc, lut, CurveChannel.Green, curveData.Green, InactiveGreenPen);
            DrawInactiveIfNeeded(dc, lut, CurveChannel.Blue, curveData.Blue, InactiveBluePen);

            var activeData = GetActiveChannelData();
            var isEnabled = ChkEnabled?.IsChecked ?? true;
            if (activeData.Points.Count >= 2)
            {
                CurveInterpolation.BuildLookupTable(activeData.Points, lut);
                DrawCurveLine(dc, lut, GetActivePen(), isEnabled);
            }
            DrawControlPoints(dc, activeData, GetActivePointPen(), isEnabled);
        }

        if (!visualAddedToTree)
        {
            CurveCanvas.Children.Add(new VisualHost(cachedVisual));
            visualAddedToTree = true;
        }
    }

    private void DrawInactiveIfNeeded(DrawingContext dc, Span<double> lut,
        CurveChannel ch, CurveChannelData data, Pen pen)
    {
        if (ch == activeChannel || data.Points.Count < 2) return;
        if (CustomCurveData.IsChannelIdentity(data)) return;
        CurveInterpolation.BuildLookupTable(data.Points, lut);
        DrawCurveLine(dc, lut, pen);
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

    private void DrawDiagonal(DrawingContext dc)
    {
        dc.DrawLine(DiagonalPen, NormToCanvas(0, 0), NormToCanvas(1, 1));
    }

    private void DrawCurveLine(DrawingContext dc, ReadOnlySpan<double> lut, Pen pen)
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
        dc.DrawGeometry(null, pen, geometry);
    }

    private void DrawCurveLine(DrawingContext dc, ReadOnlySpan<double> lut, Pen pen, bool isEnabled)
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
        dc.DrawGeometry(null, isEnabled ? pen : InactiveMasterPen, geometry);
    }

    private void DrawControlPoints(DrawingContext dc, CurveChannelData channelData, Pen pen)
    {
        foreach (var pt in channelData.Points)
        {
            var cp = NormToCanvas(pt.X, pt.Y);
            if (pt.Type == CurvePointType.Corner)
            {
                double r = PointRadius + 1;
                dc.DrawGeometry(PointFill, pen, CreateDiamond(cp, r));
            }
            else
            {
                dc.DrawEllipse(PointFill, pen, cp, PointRadius, PointRadius);
            }
        }
    }

    private void DrawControlPoints(DrawingContext dc, CurveChannelData channelData, Pen pen, bool isEnabled)
    {
        foreach (var pt in channelData.Points)
        {
            var cp = NormToCanvas(pt.X, pt.Y);
            if (pt.Type == CurvePointType.Corner)
            {
                double r = PointRadius + 1;
                dc.DrawGeometry(PointFill, isEnabled ? pen : InactiveMasterPen, CreateDiamond(cp, r));
            }
            else
            {
                dc.DrawEllipse(PointFill, isEnabled ? pen : InactiveMasterPen, cp, PointRadius, PointRadius);
            }
        }
    }

    private static StreamGeometry CreateDiamond(Point center, double r)
    {
        var diamond = new StreamGeometry();
        using (var ctx = diamond.Open())
        {
            ctx.BeginFigure(new Point(center.X, center.Y - r), true, true);
            ctx.LineTo(new Point(center.X + r, center.Y), true, false);
            ctx.LineTo(new Point(center.X, center.Y + r), true, false);
            ctx.LineTo(new Point(center.X - r, center.Y), true, false);
        }
        diamond.Freeze();
        return diamond;
    }

    /// <summary>
    /// DrawingVisual を Canvas の子として追加するための軽量ホスト。
    /// </summary>
    private sealed class VisualHost : FrameworkElement
    {
        private readonly Visual visual;
        public VisualHost(Visual v) { visual = v; AddVisualChild(v); }
        protected override int VisualChildrenCount => 1;
        protected override Visual GetVisualChild(int index) => visual;
    }
}
