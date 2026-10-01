# INI configuration file [![Nuget Version](https://img.shields.io/nuget/v/Fini)](https://www.nuget.org/packages/Fini)

> ⚠️ **This version is fully incompatible with the previous version 2.**
> The API has been completely redesigned. Code written against version 2 will not
> compile or behave the same way. If you are upgrading, expect to rewrite the parts
> of your code that use Fini.

An ***immutable*** collection of key-value pairs organized in sections.

### Getting started

Import the `Fini` namespace:

```fsharp
open Fini
```

Suppose you have the following configuration:

```ini
global_key=global_value
[one]
one_key=one_value
[one.two]
two_key=two_value
```

### Creating a configuration

Call `tryCreate` with the lines of your configuration. It accepts any sequence of
strings, so you can pass a list you built yourself or the lines you read from a file.
Configuration files are typically user-authored and untrusted, so a malformed line (for
example a stray `:` in a section or parameter name) is not a programming error — it is
data. `tryCreate` never throws for a parse failure; it returns `Result<Ini, string>`, so
a bad line surfaces as `Error message` instead of an exception:

```fsharp
let ini =
    Ini.tryCreate
        [ "global_key=global_value"
          "[one]"
          "one_key=one_value"
          "[one.two]"
          "two_key=two_value" ]
    |> Result.defaultWith (fun error -> failwith error)
```

You can also start from an empty configuration with `Ini.empty` and add lines with
`Ini.tryAppend`. Every function returns a new `Ini`; the original is never mutated, so
`tryAppend` yields a fresh configuration that combines the existing entries with the new
ones (later values override earlier ones for the same key). Like `tryCreate`,
`tryAppend` returns `Result<Ini, string>` instead of throwing on a malformed line:

```fsharp
let ini =
    Ini.empty
    |> Ini.tryAppend [ "[one]"; "one_key=one_value" ]
    |> Result.defaultWith (fun error -> failwith error)
```

Keys are addressed as `section:parameter`. Dots separate nested section names, and a
colon separates the section path from the parameter name:

- `one:one_key` — the `one_key` parameter in section `[one]`
- `one.two:two_key` — the `two_key` parameter in section `[one.two]`
- `:global_key` — a parameter that lives outside any section (note the leading colon)

Because the colon marks the boundary, a parameter name may itself contain dots without
becoming ambiguous: `one.two:a.b.c` is the `a.b.c` parameter in section `[one.two]`, and
it is a different key from `one:two.a.b.c`.

### Looking up a value

Call `tryFind` to get a value by its full key. It returns `Some value` when the key
exists and `None` otherwise:

```fsharp
let value =
    match ini |> Ini.tryFind "one:one_key" with
    | Some value -> value
    | None -> "none"
// value = "one_value"

let value =
    match ini |> Ini.tryFind "one:missing" with
    | Some value -> value
    | None -> "none"
// value = "none"
```

Lookups are exact. A key with no colon is read as a bare parameter name in the global
section, so `global_key` and `:global_key` resolve to the same value.

Global values live at the root and are addressed with a leading colon, which you may omit:

```fsharp
let value = ini |> Ini.tryFind ":global_key"
// value = Some "global_value"

let value = ini |> Ini.tryFind "global_key"
// value = Some "global_value"  (the leading colon is added for you)
```

### Falling back to a parent section

Call `tryFindNested` to look up a parameter and, if it is not present in the given
section, fall back to each parent section in turn. The parameter name (everything after
the colon) stays fixed while the search walks up the section hierarchy, ending at the
global section. This is useful when an inner section should inherit a setting from an
outer one.

For the key `a.b:c` the parameter is `c` and the following keys are tried in order until
one is found:

- `a.b:c` — `c` in section `[a.b]`
- `a:c` — `c` in the parent section `[a]`
- `:c` — `c` in the global section

```fsharp
let ini =
    Ini.tryCreate [ "[a]"; "c=parent"; "[a.b]"; "other=value" ]
    |> Result.defaultWith (fun error -> failwith error)

let value = ini |> Ini.tryFindNested "a.b:c"
// value = Some "parent"  (inherited from the parent section [a])
```

The value from the nearest section wins, so if `c` also existed in `[a.b]` that value
would be returned instead. `tryFindNested` returns `Some value` when the parameter is
found in any section along the way and `None` otherwise.

Because the parameter name is taken as a whole, dots inside it are never mistaken for
section boundaries. For `one.two:a.b.c` the walk is `one.two:a.b.c` → `one:a.b.c` →
`:a.b.c`.

### Inspecting a configuration

`Ini` also exposes a small read-only inspection surface, so a loaded configuration is
not a black box:

- `Ini.isEmpty ini` — `true` when the configuration holds no entries.
- `Ini.count ini` — the number of stored key-value pairs.
- `Ini.containsKey key ini` — `true` when the (normalized) key exists.
- `Ini.keys ini` — a `string seq` of every stored key.
- `Ini.values ini` — a `string seq` of every stored value, in the same order as `keys`.

