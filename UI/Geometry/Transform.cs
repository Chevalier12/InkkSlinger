using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public abstract class Transform
{
    public abstract Matrix ToMatrix();

    public Vector2 TransformPoint(Vector2 point)
    {
        return Vector2.Transform(point, ToMatrix());
    }
}

public sealed class MatrixTransform : Transform
{
    public MatrixTransform()
        : this(Matrix.Identity)
    {
    }

    public MatrixTransform(Matrix matrix)
    {
        Matrix = matrix;
    }

    public Matrix Matrix { get; set; }

    public override Matrix ToMatrix()
    {
        return Matrix;
    }
}

public sealed class TranslateTransform : Transform
{
    public float X { get; set; }

    public float Y { get; set; }

    public override Matrix ToMatrix()
    {
        return Matrix.CreateTranslation(X, Y, 0f);
    }
}

public sealed class ScaleTransform : Transform
{
    public float ScaleX { get; set; } = 1f;

    public float ScaleY { get; set; } = 1f;

    public float CenterX { get; set; }

    public float CenterY { get; set; }

    public override Matrix ToMatrix()
    {
        if (MathF.Abs(CenterX) < 0.0001f && MathF.Abs(CenterY) < 0.0001f)
        {
            return Matrix.CreateScale(ScaleX, ScaleY, 1f);
        }

        return Matrix.CreateTranslation(-CenterX, -CenterY, 0f)
               * Matrix.CreateScale(ScaleX, ScaleY, 1f)
               * Matrix.CreateTranslation(CenterX, CenterY, 0f);
    }
}

public sealed class RotateTransform : Transform
{
    public float Angle { get; set; }

    public float CenterX { get; set; }

    public float CenterY { get; set; }

    public override Matrix ToMatrix()
    {
        var radians = MathHelper.ToRadians(Angle);
        if (MathF.Abs(CenterX) < 0.0001f && MathF.Abs(CenterY) < 0.0001f)
        {
            return Matrix.CreateRotationZ(radians);
        }

        return Matrix.CreateTranslation(-CenterX, -CenterY, 0f)
               * Matrix.CreateRotationZ(radians)
               * Matrix.CreateTranslation(CenterX, CenterY, 0f);
    }
}

public sealed class SkewTransform : Transform
{
    public float AngleX { get; set; }

    public float AngleY { get; set; }

    public float CenterX { get; set; }

    public float CenterY { get; set; }

    public override Matrix ToMatrix()
    {
        var tanX = MathF.Tan(MathHelper.ToRadians(AngleX));
        var tanY = MathF.Tan(MathHelper.ToRadians(AngleY));
        var skew = new Matrix(
            1f, tanY, 0f, 0f,
            tanX, 1f, 0f, 0f,
            0f, 0f, 1f, 0f,
            0f, 0f, 0f, 1f);

        if (MathF.Abs(CenterX) < 0.0001f && MathF.Abs(CenterY) < 0.0001f)
        {
            return skew;
        }

        return Matrix.CreateTranslation(-CenterX, -CenterY, 0f)
               * skew
               * Matrix.CreateTranslation(CenterX, CenterY, 0f);
    }
}

public sealed class TransformGroup : Transform
{
    public List<Transform> Children { get; } = new();

    public override Matrix ToMatrix()
    {
        var matrix = Matrix.Identity;
        foreach (var transform in Children)
        {
            matrix *= transform.ToMatrix();
        }

        return matrix;
    }
}
