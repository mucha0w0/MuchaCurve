using System.Text.Json;
using System.Text.Json.Serialization;

namespace MuchaCurve;

/// <summary>
/// 制御点の種類。Smooth = 滑らかなベジェ曲線、Corner = 角の頂点、Anchor = Y固定のアンカー。
/// </summary>
public enum CurvePointType { Smooth, Corner, Anchor }

/// <summary>
/// カーブ上の制御点。X=入力(0~1), Y=出力(0~1)。
/// </summary>
public readonly record struct CurvePoint(double X, double Y, CurvePointType Type = CurvePointType.Smooth);

/// <summary>
/// みゅしゃかーぶの1チャンネル分のデータ。
/// 制御点のリストを保持し、Monotone Cubic Spline で補間する。
/// </summary>
public class CurveChannelData
{
    /// <summary>
    /// 制御点リスト（X昇順でソート済みであること）。
    /// 最低2点（始点0,0 と 終点1,1）を持つ。
    /// </summary>
    public List<CurvePoint> Points { get; set; } =
    new List<CurvePoint>
    {
        new(0.0, 0.0),
        new(1.0, 1.0)
    };

}

/// <summary>
/// みゅしゃかーぶの全チャンネルデータ。
/// Y(輝度), R, G, B の4チャンネルを保持する。
/// </summary>
public class CustomCurveData
{
    public CurveChannelData Master { get; set; } = new();
    public CurveChannelData Red { get; set; } = new();
    public CurveChannelData Green { get; set; } = new();
    public CurveChannelData Blue { get; set; } = new();

    /// <summary>
    /// JSON文字列にシリアライズする。
    /// </summary>
    public string Serialize()
    {
        return JsonSerializer.Serialize(this, CurveJsonContext.Default.CustomCurveData);
    }

    /// <summary>
    /// JSON文字列からデシリアライズする。
    /// </summary>
    public static CustomCurveData Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new CustomCurveData();

        try
        {
            return JsonSerializer.Deserialize(json, CurveJsonContext.Default.CustomCurveData)
                   ?? new CustomCurveData();
        }
        catch
        {
            return new CustomCurveData();
        }
    }

    /// <summary>
    /// いずれかのチャンネルがデフォルト（直線）から変更されているか。
    /// </summary>
    public bool IsIdentity()
    {
        return IsChannelIdentity(Master)
            && IsChannelIdentity(Red)
            && IsChannelIdentity(Green)
            && IsChannelIdentity(Blue);
    }

    internal static bool IsChannelIdentity(CurveChannelData ch)
    {
        if (ch.Points.Count != 2) return false;
        var p0 = ch.Points[0];
        var p1 = ch.Points[1];
        return p0.Type == CurvePointType.Smooth && p1.Type == CurvePointType.Smooth
            && Math.Abs(p0.X) < 1e-9 && Math.Abs(p0.Y) < 1e-9
            && Math.Abs(p1.X - 1.0) < 1e-9 && Math.Abs(p1.Y - 1.0) < 1e-9;
    }
}

/// <summary>
/// Hue vs Hue カーブデータ。
/// X=入力色相 (0~1 → 0°~360°)、Y=色相シフト (0~1 → -180°~+180°、0.5=シフトなし)。
/// カーブは循環的: X=0 の値は X=1 と同じ色相を表す。
/// </summary>
public class HueVsHueData
{
    /// <summary>
    /// 色相シフトカーブの制御点リスト。
    /// 空リスト = シフトなし（ニュートラル）。
    /// カーブは循環的: X=0 と X=1 は同じ色相。端点なし。
    /// Y=0.5 がニュートラル、Y=0 は -180°シフト、Y=1 は +180°シフト。
    /// </summary>
    public CurveChannelData Hue { get; set; } = new()
    {
        Points = new List<CurvePoint>()
    };

    public string Serialize()
        => JsonSerializer.Serialize(this, CurveJsonContext.Default.HueVsHueData);

    public static HueVsHueData Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new HueVsHueData();
        try
        {
            return JsonSerializer.Deserialize(json, CurveJsonContext.Default.HueVsHueData)
                   ?? new HueVsHueData();
        }
        catch
        {
            return new HueVsHueData();
        }
    }

    /// <summary>
    /// 色相シフトがニュートラルならtrue。ポイントが空 = シフトなし。
    /// </summary>
    public bool IsIdentity() => Hue.Points.Count == 0;
}

