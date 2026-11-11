using System;
using UnityEngine;
using UnityEngine.Events;

public abstract class ScriptableEventListener<TEvent, TEventType> : MonoBehaviour
    where TEvent : ScriptableEvent<TEventType>
{
    [SerializeField] private protected TEvent _event;
    [SerializeField] private protected UnityEvent<TEventType> _onEventFired;

    private void OnEnable()
    {
        if (_event != null)
            _event.OnEventFired += OnEventFired;
    }

    private void OnDisable()
    {
        if (_event != null)
            _event.OnEventFired -= OnEventFired;
    }

    public void OnEventFired(TEventType e)
    {
        _onEventFired?.Invoke(e);
    }
}