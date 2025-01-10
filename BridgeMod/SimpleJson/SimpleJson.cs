using System;
using System.Collections;
using System.Collections.Generic;

namespace SimpleJson
{
    public static class JsonConvert
    {

        public static T DeserializeObject<T>(string json)
        {
            return (T)DeserializeObject(json, typeof(T));
        }

        public static object DeserializeObject(string json, Type type)
        {
            return FromJson(JsonToken.Parse(ref json), type);
        }

        public static string SerializeObject<T>(T obj)
        {
            return SerializeObject((object)obj);
        }

        public static string SerializeObject(object obj)
        {
            return ToJson(obj).ToString();
        }

        public static object FromJson(JsonToken json, Type type)
        {
            if (json == null) return null;
            if (json is JsonNull) return null;
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                if (json is JsonNull) return null;
                var valueType = type.GetGenericArguments()[0];
                return FromJson(json, valueType);
            }
            if (json is JsonNumber)
            {
                var num = (JsonNumber)json;
                if (type == typeof(byte)) return (byte)num.value;
                if (type == typeof(sbyte)) return (sbyte)num.value;
                if (type == typeof(ushort)) return (ushort)num.value;
                if (type == typeof(short)) return (short)num.value;
                if (type == typeof(uint)) return (uint)num.value;
                if (type == typeof(int)) return (int)num.value;
                if (type == typeof(ulong)) return (ulong)num.value;
                if (type == typeof(long)) return (long)num.value;
                if (type == typeof(float)) return (float)num.value;
                if (type == typeof(double)) return (double)num.value;
            }
            if (json is JsonString)
            {
                var str = (JsonString)json;
                if (type == typeof(string)) return str.value;
                if (type == typeof(DateTime)) return DateTime.Parse(str.value);
                if (type.IsEnum) return Enum.Parse(type, str.value, true);
            }
            if (json is JsonBoolean)
            {
                var b = (JsonBoolean)json;
                if (type == typeof(bool)) return b.value;
            }
            if (json is JsonArray)
            {
                var arr = (JsonArray)json;
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
                {
                    var itemType = type.GetGenericArguments()[0];
                    var list = Activator.CreateInstance(type) as IList;
                    foreach (var item in arr.items) list.Add(FromJson(item, itemType));
                    return list;
                }
            }
            if (json is JsonObject)
            {
                var obj = (JsonObject)json;
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                {
                    var keyType = type.GetGenericArguments()[0];
                    var valueType = type.GetGenericArguments()[1];
                    var dict = Activator.CreateInstance(type) as IDictionary;
                    foreach (var pair in obj.items) dict.Add(Convert.ChangeType(pair.Key, keyType), FromJson(pair.Value, valueType));
                    return dict;
                }
                if (!type.IsValueType)
                {
                    var instance = Activator.CreateInstance(type);
                    var fields = type.GetFields();
                    foreach (var field in fields)
                    {
                        JsonToken item;
                        if (obj.items.TryGetValue(field.Name, out item))
                        {
                            var value = FromJson(item, field.FieldType);
                            field.SetValue(instance, value);
                        }
                    }
                    return instance;
                }
            }
            throw new ArgumentOutOfRangeException("type", type, $"Unsupported object type {type}");
        }

