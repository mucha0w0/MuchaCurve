using System.Runtime.InteropServices;

namespace MuchaCurve;

/// <summary>
/// Sat vs Luma カーブデータから32x32x32の3D LUTバイト配列を生成する。
/// 各テクセルについて RGB→HSV で彩度(S)を取得し、カーブから輝度オフセットを参照して
/// BT.709輝度を基準にRGB空間で明るさを調整する。
/// 知覚輝度: BT.709 (0.2126R + 0.7152G + 0.0722B) を使用し、
/// 人間の視覚特性に基づいたの明るさ調整を実現する。
/// </summary>
public static class SatVsLumaLutGenerator
{
    // BT.709 輝度係数
    private const double Wr = 0.2126;
    private const double Wg = 0.7152;
    private const double Wb = 0.0722;

    // 輝度オフセットの最大幅 (±1.0 のカーブ値に対する輝度の変動幅)
    private const double MaxOffset = 0.5;

    /// <summary>
    /// SatVsLumaDataからLUTバイト配列を生成する。
    /// </summary>
    public static byte[] GenerateLut(SatVsLumaData lumaData)
    {
        const int size = CurveLutGenerator.LutSize;
        const int floatsPerTexel = 4;
        int totalTexels = size * size * size;

        var result = new byte[totalTexels * floatsPerTexel * sizeof(float)];
        Span<float> floatData = MemoryMarshal.Cast<byte, float>(result.AsSpan());

        // Sat vs Luma 1D LUT (彩度0~1 → 輝度オフセット) — 非循環補間
        Span<double> lumaLut = stackalloc double[size];
        CurveInterpolation.BuildLookupTable(lumaData.Luma.Points, lumaLut);

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

                    // HSV で彩度を取得
                    HueVsHueLutGenerator.RgbToHsv(r, g, b, out _, out double s, out _);

                    double outR = r, outG = g, outB = b;

                    // カーブから輝度オフセットを取得
                    double rawOffset = LookupLumaOffset(lumaLut, s, size);

                    // Y=0.5 → 0, Y=0 → -MaxOffset, Y=1 → +MaxOffset
                    double offset = (rawOffset - 0.5) * 2.0 * MaxOffset;

                    if (Math.Abs(offset) > 1e-9)
                    {
                        // BT.709 輝度を基準にRGB空間で明るさを調整
                        // offset > 0: 明化、offset < 0: 暗化
                        outR = r + offset;
                        outG = g + offset;
                        outB = b + offset;

                        // [0, 1] クランプ (白飛び・黒潰れを許容)
                        outR = Math.Clamp(outR, 0.0, 1.0);
                        outG = Math.Clamp(outG, 0.0, 1.0);
                        outB = Math.Clamp(outB, 0.0, 1.0);
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
    /// 輝度オフセットLUTからの線形補間ルックアップ。
    /// </summary>
    private static double LookupLumaOffset(ReadOnlySpan<double> lumaLut, double sat, int size)
    {
        double pos = Math.Clamp(sat, 0.0, 1.0) * (size - 1);
        int lo = (int)pos;
        int hi = Math.Min(lo + 1, size - 1);
        double frac = pos - lo;
        return lumaLut[lo] + (lumaLut[hi] - lumaLut[lo]) * frac;
    }
}