```fsharp
Ini.count ini // 4
Ini.containsKey "one:one_key" ini // true
```

### Parsing rules

- Blank and whitespace-only lines are ignored.
- Whitespace around keys and values is trimmed; whitespace inside a value is kept
  (`key = hello world` yields `hello world`).
- Everything after a `#` or a `;` is treated as a comment and removed. A line that is
  entirely a comment is dropped.
- `:` is reserved as the section/parameter separator, so it may not appear in a section
  name or a parameter name. It is allowed inside a *value* (`url=http://example.com`).
- A line that cannot be parsed (stray text, an unclosed `[section`, whitespace inside a
  section or parameter name, a reserved `:` in a section or parameter name, and so on)
  does not throw. `tryCreate`/`tryAppend` return `Error message` for the whole batch
  instead.

### Case sensitivity

Keys are always matched case-insensitively (using `StringComparer.OrdinalIgnoreCase`), so
`ONE:ONE_KEY` finds the same value as `one:one_key`.

```fsharp
let value = ini |> Ini.tryFind "ONE:ONE_KEY"
// value = Some "one_value"
```

### Using Fini from C#

Fini ships a C# friendly facade on the `Ini` type. The same immutable configuration is
exposed through static factory methods and instance methods. Lookups (`TryFind`,
`TryFindNested`) follow the `bool`/`out` `Try...` pattern that C# developers expect.
Creation (`TryCreate`, `TryAppend`) returns F#'s `Result<Ini, string>` instead: a
`bool`/`out` pair can report success or failure, but only `Result` carries *both* the
parsed `Ini` on success *and* the parse error message on failure in a single value, so
that is what's used to propagate the error. It is a little more cumbersome from C#
(`result.IsOk` / `result.ResultValue` / `result.ErrorValue` instead of an `out` parameter),
but it is the only way to hand back the failure reason without a second method or a
nullable tuple.

Add a `using` directive for the namespace (and for `Microsoft.FSharp.Core` to work with
`Result` conveniently):

```csharp
using Fini;
using Microsoft.FSharp.Core;
```

#### Creating a configuration

Call `Ini.TryCreate` with the lines of your configuration. It accepts any
`IEnumerable<string>` and returns `Result<Ini, string>`: `Ok ini` on success, or
`Error message` when a line could not be parsed (configuration files are typically
user-authored, so a parse failure is an expected outcome rather than an exception):

```csharp
var result = Ini.TryCreate(
[
    "global_key=global_value",
    "[one]",
    "one_key=one_value",
    "[one.two]",
    "two_key=two_value"
]);

if (result.IsOk)
{
    var ini = result.ResultValue;
}
```

Use `Ini.Empty` to start from an empty configuration, and `TryAppend` to add more lines
the same way. Every operation returns a new `Ini` wrapped in `Result<Ini, string>`; the
original is never mutated:

```csharp
var result = Ini.Empty.TryAppend(["[one]", "one_key=one_value"]);

if (result.IsOk)
{
    var ini = result.ResultValue;
}
```

#### Looking up a value

`TryFind` returns `true` and sets the `out` parameter when the key exists, and returns
`false` with a `null` value otherwise:

```csharp
if (ini.TryFind("one:one_key", out var value))
{
    // value == "one_value"
}

if (!ini.TryFind("one:missing", out var missing))
{
    // missing == null
}
```

The leading colon is optional for global keys, so `global_key` and `:global_key` resolve
to the same global value. A parameter name may contain dots — `one.two:a.b.c` is the
`a.b.c` parameter in section `[one.two]`.

#### Falling back to a parent section

`TryFindNested` looks up a parameter and, if it is not present in the given section, falls
back to each parent section in turn, ending at the global section:

```csharp
var ini = Ini.TryCreate(["[a]", "c=parent", "[a.b]", "other=value"]).ResultValue;

if (ini.TryFindNested("a.b:c", out var value))
{
    // value == "parent"  (inherited from the parent section [a])
}
```

#### Inspecting a configuration

The facade also exposes the same read-only inspection surface as instance members:

- `ini.IsEmpty` — `true` when the configuration holds no entries.
- `ini.Count` — the number of stored key-value pairs.
- `ini.ContainsKey(key)` — `true` when the (normalized) key exists.
- `ini.Keys` — an `IEnumerable<string>` of every stored key.
- `ini.Values` — an `IEnumerable<string>` of every stored value, in the same order as `Keys`.

```csharp
var count = ini.Count; // 4
var hasKey = ini.ContainsKey("one:one_key"); // true
```

#### Case sensitivity

Keys are always matched case-insensitively (using `StringComparer.OrdinalIgnoreCase`), so
`ONE:ONE_KEY` finds the same value as `one:one_key`:

```csharp
ini.TryFind("ONE:ONE_KEY", out var value);  // true, value == "one_value"
```