/// <summary>
/// Hue vs Sat カーブデータ。
/// X=入力色相 (0~1 → 0°~360°)、Y=彩度倍率 (0~1 → 0x~2x、0.5=1.0x 変更なし)。
/// カーブは循環的: X=0 の値は X=1 と同じ色相を表す。
/// </summary>
public class HueVsSatData
{
    /// <summary>
    /// 彩度倍率カーブの制御点リスト。
    /// 空リスト = 倍率なし（ニュートラル）。
    /// カーブは循環的: X=0 と X=1 は同じ色相。端点なし。
    /// Y=0.5 がニュートラル (1.0x)、Y=0 は 0x (完全脱色)、Y=1 は 2.0x (最大ブースト)。
    /// </summary>
    public CurveChannelData Sat { get; set; } = new()
    {
        Points = new List<CurvePoint>()
    };

    public string Serialize()
        => JsonSerializer.Serialize(this, CurveJsonContext.Default.HueVsSatData);

    public static HueVsSatData Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new HueVsSatData();
        try
        {
            return JsonSerializer.Deserialize(json, CurveJsonContext.Default.HueVsSatData)
                   ?? new HueVsSatData();
        }
        catch
        {
            return new HueVsSatData();
        }
    }

    /// <summary>
    /// 彩度倍率がニュートラルならtrue。ポイントが空 = 変更なし。
    /// </summary>
    public bool IsIdentity() => Sat.Points.Count == 0;
}

/// <summary>
/// Hue vs Luma カーブデータ。
/// X=入力色相 (0~1 → 0°~360°)、Y=輝度オフセット (0~1 → -1.0~+1.0、0.5=変更なし)。
/// カーブは循環的: X=0 の値は X=1 と同じ色相を表す。
/// </summary>
public class HueVsLumaData
{
    /// <summary>
    /// 輝度オフセットカーブの制御点リスト。
    /// 空リスト = 変更なし（ニュートラル）。
    /// カーブは循環的: X=0 と X=1 は同じ色相。端点なし。
    /// Y=0.5 がニュートラル、Y=0 は最大暗化、Y=1 は最大明化。
    /// </summary>
    public CurveChannelData Luma { get; set; } = new()
    {
        Points = new List<CurvePoint>()
    };

    public string Serialize()
        => JsonSerializer.Serialize(this, CurveJsonContext.Default.HueVsLumaData);

    public static HueVsLumaData Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new HueVsLumaData();
        try
        {
            return JsonSerializer.Deserialize(json, CurveJsonContext.Default.HueVsLumaData)
                   ?? new HueVsLumaData();
        }
        catch
        {
            return new HueVsLumaData();
        }
    }

    /// <summary>
    /// 輝度オフセットがニュートラルならtrue。ポイントが空 = 変更なし。
    /// </summary>
    public bool IsIdentity() => Luma.Points.Count == 0;
}

/// <summary>
/// Luma vs Sat カーブデータ。
/// X=入力輝度 (0~1、BT.709)、Y=彩度倍率 (0~1 → 0x~4x、0.5=1.0x 変更なし)。
/// カーブは非循環: 左端=暗部(0)、右端=明部(1)。デフォルトで端点あり。
/// </summary>
public class LumaVsSatData
{
    /// <summary>
    /// 彩度倍率カーブの制御点リスト。
    /// デフォルトは両端 (0, 0.5)(1, 0.5) のニュートラル直線。
    /// Y=0.5 がニュートラル (1.0x)、Y=0 は 0x (完全脱色)、Y=1 は 4x (最大ブースト)。
    /// </summary>
    public CurveChannelData Sat { get; set; } = new()
    {
        Points = new List<CurvePoint> { new CurvePoint(0, 0.5), new CurvePoint(1, 0.5) }
    };

    public string Serialize()
        => JsonSerializer.Serialize(this, CurveJsonContext.Default.LumaVsSatData);

    public static LumaVsSatData Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new LumaVsSatData();
        try
        {
            return JsonSerializer.Deserialize(json, CurveJsonContext.Default.LumaVsSatData)
                   ?? new LumaVsSatData();
        }
        catch
        {
            return new LumaVsSatData();
        }
    }

    /// <summary>
    /// 彩度倍率がニュートラルならtrue。全ポイントが Y=0.5 なら変更なし。
    /// </summary>
    public bool IsIdentity()
    {
        foreach (var pt in Sat.Points)
            if (Math.Abs(pt.Y - 0.5) > 1e-9) return false;
        return true;
    }
}

