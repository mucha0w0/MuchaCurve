using System.Windows;
using YukkuriMovieMaker.Commons;

namespace MuchaCurve;

/// <summary>
/// Luma vs Sat エディタをプロパティパネルに表示するための属性。
/// </summary>
public class LumaVsSatEditorAttribute : PropertyEditorAttribute2
{
    public override PropertyEditorSize PropertyEditorSize => PropertyEditorSize.FullWidth;

    public LumaVsSatEditorAttribute()
    {
        MinHeight = 280;
    }

    public override FrameworkElement Create()
    {
        return new LumaVsSatEditorControl();
    }

    public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
    {
        if (control is LumaVsSatEditorControl editor && itemProperties.Length > 0)
        {
            editor.SetBinding(itemProperties);
        }
    }

    public override void ClearBindings(FrameworkElement control)
    {
        if (control is LumaVsSatEditorControl editor)
        {
            editor.ClearBinding();
        }
    }
}
