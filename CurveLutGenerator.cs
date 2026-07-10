using System.Runtime.InteropServices;

namespace MuchaCurve;

/// <summary>
/// みゅしゃかーぶデータから32x32x32の3D LUTバイト配列を生成する。
/// Direct2D の CreateLookupTable3D に渡すためのRGBA float32データ。
/// メモリレイアウト: B軸が最速変化、G軸が中間、R軸が最遅変化。
/// （D2D LookupTable3D エフェクトの軸慣例: X=Blue, Y=Green, Z=Red）
/// </summary>
public static class CurveLutGenerator
{
    public const int LutSize = 32;
    private const int FloatsPerTexel = 4; // RGBA

    private static readonly int[] CachedExtents = new int[] { LutSize, LutSize, LutSize };
    private static readonly int[] CachedStrides = new int[]
    {
        LutSize * FloatsPerTexel * sizeof(float),
        LutSize * LutSize * FloatsPerTexel * sizeof(float)
    };

    /// <summary>
    /// みゅしゃかーぶデータからLUTバイト配列を生成する。
    /// </summary>
    public static byte[] GenerateLut(CustomCurveData curveData)
    {
        int totalTexels = LutSize * LutSize * LutSize;
        // byte[] を直接確保し float ビューで書き込む (中間配列コピー不要)
        var result = new byte[totalTexels * FloatsPerTexel * sizeof(float)];
        Span<float> floatData = MemoryMarshal.Cast<byte, float>(result.AsSpan());

        // 各チャンネルの1D LUT を事前計算 (stackalloc — GC負荷ゼロ)
        Span<double> redLut = stackalloc double[LutSize];
        Span<double> greenLut = stackalloc double[LutSize];
        Span<double> blueLut = stackalloc double[LutSize];
        CurveInterpolation.BuildLookupTable(curveData.Red.Points, redLut);
        CurveInterpolation.BuildLookupTable(curveData.Green.Points, greenLut);
        CurveInterpolation.BuildLookupTable(curveData.Blue.Points, blueLut);

        bool masterIsIdentity = CustomCurveData.IsChannelIdentity(curveData.Master);
        int idx = 0;

        if (masterIsIdentity)
        {
            // マスターが恒等 → 輝度計算をスキップ (R/G/B のみ適用)
            for (int rIdx = 0; rIdx < LutSize; rIdx++)
            {
                float r = (float)redLut[rIdx];
                for (int gIdx = 0; gIdx < LutSize; gIdx++)
                {
                    float g = (float)greenLut[gIdx];
                    for (int bIdx = 0; bIdx < LutSize; bIdx++)
                    {
                        floatData[idx] = r;
                        floatData[idx + 1] = g;
                        floatData[idx + 2] = (float)blueLut[bIdx];
                        floatData[idx + 3] = 1.0f;
                        idx += FloatsPerTexel;
                    }
                }
            }
        }
        else
        {
            Span<double> masterLut = stackalloc double[LutSize];
            CurveInterpolation.BuildLookupTable(curveData.Master.Points, masterLut);

            for (int rIdx = 0; rIdx < LutSize; rIdx++)
            {
                double r0 = redLut[rIdx];
                for (int gIdx = 0; gIdx < LutSize; gIdx++)
                {
                    double g0 = greenLut[gIdx];
                    for (int bIdx = 0; bIdx < LutSize; bIdx++)
                    {
                        double b0 = blueLut[bIdx];
                        double lumaIn = 0.2126 * r0 + 0.7152 * g0 + 0.0722 * b0;
                        double r, g, b;

                        if (lumaIn > 1e-6)
                        {
                            double pos = lumaIn * (LutSize - 1);
                            int lo = (int)pos;
                            int hi = Math.Min(lo + 1, LutSize - 1);
                            double frac = pos - lo;
                            double lumaOut = masterLut[lo] + (masterLut[hi] - masterLut[lo]) * frac;
                            double ratio = lumaOut / lumaIn;
                            r = Math.Clamp(r0 * ratio, 0.0, 1.0);
                            g = Math.Clamp(g0 * ratio, 0.0, 1.0);
                            b = Math.Clamp(b0 * ratio, 0.0, 1.0);
                        }
                        else
                        {
                            r = g = b = masterLut[0];
                        }

                        floatData[idx] = (float)r;
                        floatData[idx + 1] = (float)g;
                        floatData[idx + 2] = (float)b;
                        floatData[idx + 3] = 1.0f;
                        idx += FloatsPerTexel;
                    }
                }
            }
        }

        return result;
    }

    public static int[] GetStrides() => CachedStrides;
    public static int[] GetExtents() => CachedExtents;
}
