# Finite.AspNetCore.JsonPatch #

A simple(ish) JSON Patch implementation for ASP.NET Core using System.Text.Json
instead of Newtonsoft.Json

## Why? ##

The "built-in" JSON Patch library requires Newtonsoft.Json... I don't want to
use Newtonsoft.Json.

## How? ##

In Startup:
```cs
void ConfigureServiceS(IServiceCollection services)
{
    _ = services.AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.Converters.Add(
                new JsonPatchConverter());
            options.JsonSerializerOptions.Converters.Add(
                new JsonPointerConverter());
        })
}
```
In your controller, use something like:
```cs
// N.B. this isn't error checked or anything, so it's probably broken.
// demonstrative purposes only!
[HttpPatch]
public IActionResult Patch(
    [FromBody, Required]JsonPatch patch)
{
    // or loaded from somewhere
    using var currentValue = JsonDocument.Parse("{}");
    using var writer = new Utf8JsonWriter(Response.BodyWriter);

    if (!patch.TryApply(writer,
        currentValue.RootElement))
    {
        // patch didn't apply
        return BadRequest();
    }

    // flush the writer
    writer.Flush();
    return Ok();
}

// Or:
[HttpPatch]
public async Task<IActionResult> Patch(
    [FromBody, Required]JsonPatch patch)
{
    // GetJsonDocumentFromDatabase should likely register the JsonDocument for
    // dispose (Response.RegisterForDispose)
    var currentValue = await GetJsonDocumentFromDatabaseAsync();
    var array = new ArrayBufferWriter<byte>();
    using var writer = new Utf8JsonWriter(array);

    if (!patch.TryApply(writer,
        currentValue.RootElement))
    {
        // patch didn't apply
        return BadRequest();
    }

    // flush the writer so we can re-parse it
    writer.Flush();

    // parse the output as a json document
    using var finalValue = JsonDocument.Parse(array.WrittenMemory);
    await SaveToDatabaseAsync(finalValue);

    // notify other people that the data in the database has changed
    // (e.g. via pub/sub)
    await BroadcastChanges(finalValue);

    return NoContent();
}
```

### Why Utf8JsonWriter as a parameter to TryApply? ###
I didn't want to expose the internal mutable JSON tree (see MutableJson.cs)
since it's not perfect and probably doesn't work like you expect:
- MutableJsonValue purely represents the tree in a JSON document
- It refers directly to JsonElement-s owned by other JsonDocuments and does not
  copy them.
  - A MutableJsonElement refers to either a JsonDocument owned by the JsonPatch
    or a JsonElement owned by the passed root element in TryApply.
- It is very easy to get into a situation where you get use-after-dispose

As ugly as the current approach is, it mostly guarantees that TryApply won't
throw, and will always produce valid JSON if it returns true.

## TODO ##
- Use built-in writable DOM: https://github.com/dotnet/designs/pull/163
  - When the above is merged, make TryApply have an `out JsonNode` parameter