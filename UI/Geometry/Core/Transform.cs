using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public abstract class Transform : Freezable
{
    public new Transform Clone()
    {
        return (Transform)base.Clone();
    }

    public new Transform CloneCurrentValue()
    {
        return (Transform)base.CloneCurrentValue();
    }

    public abstract Matrix ToMatrix();

    public Vector2 TransformPoint(Vector2 point)
    {
        return Vector2.Transform(point, ToMatrix());
    }

    protected static bool NearlyEqual(float left, float right)
    {
        return MathF.Abs(left - right) <= 0.0001f;
    }

    protected bool SetField(ref float field, float value)
    {
        WritePreamble();
        if (NearlyEqual(field, value))
        {
            return false;
        }

        field = value;
        WritePostscript();
        return true;
    }
}

public sealed class MatrixTransform : Transform
{
    private Matrix _matrix;

    public MatrixTransform()
        : this(Matrix.Identity)
    {
    }

    public MatrixTransform(Matrix matrix)
    {
        _matrix = matrix;
    }

    public Matrix Matrix
    {
        get => _matrix;
        set
        {
            WritePreamble();
            if (_matrix == value)
            {
                return;
            }

            _matrix = value;
            WritePostscript();
        }
    }

    protected override Freezable CreateInstanceCore()
    {
        return new MatrixTransform();
    }

    protected override void CloneCore(Freezable source)
    {
        var typedSource = (MatrixTransform)source;
        _matrix = typedSource._matrix;
    }

    public override Matrix ToMatrix()
    {
        return Matrix;
    }
}

public sealed class TranslateTransform : Transform
{
    private float _x;
    private float _y;

    public float X
    {
        get => _x;
        set => _ = SetField(ref _x, value);
    }

    public float Y
    {
        get => _y;
        set => _ = SetField(ref _y, value);
    }

    protected override Freezable CreateInstanceCore()
    {
        return new TranslateTransform();
    }

    protected override void CloneCore(Freezable source)
    {
        var typedSource = (TranslateTransform)source;
        _x = typedSource._x;
        _y = typedSource._y;
    }

    public override Matrix ToMatrix()
    {
        return Matrix.CreateTranslation(X, Y, 0f);
    }
}

public sealed class ScaleTransform : Transform
{
    private float _scaleX = 1f;
    private float _scaleY = 1f;
    private float _centerX;
    private float _centerY;

    public float ScaleX
    {
        get => _scaleX;
        set => _ = SetField(ref _scaleX, value);
    }

    public float ScaleY
    {
        get => _scaleY;
        set => _ = SetField(ref _scaleY, value);
    }

    public float CenterX
    {
        get => _centerX;
        set => _ = SetField(ref _centerX, value);
    }

    public float CenterY
    {
        get => _centerY;
        set => _ = SetField(ref _centerY, value);
    }

    protected override Freezable CreateInstanceCore()
    {
        return new ScaleTransform();
    }

    protected override void CloneCore(Freezable source)
    {
        var typedSource = (ScaleTransform)source;
        _scaleX = typedSource._scaleX;
        _scaleY = typedSource._scaleY;
        _centerX = typedSource._centerX;
        _centerY = typedSource._centerY;
    }

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
    private float _angle;
    private float _centerX;
    private float _centerY;

    public float Angle
    {
        get => _angle;
        set => _ = SetField(ref _angle, value);
    }

    public float CenterX
    {
        get => _centerX;
        set => _ = SetField(ref _centerX, value);
    }

    public float CenterY
    {
        get => _centerY;
        set => _ = SetField(ref _centerY, value);
    }

    protected override Freezable CreateInstanceCore()
    {
        return new RotateTransform();
    }

    protected override void CloneCore(Freezable source)
    {
        var typedSource = (RotateTransform)source;
        _angle = typedSource._angle;
        _centerX = typedSource._centerX;
        _centerY = typedSource._centerY;
    }

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
    private float _angleX;
    private float _angleY;
    private float _centerX;
    private float _centerY;

    public float AngleX
    {
        get => _angleX;
        set => _ = SetField(ref _angleX, value);
    }

