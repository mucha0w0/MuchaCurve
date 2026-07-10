using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace MuchaCurve;

public class CustomCurveProcessor : VideoEffectProcessorBase
{
    private readonly CustomCurveEffect effect;
    private IGraphicsDevicesAndContext? devices;

    // === Stage 1: Custom Curves ===
    private LookupTable3D? lutEffect;
    private CrossFade? crossFade;
    private ID2D1LookupTable3D? lutResource;

    // === Stage 2: Hue vs Hue ===
    private LookupTable3D? hvhLutEffect;
    private CrossFade? hvhCrossFade;
    private ID2D1LookupTable3D? hvhLutResource;

    // === Stage 3: Hue vs Sat ===
    private LookupTable3D? hvsLutEffect;
    private CrossFade? hvsCrossFade;
    private ID2D1LookupTable3D? hvsLutResource;

    // === Stage 4: Hue vs Luma ===
    private LookupTable3D? hvlLutEffect;
    private CrossFade? hvlCrossFade;
    private ID2D1LookupTable3D? hvlLutResource;

    // === Stage 5: Luma vs Sat ===
    private LookupTable3D? lvsLutEffect;
    private CrossFade? lvsCrossFade;
    private ID2D1LookupTable3D? lvsLutResource;

    // === Stage 6: Sat vs Sat ===
    private LookupTable3D? svsLutEffect;
    private CrossFade? svsCrossFade;
    private ID2D1LookupTable3D? svsLutResource;

    // === Stage 7: Sat vs Luma ===
    private LookupTable3D? svlLutEffect;
    private CrossFade? svlCrossFade;
    private ID2D1LookupTable3D? svlLutResource;

    // パラメータ変更を検知するためのキャッシュ
    private string lastCurveDataJson = string.Empty;
    private string lastHvhDataJson = string.Empty;
    private string lastHvsDataJson = string.Empty;
    private string lastHvlDataJson = string.Empty;
    private string lastLvsDataJson = string.Empty;
    private string lastSvsDataJson = string.Empty;
    private string lastSvlDataJson = string.Empty;
    private bool lutReady;
    private bool hvhLutReady;
    private bool hvsLutReady;
    private bool hvlLutReady;
    private bool lvsLutReady;
    private bool svsLutReady;
    private bool svlLutReady;

    public CustomCurveProcessor(IGraphicsDevicesAndContext devices, CustomCurveEffect effect)
        : base(devices)
    {
        this.effect = effect;
    }

