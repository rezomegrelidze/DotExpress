using System;
using System.Collections.Generic;
using System.Dynamic;

namespace DotExpress.Dynamic
{
    public static class DynamicHelpers
    {
        public static dynamic CreateDynamicObject()
        {
            return new ExpandoObject();
        }

        public static void AddProperty(dynamic obj, string propertyName, object value)
        {
            var expando = obj as IDictionary<string, object>;
            if (expando != null)
            {
                expando[propertyName] = value;
            }
        }

        public static object GetProperty(dynamic obj, string propertyName)
        {
            var expando = obj as IDictionary<string, object>;
            if (expando != null && expando.ContainsKey(propertyName))
            {
                return expando[propertyName];
            }
            return null;
        }
    }
}