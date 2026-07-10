using System.Windows;
using YukkuriMovieMaker.Commons;

namespace MuchaCurve;

/// <summary>
/// Hue vs Luma エディタをプロパティパネルに表示するための属性。
/// </summary>
public class HueVsLumaEditorAttribute : PropertyEditorAttribute2
{
    public override PropertyEditorSize PropertyEditorSize => PropertyEditorSize.FullWidth;

    public HueVsLumaEditorAttribute()
    {
        MinHeight = 280;
    }

    public override FrameworkElement Create()
    {
        return new HueVsLumaEditorControl();
    }

    public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
    {
        if (control is HueVsLumaEditorControl editor && itemProperties.Length > 0)
        {
            editor.SetBinding(itemProperties);
        }
    }

    public override void ClearBindings(FrameworkElement control)
    {
        if (control is HueVsLumaEditorControl editor)
        {
            editor.ClearBinding();
        }
    }
}
