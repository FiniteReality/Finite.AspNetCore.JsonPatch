using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Finite.AspNetCore.JsonPatch
{
    /// <summary>
    /// Defines a JSON converter which can be used to serialize instances of
    /// <see cref="JsonPointer"/>.
    /// </summary>
    public class JsonPointerConverter : JsonConverter<JsonPointer>
    {
        /// <inheritdoc/>
        public override JsonPointer? Read(ref Utf8JsonReader reader,
            Type typeToConvert, JsonSerializerOptions options)
        {
            var raw = reader.GetString();

            if (raw == null)
                throw new JsonException(
                    $"Expected {nameof(JsonTokenType.String)}");

            if (!JsonPointer.TryParse(raw, out var value))
                throw new JsonException("Failed to parse JSON Pointer");

            return value;
        }

        /// <inheritdoc/>
        public override void Write(Utf8JsonWriter writer, JsonPointer value,
            JsonSerializerOptions options)
            => writer.WriteStringValue(value.Value);
    }
}
