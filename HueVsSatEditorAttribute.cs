using System.Windows;
using YukkuriMovieMaker.Commons;

namespace MuchaCurve;

/// <summary>
/// Hue vs Sat エディタをプロパティパネルに表示するための属性。
/// </summary>
public class HueVsSatEditorAttribute : PropertyEditorAttribute2
{
    public override PropertyEditorSize PropertyEditorSize => PropertyEditorSize.FullWidth;

    public HueVsSatEditorAttribute()
    {
        MinHeight = 280;
    }

    public override FrameworkElement Create()
    {
        return new HueVsSatEditorControl();
    }

    public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
    {
        if (control is HueVsSatEditorControl editor && itemProperties.Length > 0)
        {
            editor.SetBinding(itemProperties);
        }
    }

    public override void ClearBindings(FrameworkElement control)
    {
        if (control is HueVsSatEditorControl editor)
        {
            editor.ClearBinding();
        }
    }
}
