using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;

namespace Finite.AspNetCore.JsonPatch
{
    /// <summary>
    /// Defines extension methods for <see cref="JsonPointer"/>.
    /// </summary>
    public static class JsonPointerExtensions
    {
        /// <summary>
        /// Attempts to find the element pointed to in the given JSON tree.
        /// </summary>
        /// <param name="pointer">
        /// The pointer representing the element to find.
        /// </param>
        /// <param name="root">
        /// The root of the document to find <paramref name="pointer"/> in.
        /// </param>
        /// <param name="value">
        /// The found <see cref="JsonElement"/>, or <c>null</c> otherwise.
        /// </param>
        /// <returns>
        /// <c>true</c> if <paramref name="root"/> contains an element with the
        /// path <paramref name="pointer"/>, or <c>false</c> otherwise.
        /// </returns>
        public static bool TryGetElement(this JsonPointer pointer,
            JsonElement root, [NotNullWhen(true)]out JsonElement? value)
        {
            var state = root;
            if (pointer.Evaluate(FindElement, ref state))
            {
                value = state;
                return true;
            }
            else
            {
                value = null;
                return false;
            }
        }

        private static bool FindElement(ReadOnlySpan<char> path, int depth,
            ref JsonElement value)
        {
            switch (value.ValueKind)
            {
                case JsonValueKind.Object:
                {
                    foreach (var kvp in value.EnumerateObject())
                    {
                        if (path.SequenceEqual(kvp.Name))
                        {
                            value = kvp.Value;
                            return true;
                        }
                    }

                    return false;
                }
                case JsonValueKind.Array:
                {
                    if (!int.TryParse(path, NumberStyles.None,
                        CultureInfo.InvariantCulture, out int index))
                        return false;

                    if (index < 0 || index >= value.GetArrayLength())
                        return false;

                    value = value[index];
                    return true;
                }
                default:
                    return false;
            }
        }
    }
}
