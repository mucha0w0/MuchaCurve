using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;
using YukkuriMovieMaker.Settings;

namespace MuchaCurve;

[VideoEffect("みゅしゃかーぶ", new[] { "色調補正" }, new string[0], false, false)]
public class CustomCurveEffect : VideoEffectBase
{
    public override string Label => "みゅしゃかーぶ";

    /// <summary>
    /// カーブデータ (JSON文字列)。カスタムプロパティエディタでUI表示・編集する。
    /// </summary>
    [Display(GroupName = "Custom Curves", Name = "", Description = "Y/R/G/Bチャンネルのカーブを編集")]
    [CurveEditor]
    public string CurveDataJson
    {
        get => curveDataJson;
        set => Set(ref curveDataJson, value);
    }
    private string curveDataJson = new CustomCurveData().Serialize();

    /// <summary>
    /// エフェクト適用率。0%でパススルー、100%でフル適用。
    /// </summary>
    [Display(GroupName = "Custom Curves", Name = "適用率", Description = "エフェクトの適用率 (0〜100%)")]
    [AnimationSlider("F0", "%", 0, 100)]
    [DefaultValue(100d)]
    public Animation Strength { get; } = new(100, 0, 100, 0);

    [Display(GroupName = "Custom Curves", Name = "エフェクト効果", Description = "エフェクトの有効/無効")]
    [DefaultValue(true)]
    public bool CurveEnabled
    {
        get => curveEnabled;
        set => Set(ref curveEnabled, value);
    }
    private bool curveEnabled = true;

    /// <summary>
    /// Hue vs Hue カーブデータ (JSON文字列)。
    /// </summary>
    [Display(GroupName = "Hue vs Hue", Name = "", Description = "入力色相に対する色相シフトを編集")]
    [HueVsHueEditor]
    public string HueVsHueDataJson
    {
        get => hueVsHueDataJson;
        set => Set(ref hueVsHueDataJson, value);
    }
    private string hueVsHueDataJson = new HueVsHueData().Serialize();

    /// <summary>
    /// Hue vs Hue 適用率。
    /// </summary>
    [Display(GroupName = "Hue vs Hue", Name = "適用率", Description = "色相シフトの適用率 (0〜100%)")]
    [AnimationSlider("F0", "%", 0, 100)]
    [DefaultValue(100d)]
    public Animation HueVsHueStrength { get; } = new(100, 0, 100, 0);

    [Display(GroupName = "Hue vs Hue", Name = "エフェクト効果", Description = "エフェクトの有効/無効")]
    [DefaultValue(true)]
    public bool HueVsHueEnabled
    {
        get => hueVsHueEnabled;
        set => Set(ref hueVsHueEnabled, value);
    }
    private bool hueVsHueEnabled = true;

    /// <summary>
    /// Hue vs Sat カーブデータ (JSON文字列)。
    /// </summary>
    [Display(GroupName = "Hue vs Sat", Name = "", Description = "入力色相に対する彩度倍率を編集")]
    [HueVsSatEditor]
    public string HueVsSatDataJson
    {
        get => hueVsSatDataJson;
        set => Set(ref hueVsSatDataJson, value);
    }
    private string hueVsSatDataJson = new HueVsSatData().Serialize();

    /// <summary>
    /// Hue vs Sat 適用率。
    /// </summary>
    [Display(GroupName = "Hue vs Sat", Name = "適用率", Description = "彩度倍率の適用率 (0〜100%)")]
    [AnimationSlider("F0", "%", 0, 100)]
    [DefaultValue(100d)]
    public Animation HueVsSatStrength { get; } = new(100, 0, 100, 0);

    [Display(GroupName = "Hue vs Sat", Name = "エフェクト効果", Description = "エフェクトの有効/無効")]
    [DefaultValue(true)]
    public bool HueVsSatEnabled
    {
        get => hueVsSatEnabled;
        set => Set(ref hueVsSatEnabled, value);
    }
    private bool hueVsSatEnabled = true;

    /// <summary>
    /// Hue vs Luma カーブデータ (JSON文字列)。
    /// </summary>
    [Display(GroupName = "Hue vs Luma", Name = "", Description = "入力色相に対する輝度オフセットを編集")]
    [HueVsLumaEditor]
    public string HueVsLumaDataJson
    {
        get => hueVsLumaDataJson;
        set => Set(ref hueVsLumaDataJson, value);
    }
    private string hueVsLumaDataJson = new HueVsLumaData().Serialize();

    /// <summary>
    /// Hue vs Luma 適用率。
    /// </summary>
    [Display(GroupName = "Hue vs Luma", Name = "適用率", Description = "輝度オフセットの適用率 (0〜100%)")]
    [AnimationSlider("F0", "%", 0, 100)]
    [DefaultValue(100d)]
    public Animation HueVsLumaStrength { get; } = new(100, 0, 100, 0);