        public static JsonToken ToJson(object obj)
        {
            if (obj == null) return new JsonNull();
            if (obj is string) return new JsonString((string)obj);
            if (obj is bool) return new JsonBoolean((bool)obj);
            if (obj is byte) return new JsonNumber((byte)obj);
            if (obj is sbyte) return new JsonNumber((sbyte)obj);
            if (obj is ushort) return new JsonNumber((ushort)obj);
            if (obj is short) return new JsonNumber((short)obj);
            if (obj is uint) return new JsonNumber((uint)obj);
            if (obj is int) return new JsonNumber((int)obj);
            if (obj is ulong) return new JsonNumber((ulong)obj);
            if (obj is long) return new JsonNumber((long)obj);
            if (obj is float) return new JsonNumber((float)obj);
            if (obj is double) return new JsonNumber((double)obj);
            if (obj is DateTime) return new JsonString(((DateTime)obj).ToString("o"));
            var type = obj.GetType();
            if (type.IsEnum) return new JsonString(Enum.GetName(type, obj));
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                var actualValue = type.GetProperty("Value").GetValue(obj, null);
                return ToJson(actualValue);
            }
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                var list = obj as IList;
                var values = new List<JsonToken>();
                foreach (var value in list)
                {
                    values.Add(ToJson(value));
                }
                return new JsonArray(values);
            }
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                var dict = obj as IDictionary;
                var values = new Dictionary<string, JsonToken>();
                foreach (var key in dict.Keys)
                {
                    values.Add(key.ToString(), ToJson(dict[key]));
                }
                return new JsonObject(values);
            }
            if (!type.IsValueType) {
                var values = new Dictionary<string, JsonToken>();
                var fields = type.GetFields();
                foreach (var field in fields)
                {
                    var fieldValue = field.GetValue(obj);
                    if (fieldValue == null && field.GetCustomAttributes(typeof(JsonOptionalAttribute), true).Length > 0)
                    {
                        continue;
                    }
                    values.Add(field.Name, ToJson(fieldValue));
                }
                return new JsonObject(values);
            }
            throw new ArgumentOutOfRangeException("obj", obj, $"Unsupported object type {type}");
        }
    }

    public abstract class JsonToken
    {
        public static JsonToken Parse(ref string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            json = json.TrimStart();
            if (json[0] == '{')
            {
                json = json.Substring(1).TrimStart();
                if (json[0] == '}')
                {
                    json = json.Substring(1).TrimStart();
                    return new JsonObject(null);
                }
                else
                {
                    var values = new Dictionary<string, JsonToken>();
                    do
                    {
                        var key = Parse(ref json) as JsonString;
                        if (key == null) throw new ArgumentOutOfRangeException("json", json, "Invalid JSON");
                        json = json.TrimStart();
                        if (json[0] != ':') throw new ArgumentOutOfRangeException("json", json, "Invalid JSON");
                        json = json.Substring(1).TrimStart();
                        var value = Parse(ref json);
                        json = json.TrimStart();
                        values[key.value] = value;
                        if (json[0] != ',') break;
                        json = json.Substring(1).TrimStart();
                    }
                    while (json[0] != '}');
                    if (json[0] != '}') throw new ArgumentOutOfRangeException("json", json, "Invalid JSON");
                    json = json.Substring(1).TrimStart();
                    return new JsonObject(values);
                }
            }
            else if (json[0] == '[')
            {
                json = json.Substring(1).TrimStart();
                if (json[0] == ']')
                {
                    json = json.Substring(1).TrimStart();
                    return new JsonArray(null);
                }
                else
                {
                    var values = new List<JsonToken>();
                    do
                    {
                        var value = Parse(ref json);
                        json = json.TrimStart();
                        values.Add(value);
                        if (json[0] != ',') break;
                        json = json.Substring(1).TrimStart();
                    }
                    while (json[0] != ']');
                    if (json[0] != ']') throw new ArgumentOutOfRangeException("json", json, "Invalid JSON");
                    json = json.Substring(1).TrimStart();
                    return new JsonArray(values);
                }

            }
            else if (json[0] == '"')
            {
                var o = "";
                var i = 1;
                while (json[i] != '"')
                {
                    if (json[i] == '\\')
                    {
                        i++;
                        if (json[i] == 'u')
                        {
                            i++;
                            var codepoint = Convert.ToUInt16(json.Substring(i, 4), 16);
                            o += char.ConvertFromUtf32(codepoint);
                            i += 4;
                        }
                        else
                        {
                            if (json[i] == '"') o += "\"";
                            else if (json[i] == '\\') o += "\\";
                            else if (json[i] == '/') o += "/";
                            else if (json[i] == 'b') o += "\b";
                            else if (json[i] == 'f') o += "\f";
                            else if (json[i] == 'n') o += "\n";
                            else if (json[i] == 'r') o += "\r";
                            else if (json[i] == 't') o += "\t";
                            else o += json[i];
                            i++;
                        }
                    }
                    else
                    {
                        o += json[i++];
                    }
                }
                json = json.Substring(i + 1);
                return new JsonString(o);
            }
            else if (json.StartsWith("true"))
            {
                json = json.Substring("true".Length);
                return new JsonBoolean(true);
            }
            else if (json.StartsWith("false"))
            {
                json = json.Substring("false".Length);
                return new JsonBoolean(false);
            }
            else if (json.StartsWith("null"))
            {
                json = json.Substring("null".Length);
                return new JsonNull();
            }
            else if (char.IsDigit(json[0]) || json[0] == '-')
            {
                var i = 1;
                while (char.IsDigit(json[i])) i++;
                if (json[i] == '.')
                {
                    i++;
                    while (char.IsDigit(json[i])) i++;
                }
                if (json[i] == 'e' || json[i] == 'E')
                {
                    i++;
                    if (json[i] == '+' || json[i] == '-') i++;
                    while (char.IsDigit(json[i])) i++;
                }
                var str = json.Substring(0, i);
                json = json.Substring(i);
                return new JsonNumber(double.Parse(str));
            }
            else
            {
                throw new ArgumentOutOfRangeException("json", json, "Invalid JSON");
            }
        }
    }

    public class JsonObject : JsonToken
    {
        public Dictionary<string, JsonToken> items;

        public JsonObject(IDictionary<string, JsonToken> items)
        {
            this.items = items != null ? new Dictionary<string, JsonToken>(items) : null;
        }

        public override string ToString()
        {
            var o = "{";
            if (items != null)
            {
                var first = true;
                foreach (var pair in items)
                {
                    if (first)
                    {
                        first = false;
                    }
                    else
                    {
                        o += ",";
                    }
                    o += new JsonString(pair.Key).ToString();
                    o += ":";
                    o += pair.Value.ToString();
                }
            }
            o += "}";
            return o;
        }
    }

    public class JsonArray : JsonToken
    {
        public List<JsonToken> items;

        public JsonArray(IEnumerable<JsonToken> items)
        {
            this.items = items != null ? new List<JsonToken>(items) : null;
        }

        public override string ToString()
        {
            var o = "[";
            var first = true;
            foreach (var item in items)
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    o += ",";
                }
                o += item.ToString();
            }
            o += "]";
            return o;
        }
    }

    public class JsonString : JsonToken
    {
        public string value;

        public JsonString(string value)
        {
            this.value = value;
        }

        public override string ToString()
        {
            var o = "\"";
            if (value != null)
            {
                foreach (var c in value)
                {
                    if (c == '"') o += "\\\"";
                    else if (c == '\\') o += "\\\\";
                    else o += c;
                }
            }
            o += "\"";
            return o;
        }
    }

    public class JsonNumber : JsonToken
    {
        public double value;

        public JsonNumber(double value)
        {
            this.value = value;
        }

        public override string ToString()
        {
            return value.ToString();
        }
    }

    public class JsonBoolean : JsonToken
    {
        public bool value;

        public JsonBoolean(bool value)
        {
            this.value = value;
        }

        public override string ToString()
        {
            return value ? "true" : "false";
        }
    }

    public class JsonNull : JsonToken
    {
        public override string ToString()
        {
            return "null";
        }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class JsonOptionalAttribute : Attribute
    {

    }
}
