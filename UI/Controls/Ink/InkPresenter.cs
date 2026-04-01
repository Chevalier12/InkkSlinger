using System.Windows;
using System.Windows.Media;

namespace InkkSlinger.UI.Controls.Ink
{
    public class InkPresenter : FrameworkElement
    {
        public static readonly DependencyProperty StrokesProperty =
            DependencyProperty.Register(nameof(Strokes), typeof(StrokeCollection), typeof(InkPresenter),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public StrokeCollection Strokes
        {
            get => (StrokeCollection)GetValue(StrokesProperty);
            set => SetValue(StrokesProperty, value);
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            if (Strokes == null) return;
            foreach (var stroke in Strokes)
            {
                var geo = stroke.GetGeometry();
                if (geo != Geometry.Empty)
                    dc.DrawGeometry(null, new Pen(new SolidColorBrush(stroke.DrawingAttributes.Color), stroke.DrawingAttributes.Width), geo);
            }
        }
    }
}
