using System.Reflection;

namespace Application.UnitTests.Support;

/// <summary>
/// Reflection helpers that prove a mapper copies every property, so a forgotten mapping fails a test
/// instead of silently dropping data.
/// </summary>
public static class MappingContract
{
    private static IEnumerable<PropertyInfo> ReadableProperties(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.GetMethod is not null);

    private static IEnumerable<PropertyInfo> WritableProperties(Type type) =>
        ReadableProperties(type).Where(p => p.SetMethod is { IsPublic: true });

    /// <summary>Creates an object whose every writable property holds a non-default, property-specific value.</summary>
    public static T Filled<T>(bool boolValue = true, params string[] skip) where T : new()
    {
        var instance = new T();
        foreach (PropertyInfo property in WritableProperties(typeof(T)).Where(p => !skip.Contains(p.Name)))
            property.SetValue(instance, ValueFor(property.PropertyType, property.Name, boolValue));
        return instance;
    }

    /// <summary>Same as <see cref="Filled{T}"/>, but with all properties left empty (null / default).</summary>
    public static T Empty<T>() where T : new() => new();

    private static object ValueFor(Type declaredType, string name, bool boolValue)
    {
        Type type = Nullable.GetUnderlyingType(declaredType) ?? declaredType;

        if (type == typeof(string)) return $"{name}-value";
        if (type == typeof(byte[])) return new byte[] { 1, 2, 3 };
        if (type == typeof(Guid)) return Guid.NewGuid();
        if (type == typeof(DateTime)) return new DateTime(2026, 2, 3, 4, 5, 6, DateTimeKind.Utc);
        if (type == typeof(DateOnly)) return new DateOnly(1991, 2, 3);
        if (type == typeof(int)) return 100 + name.Length;
        if (type == typeof(decimal)) return 1000m + name.Length + 0.25m;
        if (type == typeof(bool)) return boolValue;
        if (type.IsEnum) return Enum.GetValues(type).Cast<object>().Last();

        // A new property type must be added here on purpose, otherwise the contract tests cannot check it.
        throw new NotSupportedException($"No test value defined for {type.Name} (property '{name}').");
    }

    /// <summary>Every readable property of <paramref name="source"/> must have an equal value on <paramref name="target"/>.</summary>
    public static void AssertAllSourcePropertiesCopied(object source, object target, params string[] except)
    {
        foreach (PropertyInfo sourceProperty in ReadableProperties(source.GetType()))
        {
            if (except.Contains(sourceProperty.Name)) continue;

            PropertyInfo? targetProperty = target.GetType().GetProperty(sourceProperty.Name);
            targetProperty.ShouldNotBeNull($"{target.GetType().Name} has no property '{sourceProperty.Name}' that {source.GetType().Name} provides.");

            targetProperty.GetValue(target).ShouldBe(
                sourceProperty.GetValue(source),
                $"'{sourceProperty.Name}' was not copied from {source.GetType().Name} to {target.GetType().Name}.");
        }
    }

    /// <summary>Every property of <paramref name="target"/> must exist on <paramref name="source"/> (no property filled from nowhere).</summary>
    public static void AssertAllTargetPropertiesHaveASource(Type source, Type target)
    {
        foreach (PropertyInfo targetProperty in ReadableProperties(target))
        {
            source.GetProperty(targetProperty.Name).ShouldNotBeNull(
                $"{target.Name}.{targetProperty.Name} has no matching property on {source.Name}, so it would always be empty.");
        }
    }

    /// <summary>
    /// Every writable property of the entity must be either settable through the command or explicitly declared
    /// as system-managed. Fails when a new entity property is added but nobody decided how it is filled.
    /// </summary>
    public static void AssertEntityPropertiesAreAccountedFor(Type entity, Type command, IEnumerable<string> systemManaged)
    {
        foreach (PropertyInfo property in WritableProperties(entity))
        {
            bool inCommand = command.GetProperty(property.Name) is not null;
            bool declaredSystemManaged = systemManaged.Contains(property.Name);

            (inCommand || declaredSystemManaged).ShouldBeTrue(
                $"{entity.Name}.{property.Name} is neither on {command.Name} nor declared system-managed. " +
                "Add it to the command (and validator, response, mapper) or to the system-managed list.");

            (inCommand && declaredSystemManaged).ShouldBeFalse(
                $"{entity.Name}.{property.Name} is on {command.Name} but also declared system-managed.");
        }
    }
}
