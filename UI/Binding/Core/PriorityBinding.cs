using System.Collections.Generic;

namespace InkkSlinger;

public sealed class PriorityBinding : BindingBase
{
    public IList<Binding> Bindings { get; } = new List<Binding>();

    public string? BindingGroupName { get; set; }

    public UpdateSourceExceptionFilterCallback? UpdateSourceExceptionFilter { get; set; }
}
