// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

namespace GoogleAdk.Core.Abstractions.Sessions;

/// <summary>
/// A state mapping that maintains the current value and the pending-commit delta.
/// </summary>
public class State
{
    public const string AppPrefix = "app:";
    public const string UserPrefix = "user:";
    public const string TempPrefix = "temp:";

    private readonly Dictionary<string, object?> _value;
    private readonly Dictionary<string, object?> _delta;

    public State(Dictionary<string, object?>? value = null, Dictionary<string, object?>? delta = null)
    {
        _value = value ?? new Dictionary<string, object?>();
        _delta = delta ?? new Dictionary<string, object?>();
    }

    /// <summary>
    /// Returns the value for the given key. Checks delta first, then value.
    /// </summary>
    public T? Get<T>(string key, T? defaultValue = default)
    {
        if (_delta.TryGetValue(key, out var deltaVal))
            return deltaVal is T typed ? typed : defaultValue;

        if (_value.TryGetValue(key, out var val))
            return val is T typedVal ? typedVal : defaultValue;

        return defaultValue;
    }

    /// <summary>
    /// Sets the value for the given key in both value and delta.
    /// </summary>
    public void Set(string key, object? value)
    {
        _value[key] = value;
        _delta[key] = value;
    }

    /// <summary>
    /// Whether the key exists in value or delta.
    /// </summary>
    public bool Has(string key)
    {
        return _value.ContainsKey(key) || _delta.ContainsKey(key);
    }

    /// <summary>
    /// Whether the state has any pending delta.
    /// </summary>
    public bool HasDelta()
    {
        return _delta.Count > 0;
    }

    /// <summary>
    /// Updates the state with the given delta.
    /// </summary>
    public void Update(Dictionary<string, object?> delta)
    {
        foreach (var kv in delta)
        {
            _delta[kv.Key] = kv.Value;
            _value[kv.Key] = kv.Value;
        }
    }

    /// <summary>
    /// Returns the state as a plain dictionary.
    /// </summary>
    public Dictionary<string, object?> ToRecord()
    {
        var result = new Dictionary<string, object?>(_value);
        foreach (var kv in _delta)
            result[kv.Key] = kv.Value;
        return result;
    }

    /// <summary>
    /// Returns the current pending delta.
    /// </summary>
    public Dictionary<string, object?> GetDelta() => new(_delta);

    /// <summary>
    /// Clears the pending delta.
    /// </summary>
    public void ClearDelta() => _delta.Clear();
}
