using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

namespace X2Model
{
    public class ReflectionHelper
    {
        public static U GetField<T, U>(T obj, string fieldName)
        {
            FieldInfo fi = typeof(T).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            return (U)fi.GetValue(obj);
        }

        public static void SetField<T>(T obj, string fieldName, object value)
        {
            FieldInfo fi = typeof(T).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            fi.SetValue(obj, value);
        }

        public static T Construct<T>(Type[] ctorTypes, object[] ctorArgs)
        {
            Type typeToCreate = typeof(T);

            ConstructorInfo ci = typeToCreate.GetConstructor(
                                    BindingFlags.NonPublic | BindingFlags.Instance,
                                    null, ctorTypes, new ParameterModifier[0]);

            return (T)ci.Invoke(ctorArgs);
        }

        public static void CallMethod<T>(T obj, string methodName, object[] methodArgs)
        {
            MethodInfo mi = typeof(T).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            mi.Invoke(obj, methodArgs);
        }
    }
}
