using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Finite.AspNetCore.JsonPatch.Internal;

namespace Finite.AspNetCore.JsonPatch
{
    /// <summary>
    /// Defines a JSON converter which can be used to serialize instances of
    /// <see cref="JsonPatch"/>.
    /// </summary>
    public class JsonPatchConverter : JsonConverter<JsonPatch>
    {
        /// <inheritdoc/>
        public override JsonPatch? Read(ref Utf8JsonReader reader,
            Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartArray)
                throw new JsonException(
                    $"Expected {nameof(JsonTokenType.StartArray)}");

            var elements = new List<PatchElement>();

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                    break;

                elements.Add(ReadElement(ref reader));
            }

            return new JsonPatch(elements);

            static PatchElement ReadElement(ref Utf8JsonReader reader)
            {
                if (reader.TokenType != JsonTokenType.StartObject)
                    throw new JsonException(
                        $"Expected {nameof(JsonTokenType.StartObject)}");

                // add = 0, remove = 1, replace = 2, copy = 3, move = 4,
                // test = 5
                int? operation = null;
                JsonPointer? path = null;
                JsonDocument? value = null;
                JsonPointer? from = null;

                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndObject)
                        break;

                    if (reader.TokenType != JsonTokenType.PropertyName)
                        throw new JsonException(
                            $"Expected {nameof(JsonTokenType.PropertyName)}");

                    if (reader.ValueTextEquals("op"))
                    {
                        if (!reader.Read())
                            throw new JsonException();

                        if (reader.TokenType != JsonTokenType.String)
                            throw new JsonException(
                                $"Expected {nameof(JsonTokenType.String)}");

                        if (reader.ValueTextEquals("add"))
                            operation = 0;
                        else if (reader.ValueTextEquals("remove"))
                            operation = 1;
                        else if (reader.ValueTextEquals("replace"))
                            operation = 2;
                        else if (reader.ValueTextEquals("copy"))
                            operation = 3;
                        else if (reader.ValueTextEquals("move"))
                            operation = 4;
                        else if (reader.ValueTextEquals("test"))
                            operation = 5;
                        else
                            throw new JsonException(
                                "The 'op' field must be one of 'add', " +
                                "'remove', 'replace', 'copy', 'move' or " +
                                "'test'.");
                    }
                    else if (reader.ValueTextEquals("path"))
                    {
                        if (!reader.Read())
                            throw new JsonException();

                        if (reader.TokenType != JsonTokenType.String)
                            throw new JsonException(
                                $"Expected {nameof(JsonTokenType.String)}");

                        if (!JsonPointer.TryParse(reader.GetString()!,
                            out path))
                            throw new JsonException(
                                "Failed to parse JSON Pointer");
                    }
                    else if (reader.ValueTextEquals("value"))
                    {
                        if (!JsonDocument.TryParseValue(ref reader, out value))
                            throw new JsonException("Failed to parse value");
                    }
                    else if (reader.ValueTextEquals("from"))
                    {
                        if (!reader.Read())
                            throw new JsonException();

                        if (reader.TokenType != JsonTokenType.String)
                            throw new JsonException(
                                $"Expected {nameof(JsonTokenType.String)}");

                        if (!JsonPointer.TryParse(reader.GetString()!,
                            out from))
                            throw new JsonException(
                                "Failed to parse JSON Pointer");
                    }
                }

                return operation switch
                {
                    // add
                    0 => new PatchAddElement(path!, value!),
                    // remove
                    1 => new PatchRemoveElement(path!),
                    // replace
                    2 => new PatchReplaceElement(path!, value!),
                    // copy
                    3 => new PatchCopyElement(path!, from!),
                    // move
                    4 => new PatchMoveElement(path!, from!),
                    // test
                    5 => new PatchTestElement(path!, value!),
                    // should never happen since we handle this above
                    _ => null!,
                };
            }
        }

        // TODO: do we really need this, if the JSON Patch can only be
        // constructed internally?
        /// <inheritdoc/>
        public override void Write(Utf8JsonWriter writer, JsonPatch value,
            JsonSerializerOptions options)
        {
            writer.WriteStartArray();

            foreach (var element in value.PatchElements)
            {
                writer.WriteStartObject();

                writer.WriteString("op", element switch
                {
                    PatchAddElement => "add",
                    PatchRemoveElement => "remove",
                    PatchReplaceElement => "replace",
                    PatchCopyElement => "copy",
                    PatchMoveElement => "move",
                    PatchTestElement => "test",
                    _ => null // unreachable
                });

                writer.WritePropertyName("path");
                JsonSerializer.Serialize(writer, element.Path, options);

                switch (element)
                {
                    case PatchAddElement add:
                        writer.WritePropertyName("value");
                        add.Value.WriteTo(writer);
                        break;
                    case PatchReplaceElement replace:
                        writer.WritePropertyName("value");
                        replace.Value.WriteTo(writer);
                        break;
                    case PatchCopyElement copy:
                        writer.WritePropertyName("from");
                        JsonSerializer.Serialize(writer, copy.From, options);
                        break;
                    case PatchMoveElement move:
                        writer.WritePropertyName("from");
                        JsonSerializer.Serialize(writer, move.From, options);
                        break;
                    case PatchTestElement test:
                        writer.WritePropertyName("value");
                        test.Value.WriteTo(writer);
                        break;
                }

                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }
    }
}
