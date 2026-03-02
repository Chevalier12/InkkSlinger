using System;
using System.IO;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public partial class DatePickerView : UserControl
{
    public DatePickerView()
    {
        var markupPath = Path.Combine(AppContext.BaseDirectory, "Views", "DatePickerView.xml");
        XamlLoader.LoadInto(this, markupPath, this);
    }

    public void SetFont(SpriteFont? font)
    {
        if (font == null)
        {
            return;
        }

        ControlDemoSupport.ApplyFontRecursive(this, font);
    }
}

