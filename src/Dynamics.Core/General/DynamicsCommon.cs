namespace Dynamics.Core.General;

// Port of ClsDynamics.

/// <summary>Status of an iteration.</summary>
public enum IterationStatus
{
    Running,
    Interrupted,
    Stopped,
    Ready,
}

/// <summary>Edit mode, e.g. in L-systems.</summary>
public enum EditMode
{
    None,
    Edit,
    Add,
}

public static class ObjectCopy
{
    /// <summary>Copies all readable and writable properties with the same name from source to target.</summary>
    public static void CopyProperties(object source, object target)
    {
        var targetType = target.GetType();

        foreach (var property in source.GetType().GetProperties())
        {
            if (!property.CanRead || !property.CanWrite || property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            var targetProperty = targetType.GetProperty(property.Name);
            if (targetProperty is { CanWrite: true })
            {
                targetProperty.SetValue(target, property.GetValue(source));
            }
        }
    }
}
