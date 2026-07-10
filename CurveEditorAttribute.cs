using System.Windows;
using YukkuriMovieMaker.Commons;

namespace MuchaCurve;

/// <summary>
/// カーブエディタをプロパティパネルに表示するための属性。
/// PropertyEditorAttribute2 を継承し、CurveEditorControl を生成する。
/// </summary>
public class CurveEditorAttribute : PropertyEditorAttribute2
{
    public override PropertyEditorSize PropertyEditorSize => PropertyEditorSize.FullWidth;

    public CurveEditorAttribute()
    {
        MinHeight = 280;
    }

    public override FrameworkElement Create()
    {
        return new CurveEditorControl();
    }

    public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
    {
        if (control is CurveEditorControl editor && itemProperties.Length > 0)
        {
            editor.SetBinding(itemProperties);
        }
    }

    public override void ClearBindings(FrameworkElement control)
    {
        if (control is CurveEditorControl editor)
        {
            editor.ClearBinding();
        }
    }
}
