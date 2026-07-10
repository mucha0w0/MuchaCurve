using System.Runtime.InteropServices;

namespace MuchaCurve;

/// <summary>
/// Hue vs Hue カーブデータから32x32x32の3D LUTバイト配列を生成する。
/// 各テクセルについて RGB→HSV→色相シフト→HSV→RGB の変換を行う。
/// 無彩色保護: 低彩度・低/高輝度領域では色相シフトを滑らかにフェードアウトし、
/// ノイズや偽色の発生を防ぐ (DaVinci Resolve準拠のアルゴリズム)。
/// </summary>
public static class HueVsHueLutGenerator
{
    // 無彩色保護パラメータ
    // 彩度がこの値以下で色相シフトが完全にゼロになる
    private const double SatLow = 0.05;
    // 彩度がこの値以上で色相シフトが100%適用される
    private const double SatHigh = 0.15;
    // 明度がこの値以下で色相シフトが完全にゼロになる (黒保護)
    private const double ValLow = 0.02;
    // 明度がこの値以上で色相シフトが100%適用される
    private const double ValHigh = 0.10;

    /// <summary>
    /// HueVsHueDataからLUTバイト配列を生成する。
    /// </summary>
    public static byte[] GenerateLut(HueVsHueData hueData)
    {
        const int size = CurveLutGenerator.LutSize;
        const int floatsPerTexel = 4;
        int totalTexels = size * size * size;

        var result = new byte[totalTexels * floatsPerTexel * sizeof(float)];
        Span<float> floatData = MemoryMarshal.Cast<byte, float>(result.AsSpan());

        // Hue vs Hue 1D LUT (色相0~1 → シフト量) — 循環補間
        Span<double> hueLut = stackalloc double[size];
        CurveInterpolation.BuildWrappingLookupTable(hueData.Hue.Points, hueLut);

        // 恒等チェック: 全エントリが0.5(±ε) → シフト無し → 恒等LUTを直接書き込み
        bool isIdentity = true;
        for (int i = 0; i < size; i++)
        {
            if (Math.Abs(hueLut[i] - 0.5) > 1e-9)
            {
                isIdentity = false;
                break;
            }
        }

        if (isIdentity)
        {
            double invMax = 1.0 / (size - 1);
            int idx = 0;
            for (int rIdx = 0; rIdx < size; rIdx++)
            {
                float r = (float)(rIdx * invMax);
                for (int gIdx = 0; gIdx < size; gIdx++)
                {
                    float g = (float)(gIdx * invMax);
                    for (int bIdx = 0; bIdx < size; bIdx++)
                    {
                        floatData[idx] = r;
                        floatData[idx + 1] = g;
                        floatData[idx + 2] = (float)(bIdx * invMax);
                        floatData[idx + 3] = 1.0f;
                        idx += floatsPerTexel;
                    }
                }
            }
            return result;
        }

        double invMax2 = 1.0 / (size - 1);
        int idx2 = 0;

        // D2D LUT: Z=Red(slowest), Y=Green(middle), X=Blue(fastest)
        for (int rIdx = 0; rIdx < size; rIdx++)
        {
            double r = rIdx * invMax2;
            for (int gIdx = 0; gIdx < size; gIdx++)
            {
                double g = gIdx * invMax2;
                for (int bIdx = 0; bIdx < size; bIdx++)
                {
                    double b = bIdx * invMax2;

                    RgbToHsv(r, g, b, out double h, out double s, out double v);

                    // 出力RGB — ループ変数 r, g を保護するため別変数を使用
                    double outR = r, outG = g, outB = b;

                    // 無彩色保護: 彩度・明度に基づくスムーズなフェード係数
                    double satFade = SmoothStep(SatLow, SatHigh, s);
                    double valFade = SmoothStep(ValLow, ValHigh, v);
                    double protection = satFade * valFade;

                    if (protection > 1e-9)
                    {
                        double hueShift = LookupHueShift(hueLut, h, size);
                        double shift = (hueShift - 0.5) * protection;

                        double hNew = h + shift;
                        hNew -= Math.Floor(hNew);

                        HsvToRgb(hNew, s, v, out outR, out outG, out outB);
                    }

                    floatData[idx2] = (float)outR;
                    floatData[idx2 + 1] = (float)outG;
                    floatData[idx2 + 2] = (float)outB;
                    floatData[idx2 + 3] = 1.0f;
                    idx2 += floatsPerTexel;
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Hermite smoothstep: edge0以下で0、edge1以上で1、間は滑らかに補間。
    /// 微分が境界で0になるため、視覚的に自然な遷移を提供する。
    /// </summary>
    private static double SmoothStep(double edge0, double edge1, double x)
    {
        double t = Math.Clamp((x - edge0) / (edge1 - edge0), 0.0, 1.0);
        return t * t * (3.0 - 2.0 * t);
    }

    /// <summary>
    /// 色相シフトLUTからの線形補間ルックアップ。
    /// </summary>
    private static double LookupHueShift(ReadOnlySpan<double> hueLut, double hue, int size)
    {
        double pos = hue * (size - 1);
        int lo = (int)pos;
        int hi = Math.Min(lo + 1, size - 1);
        double frac = pos - lo;
        return hueLut[lo] + (hueLut[hi] - hueLut[lo]) * frac;
    }

    /// <summary>
    /// RGB (各0~1) → HSV (H: 0~1, S: 0~1, V: 0~1) 変換。
    /// </summary>
    internal static void RgbToHsv(double r, double g, double b,
        out double h, out double s, out double v)
    {
        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double delta = max - min;

        v = max;
        s = max > 1e-12 ? delta / max : 0.0;

        if (delta < 1e-12)
        {
            h = 0.0;
            return;
        }

        if (max == r)
            h = (g - b) / delta;
        else if (max == g)
            h = 2.0 + (b - r) / delta;
        else
            h = 4.0 + (r - g) / delta;

        h /= 6.0;
        if (h < 0.0) h += 1.0;
    }

    /// <summary>
    /// HSV (H: 0~1, S: 0~1, V: 0~1) → RGB (各0~1) 変換。
    /// </summary>
    internal static void HsvToRgb(double h, double s, double v,
        out double r, out double g, out double b)
    {
        if (s < 1e-12)
        {
            r = g = b = v;
            return;
        }

        double hh = h * 6.0;
        if (hh >= 6.0) hh = 0.0;
        int sector = (int)hh;
        double f = hh - sector;

        double p = v * (1.0 - s);
        double q = v * (1.0 - s * f);
        double t = v * (1.0 - s * (1.0 - f));

        switch (sector)
        {
            case 0: r = v; g = t; b = p; break;
            case 1: r = q; g = v; b = p; break;
            case 2: r = p; g = v; b = t; break;
            case 3: r = p; g = q; b = v; break;
            case 4: r = t; g = p; b = v; break;
            default: r = v; g = p; b = q; break;
        }
    }
}
