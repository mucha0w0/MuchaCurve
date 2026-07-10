using System.Runtime.InteropServices;

namespace MuchaCurve;

/// <summary>
/// Hue vs Sat カーブデータから32x32x32の3D LUTバイト配列を生成する。
/// 各テクセルについて RGB→HSV→彩度乗算→HSV→RGB の変換を行う。
/// 無彩色保護: 低彩度・低/高輝度領域では彩度変更を滑らかにフェードアウトし、
/// ノイズや偽色の発生を防ぐ。
/// ソフトニー: 彩度が1.0に近づく際に滑らかにクランプし、急激な飽和を防止する。
/// </summary>
public static class HueVsSatLutGenerator
{
    // 無彩色保護パラメータ
    private const double SatLow = 0.05;
    private const double SatHigh = 0.15;
    private const double ValLow = 0.02;
    private const double ValHigh = 0.10;

    // ソフトニーパラメータ: 彩度がこの閾値を超えると圧縮が始まる
    private const double KneeThreshold = 0.8;

    /// <summary>
    /// HueVsSatDataからLUTバイト配列を生成する。
    /// </summary>
    public static byte[] GenerateLut(HueVsSatData satData)
    {
        const int size = CurveLutGenerator.LutSize;
        const int floatsPerTexel = 4;
        int totalTexels = size * size * size;

        var result = new byte[totalTexels * floatsPerTexel * sizeof(float)];
        Span<float> floatData = MemoryMarshal.Cast<byte, float>(result.AsSpan());

        // Hue vs Sat 1D LUT (色相0~1 → 彩度倍率) — 循環補間
        Span<double> satLut = stackalloc double[size];
        CurveInterpolation.BuildWrappingLookupTable(satData.Sat.Points, satLut);

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

                    HueVsHueLutGenerator.RgbToHsv(r, g, b, out double h, out double s, out double v);

                    double outR = r, outG = g, outB = b;

                    // 無彩色保護: 彩度・明度に基づくスムーズなフェード係数
                    double satFade = SmoothStep(SatLow, SatHigh, s);
                    double valFade = SmoothStep(ValLow, ValHigh, v);
                    double protection = satFade * valFade;

                    if (protection > 1e-9)
                    {
                        double multiplierRaw = LookupSatMultiplier(satLut, h, size);
                        // 非対称マッピング: Y=0→0x, Y=0.5→1x, Y=1→4x
                        double multiplier;
                        if (multiplierRaw <= 0.5)
                        {
                            // 減衰側: Y=0→0x, Y=0.5→1x (線形)
                            multiplier = multiplierRaw * 2.0;
                        }
                        else
                        {
                            // ブースト側: Y=0.5→1x, Y=1→4x (線形)
                            double t = (multiplierRaw - 0.5) * 2.0;
                            multiplier = 1.0 + t * 3.0;
                        }

                        // 保護係数でブレンド: multiplier=1.0(無変更)に近づける
                        multiplier = 1.0 + (multiplier - 1.0) * protection;

                        double sNew = s * multiplier;

                        // ソフトニー: 彩度が KneeThreshold を超えた場合は圧縮
                        sNew = SoftKneeClamp(sNew);

                        HueVsHueLutGenerator.HsvToRgb(h, sNew, v, out outR, out outG, out outB);
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
    /// 彩度倍率LUTからの線形補間ルックアップ。
    /// </summary>
    private static double LookupSatMultiplier(ReadOnlySpan<double> satLut, double hue, int size)
    {
        double pos = hue * (size - 1);
        int lo = (int)pos;
        int hi = Math.Min(lo + 1, size - 1);
        double frac = pos - lo;
        return satLut[lo] + (satLut[hi] - satLut[lo]) * frac;
    }

    /// <summary>
    /// ソフトニー: 彩度が KneeThreshold を超えた値を滑らかに 1.0 へ圧縮する。
    /// KneeThreshold 以下ではそのまま通過、超えた分は二次曲線で圧縮。
    /// </summary>
    private static double SoftKneeClamp(double s)
    {
        if (s <= 0.0) return 0.0;
        if (s <= KneeThreshold) return s;

        // KneeThreshold ~ ∞ を KneeThreshold ~ 1.0 に圧縮
        double range = 1.0 - KneeThreshold; // 0.2
        double excess = s - KneeThreshold;
        // 漸近的に 1.0 に近づく: threshold + range * (1 - exp(-excess/range))
        return KneeThreshold + range * (1.0 - Math.Exp(-excess / range));
    }
}
