using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Web.Script.Serialization;

namespace DemonsSoulsPC.App.Services
{
    // JSON with .NET Framework's own JavaScriptSerializer (0.2.5). System.Text.Json and its 8 helper DLLs were embedded in
    // the exe and loaded from memory (Assembly.Load of a resource): 8 of 70 antivirus on VirusTotal flagged every build
    // from 0.2.2 on (BitDefender's generic "Gen:Variant", which RecompLauncher refuses); the same launcher without them:
    // 0/70. The model classes keep their [JsonPropertyName] names, so the files written by earlier launchers still load.
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class JsonPropertyNameAttribute : Attribute
    {
        public JsonPropertyNameAttribute(string name) => Name = name;
        public string Name { get; }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public sealed class JsonIgnoreAttribute : Attribute { }

    public static class Json
    {
        private static JavaScriptSerializer Serializer() => new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 64 };

        // Objects as Dictionary<string, object>, arrays as object[], numbers as int / long / decimal.
        public static object? Parse(string json) => Serializer().DeserializeObject(json);

        public static IDictionary<string, object>? Object(object? value) => value as IDictionary<string, object>;

        // A string member of an object, or "" when it is missing or not a string.
        public static string Text(IDictionary<string, object>? obj, string name) =>
            obj != null && obj.TryGetValue(name, out var value) && value is string text ? text : "";

        public static T? Deserialize<T>(string json) where T : class => (T?)Convert(Parse(json), typeof(T));

        public static string Serialize(object value) => Serializer().Serialize(Plain(value));

        private static string NameOf(PropertyInfo p) => p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? p.Name;

        private static IEnumerable<PropertyInfo> Members(Type type) =>
            type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0 && p.GetCustomAttribute<JsonIgnoreAttribute>() == null);

        // Our objects as dictionaries with their JSON names; dates as ISO 8601 text, as System.Text.Json wrote them.
        private static object? Plain(object? value)
        {
            switch (value)
            {
                case null: return null;
                case string _: return value;
                case DateTime date: return date.ToString("o", CultureInfo.InvariantCulture);
                case Enum e: return System.Convert.ToInt64(e, CultureInfo.InvariantCulture);
                case IDictionary dictionary:
                    var map = new Dictionary<string, object?>();
                    foreach (DictionaryEntry entry in dictionary) map[System.Convert.ToString(entry.Key, CultureInfo.InvariantCulture)] = Plain(entry.Value);
                    return map;
                case IEnumerable list: return list.Cast<object?>().Select(Plain).ToArray();
            }
            var type = value.GetType();
            if (type.IsPrimitive || value is decimal) return value;
            var result = new Dictionary<string, object?>();
            foreach (var p in Members(type)) result[NameOf(p)] = Plain(p.GetValue(value));
            return result;
        }

        // A parsed value as the given type; members missing from the JSON keep the class's own defaults.
        private static object? Convert(object? value, Type type)
        {
            var target = Nullable.GetUnderlyingType(type) ?? type;
            if (value == null) return type.IsValueType && Nullable.GetUnderlyingType(type) == null ? Activator.CreateInstance(type) : null;
            if (target == typeof(object)) return value;
            if (target == typeof(string)) return value as string ?? System.Convert.ToString(value, CultureInfo.InvariantCulture);
            if (target == typeof(DateTime))
                return value is string text ? DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind) : (DateTime)value;
            if (target.IsEnum) return Enum.ToObject(target, System.Convert.ToInt64(value, CultureInfo.InvariantCulture));
            if (target.IsPrimitive || target == typeof(decimal)) return System.Convert.ChangeType(value, target, CultureInfo.InvariantCulture);
            if (target.IsArray && value is IEnumerable items)
            {
                var element = target.GetElementType()!;
                var source = items.Cast<object?>().ToArray();
                var array = Array.CreateInstance(element, source.Length);
                for (int i = 0; i < source.Length; i++) array.SetValue(Convert(source[i], element), i);
                return array;
            }
            if (target.IsGenericType && target.GetGenericTypeDefinition() == typeof(List<>) && value is IEnumerable listItems)
            {
                var element = target.GetGenericArguments()[0];
                var list = (IList)Activator.CreateInstance(target);
                foreach (var item in listItems) list.Add(Convert(item, element));
                return list;
            }
            if (value is not IDictionary<string, object> obj) throw new FormatException($"JSON: {target.Name} attendu");
            var instance = Activator.CreateInstance(target);
            foreach (var p in Members(target))
                if (obj.TryGetValue(NameOf(p), out var member) || TryGetIgnoringCase(obj, NameOf(p), out member))
                    p.SetValue(instance, Convert(member, p.PropertyType));
            return instance;
        }

        // System.Text.Json read the settings with PropertyNameCaseInsensitive: kept.
        private static bool TryGetIgnoringCase(IDictionary<string, object> obj, string name, out object? value)
        {
            foreach (var pair in obj)
                if (string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase)) { value = pair.Value; return true; }
            value = null;
            return false;
        }
    }
}
