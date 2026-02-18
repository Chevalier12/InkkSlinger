using System;

namespace InkkSlinger;

public interface IBindingExpression : IDisposable
{
    void UpdateTarget();

    void UpdateSource();

    void OnTargetTreeChanged();
}
