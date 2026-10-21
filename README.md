# Fini

[![NuGet](https://img.shields.io/nuget/vpre/Fini.svg)](https://www.nuget.org/packages/Fini)
[![Build](https://github.com/glokhov/fini/actions/workflows/build.yml/badge.svg)](https://github.com/glokhov/fini/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/glokhov/fini/blob/main/LICENSE)

A simple INI parser written in F#.

> [!WARNING]
> Version 0.3.0 is a complete rewrite of version 0.2.1 and is **not** source- or binary-compatible with it.
> The document model and the entire API surface have changed — key shape, error handling, and a first-class
> C# surface are all new. Upgrading from version 0.2.1 means migrating code.

A document is **immutable**: every operation that would change it returns a new one, two documents are equal when they hold
the same keys and values, and the whole model is flat — one parameter per key, keyed `section:parameter`.

## Install

```shell
dotnet add package Fini --prerelease
```

Targets `net48` and `net10.0`. The only dependency is `FSharp.Core`.

## Quick start

Given `app.ini`:

```ini
; a parameter before the first header belongs to the root section
timeout = 30

[server]
host = localhost
port = 8080

[server.dev]
port = 5000
```

### F#

```fsharp
open Fini

match Ini.fromFile "app.ini" with
| Error err -> eprintfn $"%A{err}"
| Ok ini ->

// exact lookup
ini |> Ini.tryFind "server:host"              // Some "localhost"
ini |> Ini.tryFind "server.dev:host"          // None
ini |> Ini.tryFind "timeout"                  // Some "30" — no colon means the root section

// lookup that walks up the section hierarchy
ini |> Ini.tryFindNested "server.dev:port"    // Some "5000"  — from the section itself
ini |> Ini.tryFindNested "server.dev:host"    // Some "localhost" — inherited from [server]
ini |> Ini.tryFindNested "server.dev:timeout" // Some "30"    — inherited from the root

// lookup that parses the value — any IParsable type, net10.0 only
ini |> Ini.tryFindValue<int> "server:port"    // Some (Ok 8080)
```

Building a document, and rendering it back:

```fsharp
let ini =
    Ini.empty
    |> Ini.add "timeout" "30"
    |> Result.bind (Ini.add "server:host" "localhost")
    |> Result.bind (Ini.add "server:port" "8080")

ini |> Result.map Ini.toString // Ok "timeout = 30\n\n[server]\nhost = localhost\nport = 8080"
```

`toString` joins the lines with `Environment.NewLine` (`\n` above, `\r\n` on Windows). The functions take
the document last, so they compose with `|>`. `Ini.toFile` writes, `Ini.toLines` yields
the lines lazily.

### C#

```csharp
using Fini;

var result = Ini.FromFile("app.ini");

if (result.IsError)
{
    Console.Error.WriteLine(result.ErrorValue); // e.g. InvalidLine "oops"
    return;
}

Ini ini = result.ResultValue;

string? host = ini.TryFind("server:host")?.Value;           // "localhost"
string? port = ini.TryFindNested("server.dev:port")?.Value; // "5000"

// a miss is null, so an option pattern reads naturally
if (ini.TryFind("server:tls") is { Value: var tls })
{
    Console.WriteLine(tls);
}

// every change returns a new document
Ini next = ini.Add("server:tls", "true").ResultValue;

foreach (var (key, value) in next) // IEnumerable<KeyValuePair<string, string>>
{
    Console.WriteLine($"{key} = {value}");
}

next.ToFile("app.ini");
```

## F# types from C#

`TryFind` hands back an `FSharpOption<string>` and the parsing functions an
`FSharpResult<Ini, ParserError>` (both from `Microsoft.FSharp.Core`). They are ordinary .NET types, and C#
pattern matching reads them as they are — no wrapper, no conversion helper.

An option is a class whose `None` is `null`, so a hit is a property pattern and a miss is a null check:

```csharp
// is patterns
if (ini.TryFind("server:host") is { Value: var host }) { /* found */ }
if (ini.TryFind("server:tls") is null) { /* absent */ }

// switch expression: null never matches a property pattern,
// so these two arms cover both cases between them
string host = ini.TryFind("server:host") switch
{
    { Value: var value } => value,
    null => "0.0.0.0"
};

// ternary, and the null-conditional shortcut for a default
var port = ini.TryFind("server:port");
int number = port is null ? 8080 : int.Parse(port.Value);
string tls = ini.TryFind("server:tls")?.Value ?? "false";
```

Match constants and guards directly on `Value` — `{ Value: "30" }`, or
`{ Value: var v } when int.Parse(v) > 30`. The instance `IsSome` / `IsNone` properties are not callable from
C#, so reach for `is null` and `is not null` instead.

A result is a struct: never null, and with no case classes to match on. Test `IsOk` / `IsError` and read the
payload through the same pattern:

```csharp
// is pattern
if (Ini.FromFile("app.ini") is { IsOk: true, ResultValue: var config }) { /* parsed */ }

// switch expression, drilling into the error case
string report = Ini.FromFile("app.ini") switch
{
    { IsOk: true, ResultValue: var config } => $"{config.Count} parameters",
    { ErrorValue: ParserError.InvalidKey error } => $"bad key: {error.Key}",
    { ErrorValue: ParserError.InvalidLine error } => $"bad line: {error.Line}",
    _ => "unreachable"
};

// ternary
var result = Ini.FromFile("app.ini");
Ini ini = result.IsOk ? result.ResultValue : Ini.Empty;
```

`ResultValue` and `ErrorValue` are unchecked: reading the one the result does not hold yields `default` — a
`null` document — rather than raising, so test the case before reading the payload.

## Keys

A key is `section:parameter`. Both halves must avoid whitespace and the characters `=`, `:`, `;`, `#`, `[`,
`]`; the parameter half must be non-empty, the section half may be empty and then means the root section —
so `x` and `:x` are the same key. Keys are compared `OrdinalIgnoreCase` in both halves, values are not.

| Key               | |
|-------------------|---------------------------------------|
| `server:host`     | valid                                 |
| `host`, `:host`   | valid, the root section               |
| `server.dev:port` | valid — a dot is part of the name     |
| `a b`             | `InvalidKey` — whitespace             |
| `server:`         | `InvalidKey` — empty parameter        |
| `a:b:c`           | `InvalidKey` — two colons             |

Keys are never trimmed or repaired, so a key the parser would reject is simply absent on the way out:
`TryFind` gives `None`, `ContainsKey` gives `false` and `Remove` is a no-op. On the way out a root
parameter is written in full, so `keys` and enumeration give `:timeout`, not `timeout`.

## File format

| Line             | Meaning                                                      |
|------------------|--------------------------------------------------------------|
| `[name]`         | opens a section                                              |
| `name = value`   | a parameter in the current section                           |
| `; text`         | comment                                                      |
| `# text`         | comment                                                      |
| blank            | ignored                                                      |
| anything else    | `InvalidLine`                                                |

A value is everything after the first `=`, with surrounding whitespace trimmed, so it may contain `=`, `:`,
`;`, `#` and may be empty. A later parameter overwrites an earlier one with the same key, and a section may
be reopened later on. Parsing is a single lazy pass that stops at the first unparsable line — a 10,000-line
file with a bad line at position 3 reads three lines.

Sections do not nest in the file: `[server.dev]` is a name of its own, stored verbatim. The dots only matter
to `tryFindNested` / `TryFindNested`, which walk up from `server.dev` to `server` to the root.

## Rendering

`toLines` / `ToLines` produce a canonical document: root parameters first with no header, then one
`[section]` block per section separated by a blank line, all in key order — the root section first, then
sections `OrdinalIgnoreCase` with a parent ahead of its children, then parameters within a section. An empty
value renders as `x =`.

The output is normalised, not a faithful copy of the input: comments, blank lines, original ordering and
duplicate entries are not preserved. Values are, so rendering and parsing back gives an equal document.

## Layering

`appendLines` / `appendFile` read a layer over an existing document: the layer's parameters win on a shared
key, the document's own parameters survive. Parsing always starts at the root section, so load order decides
which value wins, never which key a line writes to.

```fsharp
let config =
    Ini.fromFile "defaults.ini"
    |> Result.bind (Ini.appendFile "app.ini")
    |> Result.bind (Ini.appendLines [ "[server]"; $"port = {portFromArgs}" ])
```

## Sections

`section` takes one section as a standalone document, `removeSection` is its complement, and `sections`
lists the distinct names (`""` for the root). Dotted children are sections of their own, so removing
`server` leaves `server.dev` in place — remove it by name, or fold over `sections` to drop a whole subtree.

```fsharp
ini |> Ini.section "server" |> Ini.toLines   // seq [ "[server]"; "host = localhost"; "port = 8080" ]
```

## Typed lookup

One generic lookup parses the value it finds as any `IParsable<T>` — the primitives, `Guid`, `DateTime`,
`TimeSpan`, and your own types — under `CultureInfo.InvariantCulture`. It returns an option of a result:
`None` for a missing key, `Some (Error (InvalidValue value))` for a value that does not parse.

```fsharp
ini |> Ini.tryFindValue<int> "server:port"            // Some (Ok 8080)
ini |> Ini.tryFindValue<Guid> "server:id"             // Some (Ok ...)
ini |> Ini.tryFindNestedValue<TimeSpan> "server.dev:timeout"
```

```csharp
if (ini.TryFindValue<int>("server:port") is { Value: { IsOk: true, ResultValue: var port } })
{
    Console.WriteLine(port);
}
```

`IParsable<T>` takes no `NumberStyles`, so each type parses with its own default: `tryFindValue<float>`
accepts a thousands separator and so reads `"1,234"` as `Ok 1234.0`, where `tryFindValue<int>` does not
and gives `Error (InvalidValue "1,234")`.

`TryFindValue` / `TryFindNestedValue` (`tryFindValue` / `tryFindNestedValue` in F#) are compiled only into
the `net10.0` build — `net48` has no `IParsable<T>`, and so no parsing lookup and no `InvalidValue` case
on `ParserError`.

## Errors

Content failures are returned, not raised:

```fsharp
type ParserError =
    | InvalidKey of Key: string
    | InvalidLine of Line: string
    | InvalidValue of Value: string // net10.0 only
```

Each case carries the offending text verbatim. A missing key is an answer rather than a failure:
`tryFind` / `tryFindNested` return an option. Their parsing counterparts `tryFindValue` /
`tryFindNestedValue` return an option of a result, `Some (Error (InvalidValue value))` when the key is
found but its value does not parse. A `null` argument and a filesystem problem are raised.

`InvalidValue` only ever comes out of those two, and both they and the case itself are compiled only into
the `net10.0` build: on `net48` the type has two cases, so a match that handles `InvalidValue` does not
compile there.

From C# the cases are nested types that carry their payload under the field name — `((ParserError.InvalidKey)err).Key`,
`((ParserError.InvalidLine)err).Line` and, on `net10.0`, `((ParserError.InvalidValue)err).Value` — with
`IsInvalidKey` / `IsInvalidLine` / `IsInvalidValue` to test one and `NewInvalidKey` / `NewInvalidLine` /
`NewInvalidValue` to build one.

## API

The type and the module carry the same operations; the module functions take the document last.

| `Ini` member                     | `Ini` function                          | Result                        |
|----------------------------------|-----------------------------------------|-------------------------------|
| `Ini.Empty`                      | `Ini.empty`                             | `Ini`                         |
| `Ini.Create pairs`               | `Ini.ofSeq pairs`                       | `Result<Ini, ParserError>`    |
| `Ini.FromLines lines`            | `Ini.fromLines lines`                   | `Result<Ini, ParserError>`    |
| `Ini.FromFile path`              | `Ini.fromFile path`                     | `Result<Ini, ParserError>`    |
| `ini.AppendLines lines`          | `Ini.appendLines lines ini`             | `Result<Ini, ParserError>`    |
| `ini.AppendFile path`            | `Ini.appendFile path ini`               | `Result<Ini, ParserError>`    |
| `ini.Add(key, value)`            | `Ini.add key value ini`                 | `Result<Ini, ParserError>`    |
| `ini.TryFind key`                | `Ini.tryFind key ini`                   | `string option`               |
| `ini.TryFindValue<T> key`        | `Ini.tryFindValue<'T> key ini`          | `Result<'T, ParserError> option` — `net10.0` only |
| `ini.TryFindNested key`          | `Ini.tryFindNested key ini`             | `string option`               |
| `ini.TryFindNestedValue<T> key`  | `Ini.tryFindNestedValue<'T> key ini`    | `Result<'T, ParserError> option` — `net10.0` only |
| `ini.Remove key`                 | `Ini.remove key ini`                    | `Ini`                         |
| `ini.ContainsKey key`            | `Ini.containsKey key ini`               | `bool`                        |
| `ini.IsEmpty`                    | `Ini.isEmpty ini`                       | `bool`                        |
| `ini.Count`                      | `Ini.count ini`                         | `int`                         |
| `ini.Keys`                       | `Ini.keys ini`                          | `seq<string>`                 |
| `ini.Values`                     | `Ini.values ini`                        | `seq<string>`                 |
| `foreach` / LINQ over `ini`      | `Ini.toSeq ini`                         | pairs, in key order           |
| `ini.Sections`                   | `Ini.sections ini`                      | `seq<string>`                 |
| `ini.Section name`               | `Ini.section name ini`                  | `Ini`                         |
| `ini.RemoveSection name`         | `Ini.removeSection name ini`            | `Ini`                         |
| `ini.ToLines()`                  | `Ini.toLines ini`                       | `seq<string>`                 |
| `ini.ToFile path`                | `Ini.toFile path ini`                   | `unit`                        |
| `ini.ToString()`                 | `Ini.toString ini`                      | `string`                      |

There is no pairs property on the type: from C# the document itself is the sequence of pairs, so `foreach`,
LINQ and `Ini.Create(ini)` take it directly.

`Ini` implements `IEnumerable<KeyValuePair<string, string>>`, `IEquatable<Ini>` and `IComparable<Ini>`:
documents are compared by keys and then values, and equal documents have equal hash codes. Name case,
declaration order, comments and blank lines are not taken into account.

## Build

```shell
dotnet build
dotnet test
```

## License

[MIT](https://github.com/glokhov/fini/blob/main/LICENSE)