    [Display(GroupName = "Hue vs Luma", Name = "エフェクト効果", Description = "エフェクトの有効/無効")]
    [DefaultValue(true)]
    public bool HueVsLumaEnabled
    {
        get => hueVsLumaEnabled;
        set => Set(ref hueVsLumaEnabled, value);
    }
    private bool hueVsLumaEnabled = true;

    /// <summary>
    /// Luma vs Sat カーブデータ (JSON文字列)。
    /// </summary>
    [Display(GroupName = "Luma vs Sat", Name = "", Description = "入力輝度に対する彩度倍率を編集")]
    [LumaVsSatEditor]
    public string LumaVsSatDataJson
    {
        get => lumaVsSatDataJson;
        set => Set(ref lumaVsSatDataJson, value);
    }
    private string lumaVsSatDataJson = new LumaVsSatData().Serialize();

    /// <summary>
    /// Luma vs Sat 適用率。
    /// </summary>
    [Display(GroupName = "Luma vs Sat", Name = "適用率", Description = "輝度ベース彩度倍率の適用率 (0〜100%)")]
    [AnimationSlider("F0", "%", 0, 100)]
    [DefaultValue(100d)]
    public Animation LumaVsSatStrength { get; } = new(100, 0, 100, 0);

    [Display(GroupName = "Luma vs Sat", Name = "エフェクト効果", Description = "エフェクトの有効/無効")]
    [DefaultValue(true)]
    public bool LumaVsSatEnabled
    {
        get => lumaVsSatEnabled;
        set => Set(ref lumaVsSatEnabled, value);
    }
    private bool lumaVsSatEnabled = true;

    /// <summary>
    /// Sat vs Sat カーブデータ (JSON文字列)。
    /// </summary>
    [Display(GroupName = "Sat vs Sat", Name = "", Description = "入力彩度に対する彩度倍率を編集")]
    [SatVsSatEditor]
    public string SatVsSatDataJson
    {
        get => satVsSatDataJson;
        set => Set(ref satVsSatDataJson, value);
    }
    private string satVsSatDataJson = new SatVsSatData().Serialize();

    /// <summary>
    /// Sat vs Sat 適用率。
    /// </summary>
    [Display(GroupName = "Sat vs Sat", Name = "適用率", Description = "彩度ベース彩度倍率の適用率 (0〜100%)")]
    [AnimationSlider("F0", "%", 0, 100)]
    [DefaultValue(100d)]
    public Animation SatVsSatStrength { get; } = new(100, 0, 100, 0);

    [Display(GroupName = "Sat vs Sat", Name = "エフェクト効果", Description = "エフェクトの有効/無効")]
    [DefaultValue(true)]
    public bool SatVsSatEnabled
    {
        get => satVsSatEnabled;
        set => Set(ref satVsSatEnabled, value);
    }
    private bool satVsSatEnabled = true;

    /// <summary>
    /// Sat vs Luma カーブデータ (JSON文字列)。
    /// </summary>
    [Display(GroupName = "Sat vs Luma", Name = "", Description = "入力彩度に対する輝度オフセットを編集")]
    [SatVsLumaEditor]
    public string SatVsLumaDataJson
    {
        get => satVsLumaDataJson;
        set => Set(ref satVsLumaDataJson, value);
    }
    private string satVsLumaDataJson = new SatVsLumaData().Serialize();

    /// <summary>
    /// Sat vs Luma 適用率。
    /// </summary>
    [Display(GroupName = "Sat vs Luma", Name = "適用率", Description = "彩度ベース輝度オフセットの適用率 (0〜100%)")]
    [AnimationSlider("F0", "%", 0, 100)]
    [DefaultValue(100d)]
    public Animation SatVsLumaStrength { get; } = new(100, 0, 100, 0);

    [Display(GroupName = "Sat vs Luma", Name = "エフェクト効果", Description = "エフェクトの有効/無効")]
    [DefaultValue(true)]
    public bool SatVsLumaEnabled
    {
        get => satVsLumaEnabled;
        set => Set(ref satVsLumaEnabled, value);
    }
    private bool satVsLumaEnabled = true;

    public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        => new CustomCurveProcessor(devices, this);

    public override IEnumerable<string> CreateExoVideoFilters(
        int keyFrameIndex, ExoOutputDescription exoOutputDescription) => Array.Empty<string>();

    protected override IEnumerable<IAnimatable> GetAnimatables() => new IAnimatable[] { Strength, HueVsHueStrength, HueVsSatStrength, HueVsLumaStrength, LumaVsSatStrength, SatVsSatStrength, SatVsLumaStrength };
}
