using System;
using System.Collections;
using System.Collections.Generic;
#if USING_ZLINQ
using ZLinq;
#else
using System.Linq;
#endif
using System.Reflection;
using System.Diagnostics;


namespace CrowRx.Json
{
    internal static class Deserializer
    {
        private static DefaultActivator? _defaultActivator;


        private static DefaultActivator DefaultActivator => _defaultActivator ??= new DefaultActivator();


        internal static bool TryParse(out object obj, Type objectType, object jsonObject, ICustomActivator activator)
        {
            activator ??= DefaultActivator;

            (bool isSuccess, object result) = Parse(null, objectType, jsonObject, activator, false);

            obj = result;

            return isSuccess;
        }

        internal static bool TryParseOverwrite(ref object obj, Type objectType, object jsonObject, ICustomActivator activator, bool isAppend)
        {
            activator ??= DefaultActivator;

            (bool isSuccess, object result) = Parse(obj, objectType, jsonObject, activator, isAppend);

            if (isSuccess)
            {
                obj = result;

                return true;
            }

            return false;
        }

        internal static bool TryParseOverwrite(ref object obj, Type objectType, object jsonObject, ICustomActivator activator) =>
            TryParseOverwrite(ref obj, objectType, jsonObject, activator, false);

        private static (bool isSuccess, object result) Parse(object obj, Type objectType, object jsonObject, ICustomActivator activator, bool isAppend)
        {
            if (typeof(IList).IsAssignableFrom(objectType))
            {
                if (TryGetJsonObjectAsList(jsonObject, out List<object> jsonObjectAsList))
                {
                    if (objectType.IsArray)
                    {
                        Type elementType = objectType.GetElementType();

                        Debug.Assert(elementType is not null);

                        if (obj is not Array array || array.Length != jsonObjectAsList.Count)
                        {
                            obj = Array.CreateInstance(elementType, jsonObjectAsList.Count);
                        }

                        return (TryParseAsArray((IList)obj, elementType, jsonObjectAsList, activator), obj);
                    }
                    else
                    {
                        if (obj is null)
                        {
                            if (jsonObject is Dictionary<string, object> jsonObjectAsDictionary)
                            {
                                obj = activator.CreateInstance(objectType, jsonObjectAsDictionary);
                            }
                            else
                            {
                                obj = activator.CreateInstance(objectType);
                            }
                        }

                        return
                            (obj is IList objectAsList &&
                             TryGetListElementType(objectAsList, objectType, out Type elementType) &&
                             TryParseAsList(objectAsList, elementType, jsonObjectAsList, activator, isAppend),
                                obj);
                    }
                }
            }
            else if (jsonObject is Dictionary<string, object> jsonObjectAsDictionary)
            {
                if (obj is null)
                {
                    Type nullableType = Nullable.GetUnderlyingType(objectType);

                    if (nullableType is not null)
                    {
                        obj = activator.CreateInstance(nullableType, jsonObjectAsDictionary);

                        objectType = nullableType;
                    }
                    else
                    {
                        obj = activator.CreateInstance(objectType, jsonObjectAsDictionary);
                    }
                }

                return
                    typeof(IDictionary).IsAssignableFrom(objectType)
                        ? (TryParseAsDictionary(obj as IDictionary, objectType, jsonObjectAsDictionary, activator, isAppend), obj)
                        : (TryParseAsCustomType(obj, objectType, jsonObjectAsDictionary, activator), obj);
            }
            else if (TryConvertValue(objectType, jsonObject, out object value, activator))
            {
                return (true, value);
            }

            return (false, obj);
        }

        private static bool TryParseAsCustomType(object obj, Type objectType, Dictionary<string, object> jsonObjectAsDictionary, ICustomActivator activator)
        {
            FieldInfo[] fieldInfos = objectType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            foreach (FieldInfo fieldInfo in fieldInfos)
            {
                if (!jsonObjectAsDictionary.TryGetValue(fieldInfo.Name, out object jsonObject))
                {
                    Log.Info($"{nameof(TryParseAsCustomType)} : <{objectType}.{fieldInfo.Name}> is not included in current json.");

                    continue;
                }

                if (TryParse(out object fieldObject, fieldInfo.FieldType, jsonObject, activator))
                {
                    fieldInfo.SetValue(obj, fieldObject);
                }
            }

            return true;
        }

        private static bool TryParseAsDictionary(IDictionary objectAsDictionary, Type objectType, Dictionary<string, object> jsonObjectAsDictionary, ICustomActivator activator,
            bool isAppend)
        {
            if (!isAppend)
            {
                objectAsDictionary.Clear();
            }

            Type[] types = objectType.GetGenericArguments();

            Type keyType = types[0];
            Type valueType = types[1];

            foreach (KeyValuePair<string, object> pair in jsonObjectAsDictionary)
            {
                if (TryGetKey(keyType, pair.Key, out object key, activator) &&
                    TryParse(out object elementInstance, valueType, pair.Value, activator))
                {
                    objectAsDictionary.Add(key, elementInstance);
                }
            }

            return true;

            static bool TryGetKey(Type keyType, string jsonKey, out object key, ICustomActivator activator)
            {
                if (keyType.IsEnum)
                {
                    key = Enum.Parse(keyType, jsonKey, false);

                    return true;
                }

                return TryConvertValue(keyType, jsonKey, out key, activator);
            }
        }