    public float AngleY
    {
        get => _angleY;
        set => _ = SetField(ref _angleY, value);
    }

    public float CenterX
    {
        get => _centerX;
        set => _ = SetField(ref _centerX, value);
    }

    public float CenterY
    {
        get => _centerY;
        set => _ = SetField(ref _centerY, value);
    }

    protected override Freezable CreateInstanceCore()
    {
        return new SkewTransform();
    }

    protected override void CloneCore(Freezable source)
    {
        var typedSource = (SkewTransform)source;
        _angleX = typedSource._angleX;
        _angleY = typedSource._angleY;
        _centerX = typedSource._centerX;
        _centerY = typedSource._centerY;
    }

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
    private readonly TransformCollection _children;

    public TransformGroup()
    {
        _children = new TransformCollection(this, OnChildrenChanged);
    }

    public IList<Transform> Children => _children;

    protected override Freezable CreateInstanceCore()
    {
        return new TransformGroup();
    }

    protected override void CloneCore(Freezable source)
    {
        var typedSource = (TransformGroup)source;
        _children.Clear();
        foreach (var child in typedSource.Children)
        {
            _children.Add(child.Clone());
        }
    }

    protected override bool FreezeCore(bool isChecking)
    {
        foreach (var child in _children)
        {
            if (!FreezeValue(child, isChecking))
            {
                return false;
            }
        }

        return true;
    }

    public override Matrix ToMatrix()
    {
        var matrix = Matrix.Identity;
        foreach (var transform in Children)
        {
            matrix *= transform.ToMatrix();
        }

        return matrix;
    }

    private void OnChildrenChanged()
    {
        WritePostscript();
    }

    private sealed class TransformCollection : IList<Transform>
    {
        private readonly TransformGroup _owner;
        private readonly List<Transform> _items = new();
        private readonly Action _onChanged;

        public TransformCollection(TransformGroup owner, Action onChanged)
        {
            _owner = owner;
            _onChanged = onChanged;
        }

        public Transform this[int index]
        {
            get => _items[index];
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                _owner.WritePreamble();
                var existing = _items[index];
                if (ReferenceEquals(existing, value))
                {
                    return;
                }

                Detach(existing);
                _items[index] = value;
                Attach(value);
                _onChanged();
            }
        }

        public int Count => _items.Count;

        public bool IsReadOnly => false;

        public void Add(Transform item)
        {
            ArgumentNullException.ThrowIfNull(item);
            _owner.WritePreamble();
            _items.Add(item);
            Attach(item);
            _onChanged();
        }

        public void Clear()
        {
            _owner.WritePreamble();
            if (_items.Count == 0)
            {
                return;
            }

            for (var i = 0; i < _items.Count; i++)
            {
                Detach(_items[i]);
            }

            _items.Clear();
            _onChanged();
        }

        public bool Contains(Transform item)
        {
            return _items.Contains(item);
        }

        public void CopyTo(Transform[] array, int arrayIndex)
        {
            _items.CopyTo(array, arrayIndex);
        }

        public IEnumerator<Transform> GetEnumerator()
        {
            return _items.GetEnumerator();
        }

        public int IndexOf(Transform item)
        {
            return _items.IndexOf(item);
        }

        public void Insert(int index, Transform item)
        {
            ArgumentNullException.ThrowIfNull(item);
            _owner.WritePreamble();
            _items.Insert(index, item);
            Attach(item);
            _onChanged();
        }

        public bool Remove(Transform item)
        {
            _owner.WritePreamble();
            if (!_items.Remove(item))
            {
                return false;
            }

            Detach(item);
            _onChanged();
            return true;
        }

        public void RemoveAt(int index)
        {
            _owner.WritePreamble();
            var item = _items[index];
            _items.RemoveAt(index);
            Detach(item);
            _onChanged();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _items.GetEnumerator();
        }

        private void Attach(Transform transform)
        {
            transform.Changed += OnChildChanged;
        }

        private void Detach(Transform transform)
        {
            transform.Changed -= OnChildChanged;
        }

        private void OnChildChanged()
        {
            _onChanged();
        }
    }
}
