using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

// An Inspector-configured call with fixed, Odin-serialized arguments.
// This complements UnityEvent's dynamic argument binding for methods with several parameters.
[Serializable]
public sealed class SerializedMethodEvent
{
    [OdinSerialize, ListDrawerSettings(ShowFoldout = true)]
    private List<MethodCall> _calls = new();

    public void Invoke()
    {
        if (_calls == null)
            return;
        foreach (MethodCall call in _calls)
            call?.Invoke();
    }

    [Serializable, InlineProperty]
    private sealed class MethodCall
    {
        [SerializeField, OnValueChanged(nameof(RefreshArguments))]
        private UnityEngine.Object _target;
        [SerializeField, ValueDropdown(nameof(GetMethodChoices)), OnValueChanged(nameof(RefreshArguments))]
        private string _methodId;
        [OdinSerialize, ListDrawerSettings(ShowFoldout = true)]
        private List<Argument> _arguments = new();

        [NonSerialized] private MethodInfo _cachedMethod;
        [NonSerialized] private UnityEngine.Object _cachedTarget;
        [NonSerialized] private string _cachedId;
        [NonSerialized] private bool _loggedFailure;

        private IEnumerable<ValueDropdownItem<string>> GetMethodChoices()
        {
            if (_target == null)
                yield break;
            foreach (MethodInfo method in _target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public))
            {
                ParameterInfo[] parameters = method.GetParameters();
                if (method.IsSpecialName || method.ContainsGenericParameters ||
                    parameters.Any(p => !IsSupported(p.ParameterType)))
                    continue;
                string label = method.Name + "(" +
                    string.Join(", ", parameters.Select(p => p.ParameterType.Name + " " + p.Name)) + ")";
                yield return new ValueDropdownItem<string>(label, IdFor(method));
            }
        }

        private void RefreshArguments()
        {
            _cachedMethod = null;
            _loggedFailure = false;
            _arguments ??= new List<Argument>();
            MethodInfo method = ResolveMethod();
            if (method == null)
            {
                _arguments.Clear();
                return;
            }
            ParameterInfo[] parameters = method.GetParameters();
            var old = _arguments;
            _arguments = parameters.Select((p, i) =>
                i < old.Count && old[i].Matches(p) ? old[i] : new Argument(p)).ToList();
        }

        public void Invoke()
        {
            MethodInfo method = ResolveMethod();
            if (method == null)
                return;
            ParameterInfo[] parameters = method.GetParameters();
            if (_arguments == null || _arguments.Count != parameters.Length)
            {
                LogOnce("The saved argument list no longer matches " + method.Name + ".");
                return;
            }
            try
            {
                object[] values = new object[parameters.Length];
                for (int i = 0; i < values.Length; i++)
                    values[i] = _arguments[i].GetValue(parameters[i].ParameterType);
                method.Invoke(_target, values);
            }
            catch (Exception exception)
            {
                if (!_loggedFailure)
                {
                    Debug.LogException(exception is TargetInvocationException invocation
                        ? invocation.InnerException ?? exception : exception, _target);
                    _loggedFailure = true;
                }
            }
        }

        private MethodInfo ResolveMethod()
        {
            if (_target == null || string.IsNullOrEmpty(_methodId))
                return null;
            if (_cachedTarget == _target && _cachedId == _methodId)
                return _cachedMethod;
            _cachedTarget = _target;
            _cachedId = _methodId;
            _cachedMethod = _target.GetType()
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(m => IdFor(m) == _methodId);
            return _cachedMethod;
        }

        private void LogOnce(string message)
        {
            if (_loggedFailure)
                return;
            Debug.LogWarning(message, _target);
            _loggedFailure = true;
        }

        private static string IdFor(MethodInfo method) =>
            method.DeclaringType.FullName + "." + method.Name + "(" +
            string.Join(",", method.GetParameters().Select(p => p.ParameterType.AssemblyQualifiedName)) + ")";

        private static bool IsSupported(Type type) =>
            !type.IsByRef && !type.IsPointer && !type.ContainsGenericParameters &&
            (type.IsSerializable || typeof(UnityEngine.Object).IsAssignableFrom(type));
    }

    [Serializable, InlineProperty]
    private sealed class Argument
    {
        [SerializeField, ReadOnly] private string _name;
        [SerializeField, ReadOnly] private string _typeId;
        [OdinSerialize, ShowIf(nameof(IsUnityObject))] private UnityEngine.Object _unityObject;
        [OdinSerialize, HideIf(nameof(IsUnityObject))] private object _value;

        private bool IsUnityObject =>
            !string.IsNullOrEmpty(_typeId) && Type.GetType(_typeId) is Type type &&
            typeof(UnityEngine.Object).IsAssignableFrom(type);

        public Argument(ParameterInfo parameter)
        {
            _name = parameter.Name;
            _typeId = parameter.ParameterType.AssemblyQualifiedName;
            if (!IsUnityObject)
            {
                Type type = parameter.ParameterType;
                if (type == typeof(string))
                    _value = string.Empty;
                else if (type.IsValueType)
                    _value = Activator.CreateInstance(type);
                else if (type.GetConstructor(Type.EmptyTypes) != null)
                    _value = Activator.CreateInstance(type);
            }
        }

        public bool Matches(ParameterInfo parameter) =>
            _typeId == parameter.ParameterType.AssemblyQualifiedName;

        public object GetValue(Type expectedType)
        {
            object value = IsUnityObject ? _unityObject : _value;
            if (value == null && expectedType.IsValueType)
                return Activator.CreateInstance(expectedType);
            if (value != null && !expectedType.IsInstanceOfType(value))
                throw new ArgumentException(_name + " expects " + expectedType.Name + ".");
            return value;
        }
    }
}