    protected override ID2D1Image CreateEffect(IGraphicsDevicesAndContext devices)
    {
        this.devices = devices;
        var dc = devices.DeviceContext;

        // === Stage 1: Custom Curves ===
        lutEffect = new LookupTable3D(dc);
        disposer.Collect(lutEffect);

        crossFade = new CrossFade(dc);
        disposer.Collect(crossFade);

        // Chain: Input → LUT1 → CrossFade1[0], Input → CrossFade1[1]
        crossFade.SetInput(0, lutEffect.Output, true);

        // === Stage 2: Hue vs Hue ===
        hvhLutEffect = new LookupTable3D(dc);
        disposer.Collect(hvhLutEffect);

        hvhCrossFade = new CrossFade(dc);
        disposer.Collect(hvhCrossFade);

        // Chain: CrossFade1.Output → LUT2 → CrossFade2[0], CrossFade1.Output → CrossFade2[1]
        hvhLutEffect.SetInput(0, crossFade.Output, true);
        hvhCrossFade.SetInput(0, hvhLutEffect.Output, true);
        hvhCrossFade.SetInput(1, crossFade.Output, true);

        // === Stage 3: Hue vs Sat ===
        hvsLutEffect = new LookupTable3D(dc);
        disposer.Collect(hvsLutEffect);

        hvsCrossFade = new CrossFade(dc);
        disposer.Collect(hvsCrossFade);

        // Chain: CrossFade2.Output → LUT3 → CrossFade3[0], CrossFade2.Output → CrossFade3[1]
        hvsLutEffect.SetInput(0, hvhCrossFade.Output, true);
        hvsCrossFade.SetInput(0, hvsLutEffect.Output, true);
        hvsCrossFade.SetInput(1, hvhCrossFade.Output, true);

        // === Stage 4: Hue vs Luma ===
        hvlLutEffect = new LookupTable3D(dc);
        disposer.Collect(hvlLutEffect);

        hvlCrossFade = new CrossFade(dc);
        disposer.Collect(hvlCrossFade);

        // Chain: CrossFade3.Output → LUT4 → CrossFade4[0], CrossFade3.Output → CrossFade4[1]
        hvlLutEffect.SetInput(0, hvsCrossFade.Output, true);
        hvlCrossFade.SetInput(0, hvlLutEffect.Output, true);
        hvlCrossFade.SetInput(1, hvsCrossFade.Output, true);

        // === Stage 5: Luma vs Sat ===
        lvsLutEffect = new LookupTable3D(dc);
        disposer.Collect(lvsLutEffect);

        lvsCrossFade = new CrossFade(dc);
        disposer.Collect(lvsCrossFade);

        // Chain: CrossFade4.Output → LUT5 → CrossFade5[0], CrossFade4.Output → CrossFade5[1]
        lvsLutEffect.SetInput(0, hvlCrossFade.Output, true);
        lvsCrossFade.SetInput(0, lvsLutEffect.Output, true);
        lvsCrossFade.SetInput(1, hvlCrossFade.Output, true);

        // === Stage 6: Sat vs Sat ===
        svsLutEffect = new LookupTable3D(dc);
        disposer.Collect(svsLutEffect);

        svsCrossFade = new CrossFade(dc);
        disposer.Collect(svsCrossFade);

        // Chain: CrossFade5.Output → LUT6 → CrossFade6[0], CrossFade5.Output → CrossFade6[1]
        svsLutEffect.SetInput(0, lvsCrossFade.Output, true);
        svsCrossFade.SetInput(0, svsLutEffect.Output, true);
        svsCrossFade.SetInput(1, lvsCrossFade.Output, true);

        // === Stage 7: Sat vs Luma ===
        svlLutEffect = new LookupTable3D(dc);
        disposer.Collect(svlLutEffect);

        svlCrossFade = new CrossFade(dc);
        disposer.Collect(svlCrossFade);

        // Chain: CrossFade6.Output → LUT7 → CrossFade7[0], CrossFade6.Output → CrossFade7[1]
        svlLutEffect.SetInput(0, svsCrossFade.Output, true);
        svlCrossFade.SetInput(0, svlLutEffect.Output, true);
        svlCrossFade.SetInput(1, svsCrossFade.Output, true);

        return svlCrossFade.Output;
    }

    protected override void setInput(ID2D1Image? input)
    {
        lutEffect?.SetInput(0, input!, true);
        crossFade?.SetInput(1, input!, true);
    }

    protected override void ClearEffectChain()
    {
        lutEffect?.SetInput(0, null!, true);
        crossFade?.SetInput(1, null!, true);
        hvhLutEffect?.SetInput(0, null!, true);
        hvhCrossFade?.SetInput(0, null!, true);
        hvhCrossFade?.SetInput(1, null!, true);
        hvsLutEffect?.SetInput(0, null!, true);
        hvsCrossFade?.SetInput(0, null!, true);
        hvsCrossFade?.SetInput(1, null!, true);
        hvlLutEffect?.SetInput(0, null!, true);
        hvlCrossFade?.SetInput(0, null!, true);
        hvlCrossFade?.SetInput(1, null!, true);
        lvsLutEffect?.SetInput(0, null!, true);
        lvsCrossFade?.SetInput(0, null!, true);
        lvsCrossFade?.SetInput(1, null!, true);
        svsLutEffect?.SetInput(0, null!, true);
        svsCrossFade?.SetInput(0, null!, true);
        svsCrossFade?.SetInput(1, null!, true);
        svlLutEffect?.SetInput(0, null!, true);
        svlCrossFade?.SetInput(0, null!, true);
        svlCrossFade?.SetInput(1, null!, true);
    }

