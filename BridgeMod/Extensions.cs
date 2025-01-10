using System;
using System.Reflection;

namespace BridgeMod
{
    public static class ReflectionUtils
    {
        public static U GetStaticField<T, U>(string fieldName)
        {
            var type = typeof(T);
            var field = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
            var value = field.GetValue(null);
            return (U)value;
        }

        public static U SetStaticField<T, U>(string fieldName, U value)
        {
            var type = typeof(T);
            var field = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
            field.SetValue(null, value);
            return value;
        }
    }

    public static class ReflectionUtilsExtensions
    {
        public static T GetPrivateField<T>(this object obj, string fieldName)
        {
            var type = obj.GetType();
            var field = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            var value = field.GetValue(obj);
            return (T)value;
        }

        public static T SetPrivateField<T>(this object obj, string fieldName, T value)
        {
            var type = obj.GetType();
            var field = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(obj, value);
            return value;
        }
    }
}
