using System.Windows;
using System.Windows.Media;

namespace InkkSlinger.UI.Controls.Ink
{
    public class Stroke
    {
        public StylusPointCollection StylusPoints { get; }
        public DrawingAttributes DrawingAttributes { get; set; }

        public Stroke(StylusPointCollection stylusPoints, DrawingAttributes drawingAttributes)
        {
            StylusPoints = stylusPoints;
            DrawingAttributes = drawingAttributes;
        }

        public Geometry GetGeometry()
        {
            if (StylusPoints.Count < 2) return Geometry.Empty;
            var fig = new PathFigure { StartPoint = new Point(StylusPoints[0].X, StylusPoints[0].Y) };
            for (int i = 1; i < StylusPoints.Count; i++)
                fig.Segments.Add(new LineSegment(new Point(StylusPoints[i].X, StylusPoints[i].Y), true));
            return new PathGeometry { Figures = { fig } };
        }
    }
}