    public override DrawDescription Update(EffectDescription effectDescription)
    {
        var frame = effectDescription.ItemPosition.Frame;
        var duration = effectDescription.ItemDuration.Frame;
        var fps = effectDescription.FPS;

        // === Stage 1: Custom Curves ===
        string currentJson = effect.CurveDataJson;
        if (currentJson != lastCurveDataJson)
        {
            lastCurveDataJson = currentJson;
            UpdateLut();
        }

        double strength = 0.0;
        if (effect.CurveEnabled && lutReady)
            strength = effect.Strength.GetValue(frame, duration, fps) / 100.0;

        if (crossFade != null)
            crossFade.Weight = (float)Math.Clamp(strength, 0.0, 1.0);

        // === Stage 2: Hue vs Hue ===
        string currentHvhJson = effect.HueVsHueDataJson;
        if (currentHvhJson != lastHvhDataJson)
        {
            lastHvhDataJson = currentHvhJson;
            UpdateHvhLut();
        }

        double hvhStrength = 0.0;
        if (effect.HueVsHueEnabled && hvhLutReady)
            hvhStrength = effect.HueVsHueStrength.GetValue(frame, duration, fps) / 100.0;

        if (hvhCrossFade != null)
            hvhCrossFade.Weight = (float)Math.Clamp(hvhStrength, 0.0, 1.0);

        // === Stage 3: Hue vs Sat ===
        string currentHvsJson = effect.HueVsSatDataJson;
        if (currentHvsJson != lastHvsDataJson)
        {
            lastHvsDataJson = currentHvsJson;
            UpdateHvsLut();
        }

        double hvsStrength = 0.0;
        if (effect.HueVsSatEnabled && hvsLutReady)
            hvsStrength = effect.HueVsSatStrength.GetValue(frame, duration, fps) / 100.0;

        if (hvsCrossFade != null)
            hvsCrossFade.Weight = (float)Math.Clamp(hvsStrength, 0.0, 1.0);

        // === Stage 4: Hue vs Luma ===
        string currentHvlJson = effect.HueVsLumaDataJson;
        if (currentHvlJson != lastHvlDataJson)
        {
            lastHvlDataJson = currentHvlJson;
            UpdateHvlLut();
        }

        double hvlStrength = 0.0;
        if (effect.HueVsLumaEnabled && hvlLutReady)
            hvlStrength = effect.HueVsLumaStrength.GetValue(frame, duration, fps) / 100.0;

        if (hvlCrossFade != null)
            hvlCrossFade.Weight = (float)Math.Clamp(hvlStrength, 0.0, 1.0);

        // === Stage 5: Luma vs Sat ===
        string currentLvsJson = effect.LumaVsSatDataJson;
        if (currentLvsJson != lastLvsDataJson)
        {
            lastLvsDataJson = currentLvsJson;
            UpdateLvsLut();
        }

        double lvsStrength = 0.0;
        if (effect.LumaVsSatEnabled && lvsLutReady)
            lvsStrength = effect.LumaVsSatStrength.GetValue(frame, duration, fps) / 100.0;

        if (lvsCrossFade != null)
            lvsCrossFade.Weight = (float)Math.Clamp(lvsStrength, 0.0, 1.0);

        // === Stage 6: Sat vs Sat ===
        string currentSvsJson = effect.SatVsSatDataJson;
        if (currentSvsJson != lastSvsDataJson)
        {
            lastSvsDataJson = currentSvsJson;
            UpdateSvsLut();
        }

        double svsStrength = 0.0;
        if (effect.SatVsSatEnabled && svsLutReady)
            svsStrength = effect.SatVsSatStrength.GetValue(frame, duration, fps) / 100.0;

        if (svsCrossFade != null)
            svsCrossFade.Weight = (float)Math.Clamp(svsStrength, 0.0, 1.0);

        // === Stage 7: Sat vs Luma ===
        string currentSvlJson = effect.SatVsLumaDataJson;
        if (currentSvlJson != lastSvlDataJson)
        {
            lastSvlDataJson = currentSvlJson;
            UpdateSvlLut();
        }

        double svlStrength = 0.0;
        if (effect.SatVsLumaEnabled && svlLutReady)
            svlStrength = effect.SatVsLumaStrength.GetValue(frame, duration, fps) / 100.0;

        if (svlCrossFade != null)
            svlCrossFade.Weight = (float)Math.Clamp(svlStrength, 0.0, 1.0);

        return effectDescription.DrawDescription;
    }

