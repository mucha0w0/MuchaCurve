using System.Runtime.InteropServices;

namespace MuchaCurve;

/// <summary>
/// Luma vs Sat カーブデータから32x32x32の3D LUTバイト配列を生成する。
/// 各テクセルについて BT.709輝度を計算し、その輝度に応じた彩度倍率を適用する。
/// 彩度はRGB空間で輝度グレー点を基準に調整し、色相を保持する。
/// ソフトニー: 彩度が飽和に近づく際に滑らかにクランプし、急激な飽和を防止する。
/// </summary>
public static class LumaVsSatLutGenerator
{
    // BT.709 輝度係数
    private const double Wr = 0.2126;
    private const double Wg = 0.7152;
    private const double Wb = 0.0722;

    // ソフトニーパラメータ: 彩度がこの閾値を超えると圧縮が始まる
    private const double KneeThreshold = 0.8;

    /// <summary>
    /// LumaVsSatDataからLUTバイト配列を生成する。
    /// </summary>
    public static byte[] GenerateLut(LumaVsSatData satData)
    {
        const int size = CurveLutGenerator.LutSize;
        const int floatsPerTexel = 4;
        int totalTexels = size * size * size;

        var result = new byte[totalTexels * floatsPerTexel * sizeof(float)];
        Span<float> floatData = MemoryMarshal.Cast<byte, float>(result.AsSpan());

        // Luma vs Sat 1D LUT (輝度0~1 → 彩度倍率) — 非循環補間
        Span<double> satLut = stackalloc double[size];
        CurveInterpolation.BuildLookupTable(satData.Sat.Points, satLut);

        // 恒等チェック: 全エントリが0.5(±ε) → 倍率1.0x → 恒等LUTを直接書き込み
        bool isIdentity = true;
        for (int i = 0; i < size; i++)
        {
            if (Math.Abs(satLut[i] - 0.5) > 1e-9)
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

                    // BT.709 輝度
                    double luma = Wr * r + Wg * g + Wb * b;

                    double outR = r, outG = g, outB = b;

                    // カーブから彩度倍率を取得
                    double rawMult = LookupSatMultiplier(satLut, luma, size);

                    // 非対称マッピング: Y=0→0x, Y=0.5→1x, Y=1→4x
                    double multiplier;
                    if (rawMult <= 0.5)
                    {
                        // 減衰側: Y=0→0x, Y=0.5→1x (線形)
                        multiplier = rawMult * 2.0;
                    }
                    else
                    {
                        // ブースト側: Y=0.5→1x, Y=1→4x (線形)
                        double t = (rawMult - 0.5) * 2.0;
                        multiplier = 1.0 + t * 3.0;
                    }

                    if (Math.Abs(multiplier - 1.0) > 1e-9)
                    {
                        // RGB空間で輝度グレー点を基準に彩度を調整
                        outR = luma + multiplier * (r - luma);
                        outG = luma + multiplier * (g - luma);
                        outB = luma + multiplier * (b - luma);

                        // ソフトクランプ: ガマット境界で滑らかに圧縮
                        outR = SoftClamp01(outR);
                        outG = SoftClamp01(outG);
                        outB = SoftClamp01(outB);
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
    /// 彩度倍率LUTからの線形補間ルックアップ。
    /// </summary>
    private static double LookupSatMultiplier(ReadOnlySpan<double> satLut, double luma, int size)
    {
        double pos = Math.Clamp(luma, 0.0, 1.0) * (size - 1);
        int lo = (int)pos;
        int hi = Math.Min(lo + 1, size - 1);
        double frac = pos - lo;
        return satLut[lo] + (satLut[hi] - satLut[lo]) * frac;
    }

    /// <summary>
    /// ソフトクランプ: 0と1の境界で滑らかに頭打ちにする。
    /// 彩度ブースト時にRGBチャンネルが[0,1]を超えた場合、
    /// 色相を保持しながら滑らかにガマット内に収める。
    /// </summary>
    private static double SoftClamp01(double v)
    {
        if (v <= 0.0) return 0.0;
        if (v >= 1.0) return 1.0;

        // 下端ロールオフ (v < 0.05)
        const double lowKnee = 0.05;
        if (v < lowKnee)
            return lowKnee * (1.0 - Math.Exp(-v / lowKnee));

        // 上端ロールオフ (v > 0.95)
        const double highKnee = 0.95;
        if (v > highKnee)
        {
            double excess = v - highKnee;
            double range = 1.0 - highKnee;
            return highKnee + range * (1.0 - Math.Exp(-excess / range));
        }

        return v;
    }
}
