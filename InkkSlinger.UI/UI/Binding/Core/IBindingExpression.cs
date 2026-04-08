using System;
using System.Collections.Generic;

namespace InkkSlinger;

public interface IBindingExpression : IDisposable
{
    DependencyObject Target { get; }

    DependencyProperty TargetProperty { get; }

    BindingBase Binding { get; }

    void UpdateTarget();

    void UpdateSource();

    void OnTargetTreeChanged();

    bool TryValidateForBindingGroup(List<ValidationError> errors);

    bool TryUpdateSourceForBindingGroup(List<ValidationError> errors);
}
