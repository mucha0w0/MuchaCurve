using System.Runtime.InteropServices;

namespace MuchaCurve;

/// <summary>
/// Monotone Cubic Hermite Spline (Fritsch-Carlson法) による補間。
/// 単調性を保証し、オーバーシュートを抑制する。
/// </summary>
public static class CurveInterpolation
{
    /// <summary>
    /// 制御点リストから均等ステップのLUTテーブルを一括生成する。
    /// deltas/slopes を一度だけ計算し、前方走査で全ステップの補間値を求める。
    /// </summary>
    public static double[] BuildLookupTable(List<CurvePoint> points, int steps = 256)
    {
        var table = new double[steps];
        BuildLookupTable(points, table);
        return table;
    }

    /// <summary>
    /// 事前確保済みバッファに LUT を書き込む。GC 負荷ゼロ版。
    /// </summary>
    public static void BuildLookupTable(List<CurvePoint> points, Span<double> table)
    {
        BuildLookupTable(CollectionsMarshal.AsSpan(points), table);
    }

    /// <summary>
    /// ReadOnlySpan ベースの BuildLookupTable。List アロケーション不要。
    /// </summary>
    public static void BuildLookupTable(ReadOnlySpan<CurvePoint> points, Span<double> table)
    {
        int steps = table.Length;
        int n = points.Length;

        if (n == 0)
        {
            double inv = 1.0 / (steps - 1);
            for (int i = 0; i < steps; i++)
                table[i] = i * inv;
            return;
        }
        if (n == 1)
        {
            table.Fill(points[0].Y);
            return;
        }

        bool useStack = n <= 64;
        Span<double> leftSlopes = useStack ? stackalloc double[n] : new double[n];
        Span<double> rightSlopes = useStack ? stackalloc double[n] : new double[n];
        ComputeSlopes(points, leftSlopes, rightSlopes);

        double inv2 = 1.0 / (steps - 1);
        int seg = 0;
        for (int step = 0; step < steps; step++)
        {
            double x = step * inv2;
            table[step] = EvaluateWithSlopes(points, leftSlopes, rightSlopes, x, ref seg);
        }
    }

    /// <summary>
    /// Fritsch-Carlson 法で各制御点の接線傾きを計算する。
    /// Corner ポイントでは左右の傾きを分離し、角を作る。
    /// </summary>
    private static void ComputeSlopes(ReadOnlySpan<CurvePoint> points,
        Span<double> leftSlopes, Span<double> rightSlopes)
    {
        int n = points.Length;
        bool useStack = n <= 64;
        Span<double> deltas = useStack ? stackalloc double[n - 1] : new double[n - 1];
        Span<double> smooth = useStack ? stackalloc double[n] : new double[n];

        for (int i = 0; i < n - 1; i++)
        {
            double dx = points[i + 1].X - points[i].X;
            deltas[i] = dx > 1e-12 ? (points[i + 1].Y - points[i].Y) / dx : 0.0;
        }

        smooth[0] = deltas[0];
        smooth[n - 1] = deltas[n - 2];

        for (int i = 1; i < n - 1; i++)
            smooth[i] = (deltas[i - 1] + deltas[i]) * 0.5;

        // Fritsch-Carlson 単調性修正
        for (int i = 0; i < n - 1; i++)
        {
            if (Math.Abs(deltas[i]) < 1e-12)
            {
                smooth[i] = 0;
                smooth[i + 1] = 0;
            }
            else
            {
                double alpha = smooth[i] / deltas[i];
                double beta = smooth[i + 1] / deltas[i];
                double mag = alpha * alpha + beta * beta;
                if (mag > 9.0)
                {
                    double tau = 3.0 / Math.Sqrt(mag);
                    smooth[i] = tau * alpha * deltas[i];
                    smooth[i + 1] = tau * beta * deltas[i];
                }
            }
        }

        // Corner ポイントは隣接セグメントの傾きをそのまま使用（端点含む）
        for (int i = 0; i < n; i++)
        {
            if (points[i].Type == CurvePointType.Corner)
            {
                leftSlopes[i] = i > 0 ? deltas[i - 1] : deltas[0];
                rightSlopes[i] = i < n - 1 ? deltas[i] : deltas[n - 2];
            }
            else
            {
                leftSlopes[i] = smooth[i];
                rightSlopes[i] = smooth[i];
            }
        }
    }

    /// <summary>
    /// 事前計算済み left/right slopes と前方走査用 seg を使って補間値を返す。
    /// </summary>
    private static double EvaluateWithSlopes(
        ReadOnlySpan<CurvePoint> points,
        ReadOnlySpan<double> leftSlopes, ReadOnlySpan<double> rightSlopes,
        double x, ref int seg)
    {
        int n = points.Length;
        if (x <= points[0].X) return points[0].Y;
        if (x >= points[n - 1].X) return points[n - 1].Y;

        while (seg < n - 2 && points[seg + 1].X < x)
            seg++;

        double h = points[seg + 1].X - points[seg].X;
        double t = h > 1e-12 ? (x - points[seg].X) / h : 0.0;
        double t2 = t * t;
        double t3 = t2 * t;

        // rightSlopes[seg]: セグメント左端からの出発傾き
        // leftSlopes[seg+1]: セグメント右端への到着傾き
        return Math.Clamp(
            (2 * t3 - 3 * t2 + 1) * points[seg].Y
          + (t3 - 2 * t2 + t) * h * rightSlopes[seg]
          + (-2 * t3 + 3 * t2) * points[seg + 1].Y
          + (t3 - t2) * h * leftSlopes[seg + 1],
            0.0, 1.0);
    }

    /// <summary>
    /// 循環（ラッピング）補間用 LUT を生成する。Hue vs Hue カーブ用。
    /// X=0 と X=1 が同一点として扱われ、カーブが循環する。
    /// ポイント0個 → defaultY で埋め、1個 → そのYで埋める。
    /// 2個以上 → 末尾→先頭をラップするセンチネルを追加してスプライン補間。
    /// </summary>
    public static void BuildWrappingLookupTable(List<CurvePoint> points, Span<double> table, double defaultY = 0.5)
    {
        int steps = table.Length;
        int n = points.Count;

        if (n == 0)
        {
            table.Fill(defaultY);
            return;
        }
        if (n == 1)
        {
            table.Fill(points[0].Y);
            return;
        }

        // ラップ用配列: [末尾-1.0, ...元ポイント..., 先頭+1.0] — stackalloc でGC負荷ゼロ
        int wrappedLen = n + 2;
        bool useStack = wrappedLen <= 64;
        Span<CurvePoint> wrapped = useStack ? stackalloc CurvePoint[wrappedLen] : new CurvePoint[wrappedLen];

        ReadOnlySpan<CurvePoint> span = CollectionsMarshal.AsSpan(points);
        var last = span[n - 1];
        wrapped[0] = new CurvePoint(last.X - 1.0, last.Y, last.Type);
        span.CopyTo(wrapped.Slice(1));
        var first = span[0];
        wrapped[wrappedLen - 1] = new CurvePoint(first.X + 1.0, first.Y, first.Type);

        BuildLookupTable((ReadOnlySpan<CurvePoint>)wrapped, table);
    }
}
