using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace InkkSlinger.UI.Controls.Ink
{
    public class InkCanvas : FrameworkElement
    {
        public static readonly DependencyProperty StrokesProperty =
            DependencyProperty.Register(nameof(Strokes), typeof(StrokeCollection), typeof(InkCanvas),
                new FrameworkPropertyMetadata(new StrokeCollection(), FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty DefaultDrawingAttributesProperty =
            DependencyProperty.Register(nameof(DefaultDrawingAttributes), typeof(DrawingAttributes), typeof(InkCanvas),
                new FrameworkPropertyMetadata(new DrawingAttributes(), FrameworkPropertyMetadataOptions.AffectsRender));

        public StrokeCollection Strokes
        {
            get => (StrokeCollection)GetValue(StrokesProperty);
            set => SetValue(StrokesProperty, value);
        }

        public DrawingAttributes DefaultDrawingAttributes
        {
            get => (DrawingAttributes)GetValue(DefaultDrawingAttributesProperty);
            set => SetValue(DefaultDrawingAttributesProperty, value);
        }

        private StylusPointCollection _currentPoints;
        private bool _isDrawing;

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            foreach (var stroke in Strokes)
            {
                var geo = stroke.GetGeometry();
                if (geo != Geometry.Empty)
                    dc.DrawGeometry(null, new Pen(new SolidColorBrush(stroke.DrawingAttributes.Color), stroke.DrawingAttributes.Width), geo);
            }
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            _isDrawing = true;
            var p = e.GetPosition(this);
            _currentPoints = new StylusPointCollection { new StylusPoint(p.X, p.Y) };
            CaptureMouse();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (!_isDrawing) return;
            var p = e.GetPosition(this);
            _currentPoints.Add(new StylusPoint(p.X, p.Y));
            InvalidateVisual();
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            if (!_isDrawing) return;
            var p = e.GetPosition(this);
            _currentPoints.Add(new StylusPoint(p.X, p.Y));
            Strokes.Add(new Stroke(_currentPoints, DefaultDrawingAttributes));
            _isDrawing = false;
            ReleaseMouseCapture();
            InvalidateVisual();
        }
    }
}
