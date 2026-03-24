using System;

namespace InkkSlinger;

public abstract class Freezable
{
    private bool _isFrozen;
    private int _batchUpdateDepth;
    private bool _hasPendingChangedDuringBatch;

    internal event Action? Changed;

    public bool IsFrozen => _isFrozen;

    public bool CanFreeze => FreezeCore(isChecking: true);

    public void Freeze()
    {
        if (_isFrozen)
        {
            return;
        }

        if (!FreezeCore(isChecking: true))
        {
            throw new InvalidOperationException($"{GetType().Name} cannot be frozen.");
        }

        _ = FreezeCore(isChecking: false);
        _isFrozen = true;
    }

    public Freezable Clone()
    {
        var clone = CreateInstanceCore();
        clone.CloneCore(this);
        clone._isFrozen = false;
        return clone;
    }

    public Freezable CloneCurrentValue()
    {
        var clone = CreateInstanceCore();
        clone.CloneCurrentValueCore(this);
        clone._isFrozen = false;
        return clone;
    }

    protected void ReadPreamble()
    {
    }

    protected void WritePreamble()
    {
        if (_isFrozen)
        {
            throw new InvalidOperationException($"Cannot modify frozen {GetType().Name}.");
        }
    }

    protected void WritePostscript()
    {
        if (_batchUpdateDepth > 0)
        {
            _hasPendingChangedDuringBatch = true;
            return;
        }

        OnChanged();
    }

    protected virtual void OnChanged()
    {
        Changed?.Invoke();
    }

    protected abstract Freezable CreateInstanceCore();

    protected virtual void CloneCore(Freezable source)
    {
    }

    protected virtual void CloneCurrentValueCore(Freezable source)
    {
        CloneCore(source);
    }

    protected virtual bool FreezeCore(bool isChecking)
    {
        return true;
    }

    protected static bool FreezeValue(Freezable? value, bool isChecking)
    {
        if (value == null || value.IsFrozen)
        {
            return true;
        }

        if (!value.FreezeCore(isChecking: true))
        {
            return false;
        }

        if (!isChecking)
        {
            value.Freeze();
        }

        return true;
    }

    internal void BeginBatchUpdate()
    {
        _batchUpdateDepth++;
    }

    internal void EndBatchUpdate()
    {
        if (_batchUpdateDepth <= 0)
        {
            return;
        }

        _batchUpdateDepth--;
        if (_batchUpdateDepth == 0 && _hasPendingChangedDuringBatch)
        {
            _hasPendingChangedDuringBatch = false;
            OnChanged();
        }
    }
}
