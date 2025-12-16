using System;

namespace Security.Api.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class ResourceAttribute : Attribute
{
    public string Name { get; }
    public ResourceAttribute(string name) => Name = name;
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class OperationAttribute : Attribute
{
    public string Name { get; }
    public OperationAttribute(string name) => Name = name;
}