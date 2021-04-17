using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

// TODO: This should be replaced with the built-in mutable JSON DOM when it's
// available.

namespace Finite.AspNetCore.JsonPatch.Internal
{
    internal abstract class MutableJsonValue
    {
        public abstract void WriteTo(Utf8JsonWriter writer);

        public static MutableJsonValue Build(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                {
                    var result = new MutableJsonObject();
                    foreach (var property in element.EnumerateObject())
                    {
                        result[property.Name] = Build(property.Value);
                    }
                    return result;
                }
                case JsonValueKind.Array:
                {
                    var result = new MutableJsonArray();
                    foreach (var child in element.EnumerateArray())
                    {
                        result.Add(Build(child));
                    }
                    return result;
                }
                default:
                {
                    return new MutableJsonElement(element);
                }
            }
        }
    }

    internal sealed class MutableJsonObject
        : MutableJsonValue, IDictionary<string, MutableJsonValue>
    {
        private readonly IDictionary<string, MutableJsonValue> _values;

        public MutableJsonObject()
        {
            _values = new Dictionary<string, MutableJsonValue>();
        }

        public override void WriteTo(Utf8JsonWriter writer)
        {
            writer.WriteStartObject();
            foreach (var pair in _values)
            {
                writer.WritePropertyName(pair.Key);
                pair.Value.WriteTo(writer);
            }
            writer.WriteEndObject();
        }

        public MutableJsonValue this[string key]
        {
            get => _values[key];
            set => _values[key] = value;
        }

        public ICollection<string> Keys => _values.Keys;

        public ICollection<MutableJsonValue> Values => _values.Values;

        public int Count => _values.Count;

        public bool IsReadOnly => _values.IsReadOnly;

        public void Add(string key, MutableJsonValue value)
            => _values.Add(key, value);

        public void Add(KeyValuePair<string, MutableJsonValue> item)
            => _values.Add(item);

        public void Clear()
            => _values.Clear();

        public bool Contains(KeyValuePair<string, MutableJsonValue> item)
            => _values.Contains(item);

        public bool ContainsKey(string key)
            => _values.ContainsKey(key);

        public void CopyTo(KeyValuePair<string, MutableJsonValue>[] array,
            int arrayIndex)
            => _values.CopyTo(array, arrayIndex);

        public IEnumerator<KeyValuePair<string, MutableJsonValue>> GetEnumerator()
            => _values.GetEnumerator();

        public bool Remove(string key)
            => _values.Remove(key);

        public bool Remove(KeyValuePair<string, MutableJsonValue> item)
            => _values.Remove(item);

        public bool TryGetValue(string key,
            [MaybeNullWhen(false)] out MutableJsonValue value)
            => _values.TryGetValue(key, out value);

        IEnumerator IEnumerable.GetEnumerator()
            => ((IEnumerable)_values).GetEnumerator();
    }

    internal sealed class MutableJsonArray
        : MutableJsonValue, IList<MutableJsonValue>
    {
        private readonly IList<MutableJsonValue> _values;

        public MutableJsonArray()
        {
            _values = new List<MutableJsonValue>();
        }

        public override void WriteTo(Utf8JsonWriter writer)
        {
            writer.WriteStartArray();
            foreach (var element in _values)
                element.WriteTo(writer);
            writer.WriteEndArray();
        }

        public MutableJsonValue this[int index]
        {
            get => _values[index];
            set => _values[index] = value;
        }

        public int Count => _values.Count;

        public bool IsReadOnly => _values.IsReadOnly;

        public void Add(MutableJsonValue item)
            => _values.Add(item);

        public void Clear()
            => _values.Clear();

        public bool Contains(MutableJsonValue item)
            => _values.Contains(item);

        public void CopyTo(MutableJsonValue[] array, int arrayIndex)
            => _values.CopyTo(array, arrayIndex);

        public IEnumerator<MutableJsonValue> GetEnumerator()
            => _values.GetEnumerator();

        public int IndexOf(MutableJsonValue item)
            => _values.IndexOf(item);

        public void Insert(int index, MutableJsonValue item)
            => _values.Insert(index, item);

        public bool Remove(MutableJsonValue item)
            => _values.Remove(item);

        public void RemoveAt(int index)
            => _values.RemoveAt(index);

        IEnumerator IEnumerable.GetEnumerator()
            => ((IEnumerable)_values).GetEnumerator();
    }

    internal sealed class MutableJsonElement : MutableJsonValue
    {
        public JsonElement Element { get; set; }

        public MutableJsonElement(JsonElement element)
        {
            Element = element;
        }

        public override void WriteTo(Utf8JsonWriter writer)
            => Element.WriteTo(writer);
    }
}
