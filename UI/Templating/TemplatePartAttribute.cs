using System;

namespace InkkSlinger;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public sealed class TemplatePartAttribute : Attribute
{
    public TemplatePartAttribute(string name, Type type)
    {
        Name = name;
        Type = type;
    }

    public string Name { get; }

    public Type Type { get; }
}
