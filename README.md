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

Call `create` with the lines of your configuration. It accepts any sequence of
strings, so you can pass a list you built yourself or the lines you read from a file:

```fsharp
let ini =
    Ini.create
        [ "global_key=global_value"
          "[one]"
          "one_key=one_value"
          "[one.two]"
          "two_key=two_value" ]
```

You can also start from an empty configuration with `Ini.empty` and add lines with
`Ini.append`. Every function returns a new `Ini`; the original is never mutated, so
`append` yields a fresh configuration that combines the existing entries with the new
ones (later values override earlier ones for the same key):

```fsharp
let ini = Ini.empty |> Ini.append [ "[one]"; "one_key=one_value" ]
```

Keys are addressed with dot notation. A section name and a parameter name are joined
with a dot, and nested sections simply chain more dots:

- `one.one_key` — the `one_key` parameter in section `[one]`
- `one.two.two_key` — the `two_key` parameter in section `[one.two]`
- `.global_key` — a parameter that lives outside any section (note the leading dot)

### Looking up a value

Call `tryFind` to get a value by its full key. It returns `Some value` when the key
exists and `None` otherwise:

```fsharp
let value =
    match ini |> Ini.tryFind "one.one_key" with
    | Some value -> value
    | None -> "none"
// value = "one_value"

let value =
    match ini |> Ini.tryFind "one.missing" with
    | Some value -> value
    | None -> "none"
// value = "none"
```

Lookups are exact, but the leading dot that roots a key is optional: `one.one_key` and
`.one.one_key` resolve to the same value. A bare parameter name such as `global_key` is
therefore treated as a global value.

Global values live at the root and are addressed with a leading dot, which you may omit:

```fsharp
let value = ini |> Ini.tryFind ".global_key"
// value = Some "global_value"

let value = ini |> Ini.tryFind "global_key"
// value = Some "global_value"  (the leading dot is added for you)
```

### Falling back to a parent section

Call `tryFindNested` to look up a parameter and, if it is not present in the given
section, fall back to each parent section in turn. The parameter name (the last segment
of the key) stays fixed while the search walks up the section hierarchy, ending at the
global section. This is useful when an inner section should inherit a setting from an
outer one.

For the key `a.b.c` the parameter is `c` and the following keys are tried in order until
one is found:

- `a.b.c` — `c` in section `[a.b]`
- `a.c` — `c` in the parent section `[a]`
- `.c` — `c` in the global section

```fsharp
let ini = Ini.create [ "[a]"; "c=parent"; "[a.b]"; "other=value" ]

let value = ini |> Ini.tryFindNested "a.b.c"
// value = Some "parent"  (inherited from the parent section [a])
```

The value from the nearest section wins, so if `c` also existed in `[a.b]` that value
would be returned instead. `tryFindNested` returns `Some value` when the parameter is
found in any section along the way and `None` otherwise.

### Parsing rules

- Blank and whitespace-only lines are ignored.
- Whitespace around keys and values is trimmed; whitespace inside a value is kept
  (`key = hello world` yields `hello world`).
- Everything after a `#` is treated as a comment and removed. A line that is entirely
  a comment is dropped.

### Case sensitivity

Keys are always matched case-insensitively (using `StringComparer.OrdinalIgnoreCase`), so
`ONE.ONE_KEY` finds the same value as `one.one_key`.

```fsharp
let value = ini |> Ini.tryFind "ONE.ONE_KEY"
// value = Some "one_value"
```

### Using Fini from C#

Fini ships a C# friendly facade on the `Ini` type. The same immutable configuration is
exposed through static factory methods and instance methods, and lookups follow the
`bool`/`out` `Try...` pattern that C# developers expect.

Add a `using` directive for the namespace:

```csharp
using Fini;
```

#### Creating a configuration

Call `Ini.Create` with the lines of your configuration. It accepts any `IEnumerable<string>`:

```csharp
var ini = Ini.Create(
[
    "global_key=global_value",
    "[one]",
    "one_key=one_value",
    "[one.two]",
    "two_key=two_value"
]);
```

Use `Ini.Empty` to start from an empty configuration, and `Append` to add more lines.
Every operation returns a new `Ini`; the original is never mutated:

```csharp
var ini = Ini.Empty.Append(["[one]", "one_key=one_value"]);
```

#### Looking up a value

`TryFind` returns `true` and sets the `out` parameter when the key exists, and returns
`false` with a `null` value otherwise:

```csharp
if (ini.TryFind("one.one_key", out var value))
{
    // value == "one_value"
}

if (!ini.TryFind("one.missing", out var missing))
{
    // missing == null
}
```

The leading dot that roots a key is optional, so `global_key` and `.global_key` resolve
to the same global value.

#### Falling back to a parent section

`TryFindNested` looks up a parameter and, if it is not present in the given section, falls
back to each parent section in turn, ending at the global section:

```csharp
var ini = Ini.Create(["[a]", "c=parent", "[a.b]", "other=value"]);

if (ini.TryFindNested("a.b.c", out var value))
{
    // value == "parent"  (inherited from the parent section [a])
}
```

#### Case sensitivity

Keys are always matched case-insensitively (using `StringComparer.OrdinalIgnoreCase`), so
`ONE.ONE_KEY` finds the same value as `one.one_key`:

```csharp
ini.TryFind("ONE.ONE_KEY", out var value);  // true, value == "one_value"
```
