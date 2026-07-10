using System.Runtime.InteropServices;

namespace MuchaCurve;

/// <summary>
/// Sat vs Sat カーブデータから32x32x32の3D LUTバイト配列を生成する。
/// 各テクセルについて RGB→HSV で彩度(S)を取得し、カーブから倍率を参照して
/// 新しい彩度を決定し、HSV→RGB で戻す。
/// 無彩色保護: 極低彩度(S &lt; 0.005)ではブースト時のノイズ色を防ぐため
/// 効果を滑らかにフェードアウトする。
/// </summary>
public static class SatVsSatLutGenerator
{
    // 無彩色保護: この閾値以下ではブースト効果をフェードアウト
    private const double SatLow = 0.005;
    private const double SatHigh = 0.02;

    /// <summary>
    /// SatVsSatDataからLUTバイト配列を生成する。
    /// </summary>
    public static byte[] GenerateLut(SatVsSatData satData)
    {
        const int size = CurveLutGenerator.LutSize;
        const int floatsPerTexel = 4;
        int totalTexels = size * size * size;

        var result = new byte[totalTexels * floatsPerTexel * sizeof(float)];
        Span<float> floatData = MemoryMarshal.Cast<byte, float>(result.AsSpan());

        // Sat vs Sat 1D LUT (彩度0~1 → 彩度倍率) — 非循環補間
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

                    // RGB → HSV
                    HueVsHueLutGenerator.RgbToHsv(r, g, b, out double h, out double s, out double v);

                    double outR = r, outG = g, outB = b;

                    // カーブから彩度倍率を取得
                    double rawMult = LookupSatMultiplier(satLut, s, size);

                    // 非対称マッピング: Y=0→0x, Y=0.5→1x, Y=1→4x
                    double multiplier;
                    if (rawMult <= 0.5)
                    {
                        multiplier = rawMult * 2.0;
                    }
                    else
                    {
                        double t = (rawMult - 0.5) * 2.0;
                        multiplier = 1.0 + t * 3.0;
                    }

                    if (Math.Abs(multiplier - 1.0) > 1e-9)
                    {
                        double newS = s * multiplier;
                        newS = Math.Clamp(newS, 0.0, 1.0);

                        // 無彩色保護: 極低彩度でブースト時にノイズ色が出るのを防ぐ
                        if (multiplier > 1.0 && s < SatHigh)
                        {
                            double fade = s <= SatLow ? 0.0
                                : (s - SatLow) / (SatHigh - SatLow);
                            newS = s + (newS - s) * fade;
                        }

                        // HSV → RGB (彩度のみ変更)
                        HueVsHueLutGenerator.HsvToRgb(h, newS, v, out outR, out outG, out outB);
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
    private static double LookupSatMultiplier(ReadOnlySpan<double> satLut, double sat, int size)
    {
        double pos = Math.Clamp(sat, 0.0, 1.0) * (size - 1);
        int lo = (int)pos;
        int hi = Math.Min(lo + 1, size - 1);
        double frac = pos - lo;
        return satLut[lo] + (satLut[hi] - satLut[lo]) * frac;
    }
}
