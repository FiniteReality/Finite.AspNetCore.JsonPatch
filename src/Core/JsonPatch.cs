using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Finite.AspNetCore.JsonPatch.Internal;

namespace Finite.AspNetCore.JsonPatch
{
    /// <summary>
    /// Defines a class which represents a JSON Patch document.
    /// </summary>
    public sealed partial class JsonPatch : IDisposable
    {
        internal List<PatchElement> PatchElements { get; }

        internal JsonPatch(List<PatchElement> elements)
        {
            PatchElements = elements;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            foreach (var elem in PatchElements)
                if (elem is IDisposable disposable)
                    disposable.Dispose();
        }

        // TODO: consider replacing this with something more appropriate
        /// <summary>
        /// Attempts to apply the patches specified to the given root element.
        /// </summary>
        /// <param name="writer">
        /// The writer to write the resultant document to.
        /// </param>
        /// <param name="rootElement">
        /// The root element to apply the JSON Patch to.
        /// </param>
        /// <returns>
        /// <c>true</c> if the patch applied, or <c>false</c> otherwise.
        /// </returns>
        public bool TryApply(Utf8JsonWriter writer,
            JsonElement rootElement)
        {
            var tree = MutableJsonValue.Build(rootElement);

            if (!ApplyInternal(PatchElements, tree))
                return false;

            tree.WriteTo(writer);
            return true;
        }

        private static bool ApplyInternal(IEnumerable<PatchElement> elements,
            MutableJsonValue tree)
        {
            foreach (var patchElement in elements)
            {
                switch (patchElement)
                {
                    case PatchAddElement add:
                    {
                        var state = (tree, add.Path.Depth,
                            MutableJsonValue.Build(add.Value.RootElement));
                        if (!add.Path.Evaluate(AddElement, ref state))
                            return false;

                        break;
                    }
                    case PatchRemoveElement remove:
                    {
                        var state = (tree, remove.Path.Depth);
                        if (!remove.Path.Evaluate(RemoveElement, ref state))
                            return false;

                        break;
                    }
                    case PatchReplaceElement replace:
                    {
                        var removeState = (tree, replace.Path.Depth);
                        if (!replace.Path.Evaluate(RemoveElement,
                            ref removeState))
                            return false;

                        var addState = (tree, replace.Path.Depth,
                            MutableJsonValue.Build(replace.Value.RootElement));
                        if (!replace.Path.Evaluate(AddElement, ref addState))
                            return false;

                        break;
                    }
                    case PatchMoveElement move:
                    {
                        var removeState = (tree, move.From.Depth);
                        if (!move.From.Evaluate(RemoveElement,
                            ref removeState))
                            return false;

                        var addState = (tree, move.Path.Depth, removeState.tree);
                        if (!move.Path.Evaluate(AddElement, ref addState))
                            return false;
                        break;
                    }
                    case PatchCopyElement copy:
                    {
                        var findState = tree;
                        if (!copy.From.Evaluate(FindElement, ref findState))
                            return false;

                        var addState = (tree, copy.Path.Depth, findState);
                        if (!copy.Path.Evaluate(AddElement, ref addState))
                            return false;

                        break;
                    }
                    case PatchTestElement test:
                    {
                        var findState = tree;
                        if (!test.Path.Evaluate(FindElement, ref findState))
                            return false;

                        if (!TestEqual(findState, test.Value.RootElement))
                            return false;

                        break;
                    }
                }
            }

            return true;


            static bool TestEqual(MutableJsonValue value, JsonElement expected)
            {
                switch (value)
                {
                    case MutableJsonObject @object:
                    {
                        if (expected.ValueKind != JsonValueKind.Object)
                            return false;

                        foreach (var pair in @object)
                        {
                            if (!expected.TryGetProperty(pair.Key,
                                out var expectedValue))
                                return false;
                            if (!TestEqual(pair.Value, expectedValue))
                                return false;
                        }

                        return true;
                    }
                    case MutableJsonArray array:
                    {
                        if (expected.ValueKind != JsonValueKind.Array)
                            return false;

                        if (array.Count != expected.GetArrayLength())
                            return false;

                        for (int x = 0; x < array.Count; x++)
                        {
                            if (!TestEqual(array[x], expected[x]))
                                return false;
                        }

                        return true;
                    }
                    case MutableJsonElement element:
                    {
                        // TODO: make this more efficient
                        return element.Element.GetRawText() == expected.GetRawText();
                    }
                    default:
                        return false;
                }
            }
        }

        private static bool AddElement(ReadOnlySpan<char> path, int depth,
            ref (MutableJsonValue, int, MutableJsonValue) state)
        {
            ref var value = ref state.Item1;
            var maxDepth = state.Item2;

            switch (value)
            {
                case MutableJsonObject @object:
                {
                    if (depth == maxDepth - 1)
                    {
                        @object[path.ToString()] = state.Item3;

                        return true;
                    }

                    foreach (var kvp in @object)
                    {
                        if (path.SequenceEqual(kvp.Key))
                        {
                            value = kvp.Value;
                            return true;
                        }
                    }

                    return false;
                }
                case MutableJsonArray array:
                {
                    if ((depth == maxDepth - 1) && path.SequenceEqual("-"))
                    {
                        array.Add(state.Item3);
                        return true;
                    }
                    else
                    {
                        if (!int.TryParse(path, NumberStyles.None,
                            CultureInfo.InvariantCulture, out int index))
                            return false;

                        if (index < 0 || index >= array.Count)
                            return false;

                        if (depth == maxDepth - 1)
                        {
                            array.Insert(index, state.Item3);
                        }
                        else
                        {
                            value = array[index];
                        }
                        return true;
                    }
                }
                default:
                    return false;
            }
        }

        private static bool RemoveElement(ReadOnlySpan<char> path, int depth,
            ref (MutableJsonValue, int) state)
        {
            ref var value = ref state.Item1;
            var maxDepth = state.Item2;

            switch (value)
            {
                case MutableJsonObject @object:
                {
                    if (depth == maxDepth - 1)
                    {
                        return @object.Remove(path.ToString(), out value);
                    }

                    foreach (var kvp in @object)
                    {
                        if (path.SequenceEqual(kvp.Key))
                        {
                            value = kvp.Value;
                            return true;
                        }
                    }

                    return false;
                }
                case MutableJsonArray array:
                {
                    if (!int.TryParse(path, NumberStyles.None,
                        CultureInfo.InvariantCulture, out int index))
                        return false;

                    if (index < 0 || index >= array.Count)
                        return false;

                    value = array[index];

                    return depth != maxDepth - 1 || array.Remove(value);
                }
                default:
                    return false;
            }
        }

        private static bool FindElement(ReadOnlySpan<char> path, int depth,
            ref MutableJsonValue value)
        {
            switch (value)
            {
                case MutableJsonObject @object:
                {
                    foreach (var kvp in @object)
                    {
                        if (path.SequenceEqual(kvp.Key))
                        {
                            value = kvp.Value;
                            return true;
                        }
                    }

                    return false;
                }
                case MutableJsonArray array:
                {
                    if (!int.TryParse(path, NumberStyles.None,
                        CultureInfo.InvariantCulture, out int index))
                        return false;

                    if (index < 0 || index >= array.Count)
                        return false;

                    value = array[index];
                    return true;
                }
                default:
                    return false;
            }
        }
    }
}