        private static bool TryParseAsList(IList objectAsList, Type elementType, List<object> jsonObjectAsList, ICustomActivator activator, bool isAppend)
        {
            if (!isAppend)
            {
                objectAsList.Clear();
            }

            foreach (object jsonObjectElement in jsonObjectAsList)
            {
                if (TryParse(out object elementInstance, elementType, jsonObjectElement, activator))
                {
                    objectAsList.Add(elementInstance);
                }
            }

            return true;
        }

        private static bool TryParseAsArray(IList objectAsArray, Type elementType, List<object> jsonObjectAsList, ICustomActivator activator)
        {
            for (int i = 0, count = jsonObjectAsList.Count; i < count; ++i)
            {
                objectAsArray[i] = TryParse(out object elementInstance, elementType, jsonObjectAsList[i], activator) ? elementInstance : null;
            }

            return true;
        }

        private static bool TryGetListElementType(IList list, Type listType, out Type elementType)
        {
            if (list is not null)
            {
                foreach (PropertyInfo propertyInfo in listType.GetProperties())
                {
                    if (propertyInfo.Name != "Item")
                    {
                        continue;
                    }

                    ParameterInfo[] parameters = propertyInfo.GetIndexParameters();

                    if (parameters.Length != 1)
                    {
                        continue;
                    }

                    if (parameters[0].ParameterType == typeof(int))
                    {
                        elementType = propertyInfo.PropertyType;

                        return true;
                    }
                }
            }

            elementType = null;

            Log.Error($"{nameof(TryGetListElementType)} : can not found list element type.");

            return false;
        }

        private static bool TryGetJsonObjectAsList(object jsonObject, out List<object> list)
        {
            switch (jsonObject)
            {
                case List<object> jsonObjectAsList:
                    list = jsonObjectAsList;
                    return true;

                case Dictionary<string, object> jsonObjectAsDictionary:
                    list =
                        jsonObjectAsDictionary
#if USING_ZLINQ
                        .AsValueEnumerable()
#endif
                        .Select(pair => pair.Value)
                        .ToList();
                    return true;
            }

            list = null;

            Log.Error($"{nameof(TryGetJsonObjectAsList)} : jsonObject is not list type.");

            return false;
        }

        private static bool TryConvertValue(Type objectType, object jsonObject, out object result, ICustomActivator activator)
        {
            Type nullableType = Nullable.GetUnderlyingType(objectType);
            if (nullableType is not null)
            {
                if (jsonObject is null)
                {
                    result = null;

                    return true;
                }

                return TryParse(out result, nullableType, jsonObject, activator);
            }

            bool isSuccess = true;

            try
            {
                if (objectType == typeof(object))
                {
                    result = jsonObject;
                }
                else if (objectType == typeof(bool))
                {
                    result = Convert.ToBoolean(jsonObject);
                }
                else if (objectType == typeof(int))
                {
                    result = Convert.ToInt32(jsonObject);
                }
                else if (objectType == typeof(uint))
                {
                    result = Convert.ToUInt32(jsonObject);
                }
                else if (objectType == typeof(float))
                {
                    result = Convert.ToSingle(jsonObject);
                }
                else if (objectType == typeof(long))
                {
                    result = Convert.ToInt64(jsonObject);
                }
                else if (objectType == typeof(ulong))
                {
                    result = Convert.ToUInt64(jsonObject);
                }
                else if (objectType == typeof(double))
                {
                    result = Convert.ToDouble(jsonObject);
                }
                else if (objectType == typeof(string))
                {
                    result = Convert.ToString(jsonObject);
                }
                else if (objectType == typeof(sbyte))
                {
                    result = Convert.ToSByte(jsonObject);
                }
                else if (objectType == typeof(byte))
                {
                    result = Convert.ToByte(jsonObject);
                }
                else if (objectType == typeof(short))
                {
                    result = Convert.ToInt16(jsonObject);
                }
                else if (objectType == typeof(ushort))
                {
                    result = Convert.ToUInt16(jsonObject);
                }
                else if (objectType == typeof(decimal))
                {
                    result = Convert.ToDecimal(jsonObject);
                }
                else
                {
                    result = null;

                    isSuccess = false;

                    Log.Error($"{nameof(TryConvertValue)} : type is not primitive or string.");
                }
            }
            catch (Exception e)
            {
                result = null;

                isSuccess = false;

                Log.Error($"{nameof(TryConvertValue)} : {e}.");
            }

            return isSuccess;
        }
    }
}