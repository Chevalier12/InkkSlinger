namespace InkkSlinger.UI.Controls.Ink
{
    public struct StylusPoint
    {
        public double X { get; }
        public double Y { get; }
        public float PressureFactor { get; }

        public StylusPoint(double x, double y, float pressureFactor = 0.5f)
        {
            X = x;
            Y = y;
            PressureFactor = pressureFactor;
        }
    }
}
