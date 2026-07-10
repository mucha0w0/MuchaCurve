using System.Runtime.InteropServices;

namespace MuchaCurve;

/// <summary>
/// Hue vs Luma カーブデータから32x32x32の3D LUTバイト配列を生成する。
/// 各テクセルについて RGB→HSV→輝度(V)オフセット→HSV→RGB の変換を行う。
/// 無彩色保護: 低彩度領域では輝度変更を滑らかにフェードアウトし、
/// グレー領域への偽色を防ぐ。
/// 彩度保持: 輝度を下げる際に彩度が維持されるよう V のみを操作し、
/// 色の深みを保つ (DaVinci Resolve 準拠)。
/// </summary>
public static class HueVsLumaLutGenerator
{
    // 無彩色保護パラメータ
    private const double SatLow = 0.05;
    private const double SatHigh = 0.15;

    // 輝度オフセットの最大幅 (±1.0 のカーブ値に対する V の変動幅)
    private const double MaxOffset = 0.5;

    /// <summary>
    /// HueVsLumaDataからLUTバイト配列を生成する。
    /// </summary>
    public static byte[] GenerateLut(HueVsLumaData lumaData)
    {
        const int size = CurveLutGenerator.LutSize;
        const int floatsPerTexel = 4;
        int totalTexels = size * size * size;

        var result = new byte[totalTexels * floatsPerTexel * sizeof(float)];
        Span<float> floatData = MemoryMarshal.Cast<byte, float>(result.AsSpan());

        // Hue vs Luma 1D LUT (色相0~1 → 輝度オフセット) — 循環補間
        Span<double> lumaLut = stackalloc double[size];
        CurveInterpolation.BuildWrappingLookupTable(lumaData.Luma.Points, lumaLut);

        // 恒等チェック: 全エントリが0.5(±ε) → オフセット0 → 恒等LUT
        bool isIdentity = true;
        for (int i = 0; i < size; i++)
        {
            if (Math.Abs(lumaLut[i] - 0.5) > 1e-9)
            {
                isIdentity = false;
                break;
            }
        }

        if (isIdentity)
        {
            WriteIdentityLut(floatData, size, floatsPerTexel);
            return result;
        }

        double invMax = 1.0 / (size - 1);
        int idx = 0;

        // D2D LUT: Z=Red(slowest), Y=Green(middle), X=Blue(fastest)
        for (int rIdx = 0; rIdx < size; rIdx++)
        {
            double r = rIdx * invMax;
            for (int gIdx = 0; gIdx < size; gIdx++)
            {
                double g = gIdx * invMax;
                for (int bIdx = 0; bIdx < size; bIdx++)
                {
                    double b = bIdx * invMax;

                    HueVsHueLutGenerator.RgbToHsv(r, g, b, out double h, out double s, out double v);

                    double outR = r, outG = g, outB = b;

                    // 無彩色保護: 低彩度領域ではオフセットをフェードアウト
                    double satFade = SmoothStep(SatLow, SatHigh, s);

                    if (satFade > 1e-9)
                    {
                        double rawOffset = LookupLumaOffset(lumaLut, h, size);
                        // Y=0.5 → 0, Y=0 → -MaxOffset, Y=1 → +MaxOffset
                        double offset = (rawOffset - 0.5) * 2.0 * MaxOffset * satFade;

                        double vNew = v + offset;

                        // [0, 1] にクランプ (白飛び・黒潰れを許容)
                        vNew = Math.Clamp(vNew, 0.0, 1.0);

                        // V のみ変更して HSV→RGB (S は維持 → 色の深みを保持)
                        HueVsHueLutGenerator.HsvToRgb(h, s, vNew, out outR, out outG, out outB);
                    }

                    floatData[idx] = (float)outR;
                    floatData[idx + 1] = (float)outG;
                    floatData[idx + 2] = (float)outB;
                    floatData[idx + 3] = 1.0f;
                    idx += floatsPerTexel;
                }
            }
        }

        return result;
    }

    /// <summary>
    /// 恒等LUT (入力=出力) を書き込む。
    /// </summary>
    private static void WriteIdentityLut(Span<float> floatData, int size, int floatsPerTexel)
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
    }

    /// <summary>
    /// Hermite smoothstep。
    /// </summary>
    private static double SmoothStep(double edge0, double edge1, double x)
    {
        double t = Math.Clamp((x - edge0) / (edge1 - edge0), 0.0, 1.0);
        return t * t * (3.0 - 2.0 * t);
    }

    /// <summary>
    /// 輝度オフセットLUTからの線形補間ルックアップ。
    /// </summary>
    private static double LookupLumaOffset(ReadOnlySpan<double> lumaLut, double hue, int size)
    {
        double pos = hue * (size - 1);
        int lo = (int)pos;
        int hi = Math.Min(lo + 1, size - 1);
        double frac = pos - lo;
        return lumaLut[lo] + (lumaLut[hi] - lumaLut[lo]) * frac;
    }

}
