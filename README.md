# Fini

[![NuGet](https://img.shields.io/nuget/vpre/Fini.svg)](https://www.nuget.org/packages/Fini)
[![Build](https://github.com/glokhov/fini/actions/workflows/build.yml/badge.svg)](https://github.com/glokhov/fini/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/glokhov/fini/blob/main/LICENSE)

A simple INI parser written in F#, usable from F# and C# alike.

> [!WARNING]
> Version 0.3.0 is a complete rewrite of version 0.2.1 and is **not** source- or binary-compatible with it.
> The document model and the entire API surface have changed — key shape, error handling, and a first-class
> C# surface are all new. Upgrading from version 0.2.1 means migrating code.

A document is **immutable**: every operation that would change it returns a new one, two documents are equal when they hold
the same keys and values, and the whole model is flat — one parameter per key, keyed `section:parameter`.

## Install

```shell
dotnet add package Fini
```

Targets `net10.0` and `net48`. The only dependency is `FSharp.Core`.

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

F#:

```fsharp
open Fini

let ini =
    match Ini.fromFile "app.ini" with
    | Ok ini -> ini
    | Error err -> failwith $"%A{err}"        // InvalidLine "oops"

ini |> Ini.tryFind "server:host"              // Some "localhost"
ini |> Ini.tryFind "server.dev:host"          // None — exact lookup
ini |> Ini.tryFind "timeout"                  // Some "30" — no colon means the root section

ini |> Ini.tryFindNested "server.dev:port"    // Some "5000"      — from the section itself
ini |> Ini.tryFindNested "server.dev:host"    // Some "localhost" — inherited from [server]
ini |> Ini.tryFindNested "server.dev:timeout" // Some "30"        — inherited from the root

ini |> Ini.tryFindValue<int> "server:port"    // Some (Ok 8080) — net10.0 only
```

C#:

```csharp
using Fini;

Ini ini = Ini.FromFile("app.ini") switch
{
    { ResultValue: { } ok } => ok,
    { ErrorValue: var err } => throw new Exception(err.ToString()) // InvalidLine "oops"
};

string? host = ini.TryFind("server:host")?.Value;                  // "localhost"
string? dHost = ini.TryFind("server.dev:host")?.Value;             // null — exact lookup
string? timeout = ini.TryFind("timeout")?.Value;                   // "30" — no colon means the root section

string? dPort = ini.TryFindNested("server.dev:port")?.Value;       // "5000"      — from the section itself
string? iHost = ini.TryFindNested("server.dev:host")?.Value;       // "localhost" — inherited from [server]
string? iTimeout = ini.TryFindNested("server.dev:timeout")?.Value; // "30"     — inherited from the root

int? port = ini.TryFindValue<int>("server:port") is { Value: { ErrorValue: null, ResultValue: var p } } ? p : null; // 8080 — net10.0 only
```

The F# surface is the `Ini` module, its functions taking the document last so they compose with `|>`. The C#
surface is the members of the `Ini` type. Both return the same F# `option` and `Result` values; from C# they
are `FSharpOption<T>` and `FSharpResult<T, TError>` in `Microsoft.FSharp.Core`, ordinary .NET types that a C#
switch expression reads directly. Every example below is given in both languages, F# `match` mirrored by a C#
`switch`.

## Lookups return an option

A missing key is an answer, not a failure: `tryFind` / `TryFind` and `tryFindNested` / `TryFindNested`
return `Some value` or `None`.

F#:

```fsharp
// both cases
let message =
    match ini |> Ini.tryFind "server:host" with
    | Some host -> $"host: {host}"
    | None -> "no host"

// default
let host =
    match ini |> Ini.tryFind "server:host" with
    | Some host -> host
    | None -> "0.0.0.0"

// transform
let port =
    match ini |> Ini.tryFind "server:port" with
    | Some port -> int port
    | None -> 8080

// match on the value
let timeout =
    match ini |> Ini.tryFind "timeout" with
    | Some "0" -> "no timeout"
    | Some t -> $"timeout: {t}"
    | None -> "default timeout"
```

C#: an option is a class whose `None` is `null`, so `Some` is a property pattern on `Value` and `None` is
the `null` arm. A `null` never matches a property pattern, so the two arms cover every case and the compiler
needs no discard.

```csharp
// both cases
string message = ini.TryFind("server:host") switch
{
    { Value: var h } => $"host: {h}",
    null => "no host"
};

// default
string host = ini.TryFind("server:host") switch
{
    { Value: var h } => h,
    null => "0.0.0.0"
};

// transform
int port = ini.TryFind("server:port") switch
{
    { Value: var p } => int.Parse(p),
    null => 8080
};

// match on the value
string timeout = ini.TryFind("timeout") switch
{
    { Value: "0" } => "no timeout",
    { Value: var t } => $"timeout: {t}",
    null => "default timeout"
};
```

Reading `Value` on a `None` throws `NullReferenceException`; the `null` arm is what keeps it from being
read. The `IsSome` / `IsNone` members compile to static helpers that C# cannot call. `var` keeps
`Microsoft.FSharp.Core` out of the `using` list; name the type only when you need to, as
`FSharpOption<string>`.

## Building and parsing return a result

Everything that reads a key or a line it might reject — `fromLines`, `fromFile`, `appendLines`,
`appendFile`, `add`, `ofSeq` — returns `Ok ini` or `Error err`.

F#:

```fsharp
// both cases
let text =
    match Ini.empty |> Ini.add "timeout" "30" with
    | Ok ini -> Ini.toString ini // "timeout = 30"
    | Error err -> $"failed: %A{err}"

// default
let config =
    match Ini.fromFile "app.ini" with
    | Ok ini -> ini
    | Error _ -> Ini.empty

// chaining — each step runs only on Ok, so the first error is the one kept
let built =
    match Ini.empty |> Ini.add "timeout" "30" with
    | Ok ini -> ini |> Ini.add "server:host" "localhost"
    | Error err -> Error err

let built' =
    match built with
    | Ok ini -> ini |> Ini.add "server:port" "8080"
    | Error err -> Error err
```

`Result.bind` is the shorthand for the chaining match:
`Ini.empty |> Ini.add "timeout" "30" |> Result.bind (Ini.add "server:host" "localhost")`.

C#: a result is a struct — never null, with no case classes to match on. It holds both `ResultValue` and
`ErrorValue`, and the one it does not hold reads as `default`. A document is never null on `Ok`, so
`{ ResultValue: { } ini }` is the `Ok` arm and `{ ErrorValue: var err }` after it is the `Error` arm.

```csharp
// both cases
string text = Ini.Empty.Add("timeout", "30") switch
{
    { ResultValue: { } ini } => ini.ToString(), // "timeout = 30"
    { ErrorValue: var err } => $"failed: {err}"
};

// default
Ini config = Ini.FromFile("app.ini") switch
{
    { ResultValue: { } ini } => ini,
    { ErrorValue: _ } => Ini.Empty
};

// chaining — each step runs only on Ok, so the first error is the one kept
var built = Ini.Empty.Add("timeout", "30") switch
{
    { ResultValue: { } ini } => ini.Add("server:host", "localhost"),
    var error => error
};

built = built switch
{
    { ResultValue: { } ini } => ini.Add("server:port", "8080"),
    var error => error
};
```

When the payload is a value type, as in the typed lookups below, `ResultValue` is `0` or `false` rather than
`null` on an error, so the `ErrorValue: { } err` arm goes first — a `ParserError` is never null.

## Errors

Content failures are returned, not raised:

```fsharp
type ParserError =
    | InvalidKey of Key: string
    | InvalidLine of Line: string
    | InvalidValue of Value: string // net10.0 only
```

Each case carries the offending text verbatim. `InvalidKey` comes from `add` / `ofSeq`, `InvalidLine` from
the four parsing functions, `InvalidValue` from the typed lookups. A `null` argument and a filesystem problem
are raised as exceptions.

F#:

```fsharp
let describe err =
    match err with
    | InvalidKey key -> $"bad key: {key}"
    | InvalidLine line -> $"bad line: {line}"
    | InvalidValue value -> $"bad value: {value}" // net10.0 only

let report =
    match Ini.fromFile "app.ini" with
    | Ok ini -> $"{Ini.count ini} parameters"
    | Error err -> describe err

// build a case
let expected = InvalidLine "oops"
```

C#: the cases are nested types carrying their payload under the field name, so a type pattern with a
property pattern reaches both.

```csharp
static string Describe(ParserError err) => err switch
{
    ParserError.InvalidKey { Key: var key } => $"bad key: {key}",
    ParserError.InvalidLine { Line: var line } => $"bad line: {line}",
    ParserError.InvalidValue { Value: var value } => $"bad value: {value}", // net10.0 only
    _ => "unreachable"
};

string report = Ini.FromFile("app.ini") switch
{
    { ResultValue: { } ini } => $"{ini.Count} parameters",
    { ErrorValue: var err } => Describe(err)
};

// build a case
ParserError expected = ParserError.NewInvalidLine("oops");
```

The compiler cannot prove a C# switch over `ParserError` exhaustive, hence the discard arm. Errors are
structurally equal — `ParserError.NewInvalidLine("oops")` equals any other `InvalidLine "oops"` — and print as
`InvalidLine "oops"`.

`InvalidValue` and the typed lookups are compiled only into the `net10.0` build: on `net48` the type has two
cases, so a match that names `InvalidValue` does not compile there.

## Typed lookup

`tryFindValue` / `TryFindValue` and `tryFindNestedValue` / `TryFindNestedValue` parse the value as any
`IParsable<T>` — the primitives, `Guid`, `DateTime`, `TimeSpan`, and your own types — under
`CultureInfo.InvariantCulture`. They return an option of a result, keeping the two outcomes apart: `None`
for a missing key, `Some (Error (InvalidValue value))` for a value that does not parse.

F#:

```fsharp
let port =
    match ini |> Ini.tryFindValue<int> "server:port" with
    | Some (Ok port) -> port
    | Some (Error err) -> failwith (describe err)
    | None -> 8080

let timeout =
    match ini |> Ini.tryFindNestedValue<TimeSpan> "server.dev:timeout" with
    | Some (Ok timeout) -> timeout
    | Some (Error (InvalidValue text)) -> failwith $"not a TimeSpan: {text}"
    | Some (Error err) -> failwith (describe err)
    | None -> TimeSpan.FromSeconds 30.0
```

C#: the option's `Value` is the result, so the patterns nest. `int` and `TimeSpan` are value types, so the
error arms come before the `ResultValue` arm.

```csharp
int port = ini.TryFindValue<int>("server:port") switch
{
    { Value.ErrorValue: { } err } => throw new FormatException(Describe(err)),
    { Value.ResultValue: var p } => p,
    null => 8080
};

TimeSpan timeout = ini.TryFindNestedValue<TimeSpan>("server.dev:timeout") switch
{
    { Value.ErrorValue: ParserError.InvalidValue { Value: var text } } => throw new FormatException($"not a TimeSpan: {text}"),
    { Value.ErrorValue: { } err } => throw new FormatException(Describe(err)),
    { Value.ResultValue: var t } => t,
    null => TimeSpan.FromSeconds(30)
};
```

`IParsable<T>` takes no `NumberStyles`, so each type parses with its own default: `float` / `double` accepts
a thousands separator and so reads `"1,234"` as `1234.0`, where `int` does not and gives
`InvalidValue "1,234"`.

## Keys

A key is `section:parameter`. Both halves must avoid whitespace and the characters `=`, `:`, `;`, `#`, `[`,
`]`; the parameter half must be non-empty, the section half may be empty and then means the root section —
so `x` and `:x` are the same key. Keys are compared `OrdinalIgnoreCase` in both halves, values are not.

| Key               |                                   |
|-------------------|-----------------------------------|
| `server:host`     | valid                             |
| `host`, `:host`   | valid, the root section           |
| `server.dev:port` | valid — a dot is part of the name |
| `a b`             | `InvalidKey` — whitespace         |
| `server:`         | `InvalidKey` — empty parameter    |
| `a:b:c`           | `InvalidKey` — two colons         |

Keys are never trimmed or repaired, so a key the parser would reject is simply absent on the way out:
`TryFind` gives `None`, `ContainsKey` gives `false` and `Remove` is a no-op. On the way out a root
parameter is written in full, so `keys` and enumeration give `:timeout`, not `timeout`.

## File format

| Line           | Meaning                            |
|----------------|------------------------------------|
| `[name]`       | opens a section                    |
| `name = value` | a parameter in the current section |
| `; text`       | comment                            |
| `# text`       | comment                            |
| blank          | ignored                            |
| anything else  | `InvalidLine`                      |

A value is everything after the first `=`, with surrounding whitespace trimmed, so it may contain `=`, `:`,
`;`, `#` and may be empty. A later parameter overwrites an earlier one with the same key, and a section may
be reopened later on. Parsing is a single lazy pass that stops at the first unparsable line — a 10,000-line
file with a bad line at position 3 reads three lines.

Sections do not nest in the file: `[server.dev]` is a name of its own, stored verbatim. The dots only matter
to the nested lookups, which walk up from `server.dev` to `server` to the root.

## Layering

`appendLines` / `appendFile` read a layer over an existing document: the layer's parameters win on a shared
key, the document's own parameters survive. Parsing always starts at the root section, so load order decides
which value wins, never which key a line writes to.

F#:

```fsharp
let config =
    Ini.fromFile "defaults.ini"
    |> Result.bind (Ini.appendFile "app.ini")
    |> Result.bind (Ini.appendLines [ "[server]"; $"port = {portFromArgs}" ])
```

C#:

```csharp
var config = Ini.FromFile("defaults.ini");
config = config switch { { ResultValue: { } ini } => ini.AppendFile("app.ini"), var error => error };
config = config switch { { ResultValue: { } ini } => ini.AppendLines(["[server]", $"port = {portFromArgs}"]), var error => error };
```

## Sections

`section` takes one section as a standalone document, `removeSection` is its complement, and `sections`
lists the distinct names (`""` for the root). Dotted children are sections of their own, so removing
`server` leaves `server.dev` in place — remove it by name, or fold over the names to drop a whole subtree.

F#:

```fsharp
ini |> Ini.section "server" |> Ini.toLines // seq [ "[server]"; "host = localhost"; "port = 8080" ]

let withoutServer =
    ini
    |> Ini.sections
    |> Seq.filter (fun name -> name = "server" || name.StartsWith "server.")
    |> Seq.fold (fun doc name -> Ini.removeSection name doc) ini
```

C#:

```csharp
ini.Section("server").ToLines(); // [ "[server]", "host = localhost", "port = 8080" ]

Ini withoutServer = ini.Sections
    .Where(name => name == "server" || name.StartsWith("server."))
    .Aggregate(ini, (doc, name) => doc.RemoveSection(name));
```

## Enumeration and rendering

From F#, `Ini.toSeq` gives `(key, value)` tuples and `Ini.ofSeq` takes them back. From C#, the document
itself is an `IEnumerable<KeyValuePair<string, string>>`, so `foreach`, LINQ and `Ini.Create(ini)` take it
directly.

```fsharp
for key, value in Ini.toSeq ini do
    printfn $"{key} = {value}"
```

```csharp
foreach (var (key, value) in ini)
{
    Console.WriteLine($"{key} = {value}");
}
```

`toLines` / `ToLines` produce a canonical document: root parameters first with no header, then one
`[section]` block per section separated by a blank line, all in key order — the root section first, then
sections `OrdinalIgnoreCase` with a parent ahead of its children, then parameters within a section. An empty
value renders as `x =`. `toString` / `ToString` joins the lines with `Environment.NewLine`, and
`toFile` / `ToFile` writes them.

The output is normalised, not a faithful copy of the input: comments, blank lines, original ordering and
duplicate entries are not preserved. Values are, so rendering and parsing back gives an equal document.

## API

| `Ini` member                    | `Ini` function                       | Result                                            |
|---------------------------------|--------------------------------------|---------------------------------------------------|
| `Ini.Empty`                     | `Ini.empty`                          | `Ini`                                             |
| `Ini.Create pairs`              | `Ini.ofSeq pairs`                    | `Result<Ini, ParserError>`                        |
| `Ini.FromLines lines`           | `Ini.fromLines lines`                | `Result<Ini, ParserError>`                        |
| `Ini.FromFile path`             | `Ini.fromFile path`                  | `Result<Ini, ParserError>`                        |
| `ini.AppendLines lines`         | `Ini.appendLines lines ini`          | `Result<Ini, ParserError>`                        |
| `ini.AppendFile path`           | `Ini.appendFile path ini`            | `Result<Ini, ParserError>`                        |
| `ini.Add(key, value)`           | `Ini.add key value ini`              | `Result<Ini, ParserError>`                        |
| `ini.TryFind key`               | `Ini.tryFind key ini`                | `string option`                                   |
| `ini.TryFindValue<T> key`       | `Ini.tryFindValue<'T> key ini`       | `Result<'T, ParserError> option` — `net10.0` only |
| `ini.TryFindNested key`         | `Ini.tryFindNested key ini`          | `string option`                                   |
| `ini.TryFindNestedValue<T> key` | `Ini.tryFindNestedValue<'T> key ini` | `Result<'T, ParserError> option` — `net10.0` only |
| `ini.Remove key`                | `Ini.remove key ini`                 | `Ini`                                             |
| `ini.ContainsKey key`           | `Ini.containsKey key ini`            | `bool`                                            |
| `ini.IsEmpty`                   | `Ini.isEmpty ini`                    | `bool`                                            |
| `ini.Count`                     | `Ini.count ini`                      | `int`                                             |
| `ini.Keys`                      | `Ini.keys ini`                       | `seq<string>`                                     |
| `ini.Values`                    | `Ini.values ini`                     | `seq<string>`                                     |
| `foreach` / LINQ over `ini`     | `Ini.toSeq ini`                      | pairs, in key order                               |
| `ini.Sections`                  | `Ini.sections ini`                   | `seq<string>`                                     |
| `ini.Section name`              | `Ini.section name ini`               | `Ini`                                             |
| `ini.RemoveSection name`        | `Ini.removeSection name ini`         | `Ini`                                             |
| `ini.ToLines()`                 | `Ini.toLines ini`                    | `seq<string>`                                     |
| `ini.ToFile path`               | `Ini.toFile path ini`                | `unit`                                            |
| `ini.ToString()`                | `Ini.toString ini`                   | `string`                                          |

From C#, `string option` is `FSharpOption<string>` and `Result<Ini, ParserError>` is
`FSharpResult<Ini, ParserError>`.

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