    private void UpdateLut()
    {
        if (devices == null) return;

        var curveData = CustomCurveData.Deserialize(lastCurveDataJson);

        // 恒等カーブ → LUT不要 (パススルーで処理負荷ゼロ)
        if (curveData.IsIdentity())
        {
            DisposeLut();
            return;
        }

        var lutBytes = CurveLutGenerator.GenerateLut(curveData);

        // 既存の LUT リソースを破棄
        DisposeLut();

        var dc = devices.DeviceContext;
        lutResource = dc.CreateLookupTable3D(
            BufferPrecision.PerChannel32Float,
            CurveLutGenerator.GetExtents(),
            lutBytes,
            lutBytes.Length,
            CurveLutGenerator.GetStrides());

        if (lutEffect != null && lutResource != null)
        {
            lutEffect.LUT = lutResource;
            lutReady = true;
        }
    }

    private void DisposeLut()
    {
        if (lutEffect != null)
            lutEffect.LUT = null!;
        lutResource?.Dispose();
        lutResource = null;
        lutReady = false;
    }

    private void UpdateHvhLut()
    {
        if (devices == null) return;

        var hueData = HueVsHueData.Deserialize(lastHvhDataJson);

        if (hueData.IsIdentity())
        {
            DisposeHvhLut();
            return;
        }

        var lutBytes = HueVsHueLutGenerator.GenerateLut(hueData);

        DisposeHvhLut();

        var dc = devices.DeviceContext;
        hvhLutResource = dc.CreateLookupTable3D(
            BufferPrecision.PerChannel32Float,
            CurveLutGenerator.GetExtents(),
            lutBytes,
            lutBytes.Length,
            CurveLutGenerator.GetStrides());

        if (hvhLutEffect != null && hvhLutResource != null)
        {
            hvhLutEffect.LUT = hvhLutResource;
            hvhLutReady = true;
        }
    }

    private void DisposeHvhLut()
    {
        if (hvhLutEffect != null)
            hvhLutEffect.LUT = null!;
        hvhLutResource?.Dispose();
        hvhLutResource = null;
        hvhLutReady = false;
    }

    private void UpdateHvsLut()
    {
        if (devices == null) return;

        var satData = HueVsSatData.Deserialize(lastHvsDataJson);

        if (satData.IsIdentity())
        {
            DisposeHvsLut();
            return;
        }

        var lutBytes = HueVsSatLutGenerator.GenerateLut(satData);

        DisposeHvsLut();

        var dc = devices.DeviceContext;
        hvsLutResource = dc.CreateLookupTable3D(
            BufferPrecision.PerChannel32Float,
            CurveLutGenerator.GetExtents(),
            lutBytes,
            lutBytes.Length,
            CurveLutGenerator.GetStrides());

        if (hvsLutEffect != null && hvsLutResource != null)
        {
            hvsLutEffect.LUT = hvsLutResource;
            hvsLutReady = true;
        }
    }

    private void DisposeHvsLut()
    {
        if (hvsLutEffect != null)
            hvsLutEffect.LUT = null!;
        hvsLutResource?.Dispose();
        hvsLutResource = null;
        hvsLutReady = false;
    }

    private void UpdateHvlLut()
    {
        if (devices == null) return;

        var lumaData = HueVsLumaData.Deserialize(lastHvlDataJson);

        if (lumaData.IsIdentity())
        {
            DisposeHvlLut();
            return;
        }

        var lutBytes = HueVsLumaLutGenerator.GenerateLut(lumaData);

        DisposeHvlLut();

        var dc = devices.DeviceContext;
        hvlLutResource = dc.CreateLookupTable3D(
            BufferPrecision.PerChannel32Float,
            CurveLutGenerator.GetExtents(),
            lutBytes,
            lutBytes.Length,
            CurveLutGenerator.GetStrides());

        if (hvlLutEffect != null && hvlLutResource != null)
        {
            hvlLutEffect.LUT = hvlLutResource;
            hvlLutReady = true;
        }
    }