/// <summary>
/// Sat vs Sat カーブデータ。
/// X=入力彩度 (0~1、HSVのS値)、Y=彩度倍率 (0~1 → 0x~4x、0.5=1.0x 変更なし)。
/// カーブは非循環: 左端=無彩色(0)、右端=純色(1)。デフォルトで端点あり。
/// </summary>
public class SatVsSatData
{
    /// <summary>
    /// 彩度倍率カーブの制御点リスト。
    /// デフォルトは両端 (0, 0.5)(1, 0.5) のニュートラル直線。
    /// Y=0.5 がニュートラル (1.0x)、Y=0 は 0x (完全脱色)、Y=1 は 4x (最大ブースト)。
    /// </summary>
    public CurveChannelData Sat { get; set; } = new()
    {
        Points = new List<CurvePoint> { new CurvePoint(0, 0.5), new CurvePoint(1, 0.5) }
    };

    public string Serialize()
        => JsonSerializer.Serialize(this, CurveJsonContext.Default.SatVsSatData);

    public static SatVsSatData Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new SatVsSatData();
        try
        {
            return JsonSerializer.Deserialize(json, CurveJsonContext.Default.SatVsSatData)
                   ?? new SatVsSatData();
        }
        catch
        {
            return new SatVsSatData();
        }
    }

    /// <summary>
    /// 彩度倍率がニュートラルならtrue。全ポイントが Y=0.5 なら変更なし。
    /// </summary>
    public bool IsIdentity()
    {
        foreach (var pt in Sat.Points)
            if (Math.Abs(pt.Y - 0.5) > 1e-9) return false;
        return true;
    }
}

/// <summary>
/// Sat vs Luma カーブデータ。
/// X=入力彩度 (0~1、HSVのS値)、Y=輝度オフセット (0~1 → -0.5~+0.5、0.5=変更なし)。
/// カーブは非循環: 左端=無彩色(0)、右端=純色(1)。デフォルトで端点あり。
/// </summary>
public class SatVsLumaData
{
    /// <summary>
    /// 輝度オフセットカーブの制御点リスト。
    /// デフォルトは両端 (0, 0.5)(1, 0.5) のニュートラル直線。
    /// Y=0.5 がニュートラル、Y=0 は最大暗化、Y=1 は最大明化。
    /// </summary>
    public CurveChannelData Luma { get; set; } = new()
    {
        Points = new List<CurvePoint> { new CurvePoint(0, 0.5), new CurvePoint(1, 0.5) }
    };

    public string Serialize()
        => JsonSerializer.Serialize(this, CurveJsonContext.Default.SatVsLumaData);

    public static SatVsLumaData Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new SatVsLumaData();
        try
        {
            return JsonSerializer.Deserialize(json, CurveJsonContext.Default.SatVsLumaData)
                   ?? new SatVsLumaData();
        }
        catch
        {
            return new SatVsLumaData();
        }
    }

    /// <summary>
    /// 輝度オフセットがニュートラルならtrue。全ポイントが Y=0.5 なら変更なし。
    /// </summary>
    public bool IsIdentity()
    {
        foreach (var pt in Luma.Points)
            if (Math.Abs(pt.Y - 0.5) > 1e-9) return false;
        return true;
    }
}

/// <summary>
/// AOT/Trimming 対応のための Source Generator JSON コンテキスト。
/// </summary>
[JsonSerializable(typeof(CustomCurveData))]
[JsonSerializable(typeof(HueVsHueData))]
[JsonSerializable(typeof(HueVsSatData))]
[JsonSerializable(typeof(HueVsLumaData))]
[JsonSerializable(typeof(LumaVsSatData))]
[JsonSerializable(typeof(SatVsSatData))]
[JsonSerializable(typeof(SatVsLumaData))]
[JsonSerializable(typeof(CurveChannelData))]
[JsonSerializable(typeof(CurvePoint))]
[JsonSerializable(typeof(CurvePointType))]
[JsonSerializable(typeof(List<CurvePoint>))]
internal partial class CurveJsonContext : JsonSerializerContext;
