using System;
using System.Text.Json;

namespace Finite.AspNetCore.JsonPatch.Internal
{
    internal abstract record PatchElement(JsonPointer Path);

    internal sealed record PatchAddElement(JsonPointer Path,
        JsonDocument Value)
        : PatchElement(Path), IDisposable
    {
        public void Dispose() => Value.Dispose();
    }

    internal sealed record PatchRemoveElement(JsonPointer Path)
        : PatchElement(Path);

    internal sealed record PatchReplaceElement(JsonPointer Path,
        JsonDocument Value)
        : PatchElement(Path), IDisposable
    {
        public void Dispose() => Value.Dispose();
    }

    internal sealed record PatchCopyElement(JsonPointer Path, JsonPointer From)
        : PatchElement(Path);

    internal sealed record PatchMoveElement(JsonPointer Path, JsonPointer From)
        : PatchElement(Path);

    internal sealed record PatchTestElement(JsonPointer Path,
        JsonDocument Value)
        : PatchElement(Path), IDisposable
    {
        public void Dispose() => Value.Dispose();
    }
}
