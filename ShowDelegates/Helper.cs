using Elements.Core;
using FrooxEngine;
using FrooxEngine.UIX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ShowDelegates;

internal class MethodArgs : IEquatable<MethodArgs>
{
    private readonly Type[] _types;

    public MethodArgs(params Type[] types)
    {
        _types = types;
    }

    public bool Equals(MethodArgs other)
    {
        if (other == null || _types.Length != other._types.Length)
        {
            return false;
        }

        for (int i = 0; i < _types.Length; i++)
        {
            if (_types[i] != other._types[i])
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object obj)
    {
        return Equals(obj as MethodArgs);
    }

    public override int GetHashCode()
    {
        int hash = 17;

        foreach (var argumentType in _types)
        {
            hash = hash * 23 + (argumentType?.GetHashCode() ?? 0);
        }

        return hash;
    }
}


internal class Helper
{
    // Generates a lookup table for all the sync methods in the assembly. The keys of the table
    // are the parameter types for each method, and the values are the delegates that can be
    // used to invoke those methods.
    public static Dictionary<MethodArgs, Type> GenerateArgumentLookup(IEnumerable<Delegate> delegates)
    {
        var argumentLookup = new Dictionary<MethodArgs, Type>();

        foreach (var del in delegates)
        {
            var methodInfo = del.Method;
            var parameterTypes = methodInfo.GetParameters().Select(p => p.ParameterType).ToArray();

            // If the method returns void, we need to return an Action<> that matches its parameters.
            if (methodInfo.ReturnType == typeof(void))
            {
                argumentLookup[new MethodArgs(parameterTypes)] = typeof(Action<>).MakeGenericType(parameterTypes);
            }
            // Otherwise, we need to return a Func<,> that matches its parameters and return type.
            else
            {
                argumentLookup[new MethodArgs(parameterTypes.Concat(new[] { methodInfo.ReturnType }).ToArray())] = typeof(Func<,>).MakeGenericType(parameterTypes.Concat(new[] { methodInfo.ReturnType }).ToArray());
            }
        }

        return argumentLookup;
    }

    public static Type ClassifyDelegate(MethodInfo methodInfo, Dictionary<MethodArgs, Type> argumentLookup)
    {
        // If the method is a known delegate type, return that type.
        if (argumentLookup.TryGetValue(new MethodArgs(methodInfo.GetParameters().Select(p => p.ParameterType).ToArray()), out var type))
        {
            return type;
        }

        // If the method has three parameters, and the first two are IButton and ButtonEventData,
        // return ButtonEventHandler<T>, where T is the third parameter type.
        var parameters = methodInfo.GetParameters();
        if (parameters.Length == 3 &&
            parameters[0].ParameterType == typeof(IButton) &&
            parameters[1].ParameterType == typeof(ButtonEventData))
        {
            return typeof(ButtonEventHandler<>).MakeGenericType(parameters[2].ParameterType);
        }

        // Otherwise, infer the delegate type from the method's return type, if any.
        return GetFuncOrAction(methodInfo);
    }

    public static Type GetFuncOrAction(MethodInfo methodInfo)
    {
        // Get the parameter types.
        Type[] parameterTypes = methodInfo.GetParameters().Select(p => p.ParameterType).ToArray();

        // Return the appropriate delegate type based on the method's return type.
        return methodInfo.ReturnType == typeof(void)
            ? Expression.GetActionType(parameterTypes)
            : Expression.GetFuncType(parameterTypes.Concat(new[] { methodInfo.ReturnType }).ToArray());
    }
}
