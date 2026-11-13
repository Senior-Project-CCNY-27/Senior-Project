using System;
using UnityEngine;

public abstract class ScriptableEvent<T> : ScriptableObject
{
    public event Action<T> OnEventFired = delegate { };

    private void OnDisable()
    {
        OnEventFired = delegate { };
    }

    public void FireEvent(T parameter)
    {
        OnEventFired?.Invoke(parameter);
    }
}