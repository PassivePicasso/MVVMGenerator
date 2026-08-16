using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace MVVM.Generator.Models;

/// <summary>
/// A sequence with structural equality. ImmutableArray&lt;T&gt; compares by
/// underlying reference, which silently defeats incremental caching when used
/// in a model record.
/// </summary>
internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IEnumerable<T>
    where T : IEquatable<T>
{
    private readonly ImmutableArray<T> _values;

    public EquatableArray(ImmutableArray<T> values) => _values = values;

    public static EquatableArray<T> Empty { get; } = new(ImmutableArray<T>.Empty);

    public int Length => _values.IsDefault ? 0 : _values.Length;

    public bool IsEmpty => Length == 0;

    public T this[int index] => _values[index];

    public bool Equals(EquatableArray<T> other)
    {
        if (_values.IsDefault || other._values.IsDefault)
            return _values.IsDefault && other._values.IsDefault;

        return _values.SequenceEqual(other._values);
    }

    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        if (_values.IsDefault) return 0;

        var hash = 17;
        foreach (var value in _values)
        {
            hash = hash * 31 + (value?.GetHashCode() ?? 0);
        }
        return hash;
    }

    public IEnumerator<T> GetEnumerator()
    {
        return _values.IsDefault
            ? Enumerable.Empty<T>().GetEnumerator()
            : ((IEnumerable<T>)_values).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

internal static class EquatableArray
{
    public static EquatableArray<T> From<T>(IEnumerable<T> values)
        where T : IEquatable<T>
    {
        return new EquatableArray<T>(values.ToImmutableArray());
    }
}
