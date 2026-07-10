using System.Windows;
using YukkuriMovieMaker.Commons;

namespace MuchaCurve;

/// <summary>
/// Hue vs Hue エディタをプロパティパネルに表示するための属性。
/// </summary>
public class HueVsHueEditorAttribute : PropertyEditorAttribute2
{
    public override PropertyEditorSize PropertyEditorSize => PropertyEditorSize.FullWidth;

    public HueVsHueEditorAttribute()
    {
        MinHeight = 280;
    }

    public override FrameworkElement Create()
    {
        return new HueVsHueEditorControl();
    }

    public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
    {
        if (control is HueVsHueEditorControl editor && itemProperties.Length > 0)
        {
            editor.SetBinding(itemProperties);
        }
    }

    public override void ClearBindings(FrameworkElement control)
    {
        if (control is HueVsHueEditorControl editor)
        {
            editor.ClearBinding();
        }
    }
}
