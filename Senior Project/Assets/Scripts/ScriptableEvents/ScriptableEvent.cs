using System;
using UnityEngine;

public abstract class ScriptableEvent<T> : ScriptableObject
{
    public event Action<T> OnEventFired = delegate { };

    public void FireEvent(T parameter)
    {
        OnEventFired.Invoke(parameter);
    }
}