    private void DisposeHvlLut()
    {
        if (hvlLutEffect != null)
            hvlLutEffect.LUT = null!;
        hvlLutResource?.Dispose();
        hvlLutResource = null;
        hvlLutReady = false;
    }

    private void UpdateLvsLut()
    {
        if (devices == null) return;

        var satData = LumaVsSatData.Deserialize(lastLvsDataJson);

        if (satData.IsIdentity())
        {
            DisposeLvsLut();
            return;
        }

        var lutBytes = LumaVsSatLutGenerator.GenerateLut(satData);

        DisposeLvsLut();

        var dc = devices.DeviceContext;
        lvsLutResource = dc.CreateLookupTable3D(
            BufferPrecision.PerChannel32Float,
            CurveLutGenerator.GetExtents(),
            lutBytes,
            lutBytes.Length,
            CurveLutGenerator.GetStrides());

        if (lvsLutEffect != null && lvsLutResource != null)
        {
            lvsLutEffect.LUT = lvsLutResource;
            lvsLutReady = true;
        }
    }

    private void DisposeLvsLut()
    {
        if (lvsLutEffect != null)
            lvsLutEffect.LUT = null!;
        lvsLutResource?.Dispose();
        lvsLutResource = null;
        lvsLutReady = false;
    }

    private void UpdateSvsLut()
    {
        if (devices == null) return;

        var satData = SatVsSatData.Deserialize(lastSvsDataJson);

        if (satData.IsIdentity())
        {
            DisposeSvsLut();
            return;
        }

        var lutBytes = SatVsSatLutGenerator.GenerateLut(satData);

        DisposeSvsLut();

        var dc = devices.DeviceContext;
        svsLutResource = dc.CreateLookupTable3D(
            BufferPrecision.PerChannel32Float,
            CurveLutGenerator.GetExtents(),
            lutBytes,
            lutBytes.Length,
            CurveLutGenerator.GetStrides());

        if (svsLutEffect != null && svsLutResource != null)
        {
            svsLutEffect.LUT = svsLutResource;
            svsLutReady = true;
        }
    }

    private void DisposeSvsLut()
    {
        if (svsLutEffect != null)
            svsLutEffect.LUT = null!;
        svsLutResource?.Dispose();
        svsLutResource = null;
        svsLutReady = false;
    }

    private void UpdateSvlLut()
    {
        if (devices == null) return;

        var lumaData = SatVsLumaData.Deserialize(lastSvlDataJson);

        if (lumaData.IsIdentity())
        {
            DisposeSvlLut();
            return;
        }

        var lutBytes = SatVsLumaLutGenerator.GenerateLut(lumaData);

        DisposeSvlLut();

        var dc = devices.DeviceContext;
        svlLutResource = dc.CreateLookupTable3D(
            BufferPrecision.PerChannel32Float,
            CurveLutGenerator.GetExtents(),
            lutBytes,
            lutBytes.Length,
            CurveLutGenerator.GetStrides());

        if (svlLutEffect != null && svlLutResource != null)
        {
            svlLutEffect.LUT = svlLutResource;
            svlLutReady = true;
        }
    }

    private void DisposeSvlLut()
    {
        if (svlLutEffect != null)
            svlLutEffect.LUT = null!;
        svlLutResource?.Dispose();
        svlLutResource = null;
        svlLutReady = false;
    }

    protected override void Dispose(bool disposing)
    {
        DisposeLut();
        DisposeHvhLut();
        DisposeHvsLut();
        DisposeHvlLut();
        DisposeLvsLut();
        DisposeSvsLut();
        DisposeSvlLut();
        base.Dispose(disposing);
    }
}
