# Fini

[![NuGet](https://img.shields.io/nuget/vpre/Fini.svg)](https://www.nuget.org/packages/Fini)
[![Build](https://github.com/glokhov/fini/actions/workflows/build.yml/badge.svg)](https://github.com/glokhov/fini/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/glokhov/fini/blob/main/LICENSE)

A simple INI parser written in F#, usable from F# and C# alike.

> [!WARNING]
> Version 0.3.0 is a complete rewrite of version 0.2.1 and is **not** compatible with it, in source or in binary form.
> Keys, error handling and the C# API have all changed, so code written for 0.2.1 has to be updated.

Documents are **immutable**: any operation that changes a document returns a new one instead. Two documents are
equal if they have the same keys and values. The structure is flat: each value is stored under one key of the form
`section:parameter`.

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

int? port = ini.TryFindValue<int>("server:port") is { Value: { IsOk: true, ResultValue: var p } } ? p : null; // 8080 — net10.0 only
```

F# code uses the functions in the `Ini` module. They take the document as the last argument, so they work with
`|>`. C# code uses the methods and properties of the `Ini` type. Both return the same F# `option` and `Result`
types. In C# these are `FSharpOption<T>` and `FSharpResult<T, TError>` from `Microsoft.FSharp.Core`, and you can
match on them with any C# pattern matching: `switch` expressions, `switch` statements and `is`. Every example below is shown in both languages.

## Lookups return an option

A missing key is not an error. `tryFind` / `TryFind` and `tryFindNested` / `TryFindNested` return
`Some value` if the key exists and `None` if it doesn't.

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

C#: an option is a class, and `None` is `null`. Match `Some` with a property pattern on `Value`, and `None`
with `null`. A `null` never matches a property pattern, so these two arms cover every case and no `_` arm is
needed.

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

Reading `Value` on a `None` throws `NullReferenceException`, and the `null` arm is what prevents that. The
`IsSome` and `IsNone` members can't be called from C#, because F# compiles them as static helpers. With `var`
you don't need `using Microsoft.FSharp.Core`; write out `FSharpOption<string>` only when you need the type name.

## Building and parsing return a result

Functions that can reject a key or a line return `Ok ini` or `Error err`. These are `fromLines`, `fromFile`,
`appendLines`, `appendFile`, `append`, `add` and `ofSeq`.

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

// chaining — each step runs only if the previous one succeeded, so you get the first error
let built =
    match Ini.empty |> Ini.add "timeout" "30" with
    | Ok ini -> ini |> Ini.add "server:host" "localhost"
    | Error err -> Error err

let built' =
    match built with
    | Ok ini -> ini |> Ini.add "server:port" "8080"
    | Error err -> Error err
```

`Result.bind` does the same chaining with less code:
`Ini.empty |> Ini.add "timeout" "30" |> Result.bind (Ini.add "server:host" "localhost")`.

C#: a result is a struct, so it is never `null`, and it has no case classes to match on. Instead it has both
a `ResultValue` and an `ErrorValue` property, and the one that isn't set is `default`. On success the document
is never `null`, so `{ ResultValue: { } ini }` matches `Ok`, and `{ ErrorValue: var err }` after it matches
`Error`.

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

// chaining — each step runs only if the previous one succeeded, so you get the first error
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

This doesn't work when the result holds a value type, as with the typed lookups below. On an error,
`ResultValue` is then `0` or `false`, not `null`, so the `Ok` arm also checks `IsOk: true`.

## Errors

Problems with the content are returned as errors, not thrown:

```fsharp
type ParserError =
    | InvalidKey of Key: string
    | InvalidLine of Line: string
    | InvalidValue of Value: string // net10.0 only
```

Each case holds the exact text that caused the problem. `add`, `append` and `ofSeq` return `InvalidKey`, the four
parsing functions return `InvalidLine`, and the typed lookups return `InvalidValue`. A `null` argument or a
file system problem throws an exception.

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

C#: each case is a nested type, and its text is a property named after the field. Match the type and the
property together:

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

The C# compiler can't tell that the switch covers every `ParserError` case, so the `_` arm is needed. Errors
are compared by value — `ParserError.NewInvalidLine("oops")` is equal to any other `InvalidLine "oops"` — and
print as `InvalidLine "oops"`.

`InvalidValue` and the typed lookups exist only in the `net10.0` build. On `net48`, `ParserError` has only two
cases, so code that mentions `InvalidValue` won't compile there.

## Typed lookup

`tryFindValue` / `TryFindValue` and `tryFindNestedValue` / `TryFindNestedValue` convert the value to any type
that implements `IParsable<T>`: the primitive types, `Guid`, `DateTime`, `TimeSpan`, and your own types.
Parsing uses `CultureInfo.InvariantCulture`. They return an option that contains a result, so the two kinds of
failure stay separate: `None` means the key is missing, and `Some (Error (InvalidValue value))` means the value
couldn't be parsed.

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

C#: the option's `Value` is the result, so the patterns are nested. `int` and `TimeSpan` are value types, so
the `Ok` arm checks `IsOk: true` instead of checking that `ResultValue` isn't `null`.

```csharp
int port = ini.TryFindValue<int>("server:port") switch
{
    { Value: { IsOk: true, ResultValue: var p } } => p,
    { Value.ErrorValue: var err } => throw new FormatException(Describe(err)),
    null => 8080
};

TimeSpan timeout = ini.TryFindNestedValue<TimeSpan>("server.dev:timeout") switch
{
    { Value: { IsOk: true, ResultValue: var t } } => t,
    { Value.ErrorValue: ParserError.InvalidValue { Value: var text } } => throw new FormatException($"not a TimeSpan: {text}"),
    { Value.ErrorValue: var err } => throw new FormatException(Describe(err)),
    null => TimeSpan.FromSeconds(30)
};
```

`IParsable<T>` doesn't accept `NumberStyles`, so each type uses its own default rules. For example, `float` /
`double` allow a thousands separator and read `"1,234"` as `1234.0`, but `int` doesn't and returns
`InvalidValue "1,234"`.

## Keys

A key has the form `section:parameter`. Neither part may contain whitespace or any of `=`, `:`, `;`, `#`,
`[`, `]`. The parameter part can't be empty. The section part can be empty, which means the root section, so
`x` and `:x` are the same key. Keys are case-insensitive (`OrdinalIgnoreCase`); values are case-sensitive.

| Key               |                                   |
|-------------------|-----------------------------------|
| `server:host`     | valid                             |
| `host`, `:host`   | valid, the root section           |
| `server.dev:port` | valid — a dot is part of the name |
| `a b`             | `InvalidKey` — whitespace         |
| `server:`         | `InvalidKey` — empty parameter    |
| `a:b:c`           | `InvalidKey` — two colons         |

Keys are never trimmed or corrected. An invalid key is simply not found: `TryFind` returns `None`,
`ContainsKey` returns `false`, and `Remove` does nothing. Root keys are always returned with the colon, so
`keys` and enumeration give `:timeout`, not `timeout`.

## File format

| Line           | Meaning                            |
|----------------|------------------------------------|
| `[name]`       | opens a section                    |
| `name = value` | a parameter in the current section |
| `; text`       | comment                            |
| `# text`       | comment                            |
| blank          | ignored                            |
| anything else  | `InvalidLine`                      |

The value is everything after the first `=`, with whitespace trimmed from both ends. It can contain `=`, `:`,
`;` and `#`, and it can be empty. If the same key appears twice, the later value wins, and a section can be
opened again further down the file. Lines are read one at a time, and reading stops at the first invalid
line — if line 3 of a 10,000-line file is invalid, only three lines are read.

Sections don't nest in the file: `[server.dev]` is just a section named `server.dev`. The dots matter only to
the nested lookups, which look in `server.dev`, then `server`, then the root.

## Layering

`appendLines` / `appendFile` read new parameters on top of an existing document. If both have the same key,
the new value wins; all other existing parameters are kept. Each new file or list of lines starts in the root
section, no matter which section the previous one ended in. So the load order decides which value wins, but
never changes which key a line is stored under.

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

`append` / `Append` layers key/value pairs instead of lines. The keys have the form `section:parameter`, the same
as for `add`, and every pair is checked. If a key is invalid, you get `Error (InvalidKey key)` and no pairs are
added. Pairs are read one at a time, so reading stops at the first invalid key. If a key appears twice, the later
value wins. `append` accepts the same pairs that `toSeq` returns and that enumerating a document gives, so
you can merge one document into another.

F#:

```fsharp
let config' =
    Ini.fromFile "defaults.ini"
    |> Result.bind (Ini.append [ "server:port", portFromArgs; "timeout", "10" ])

// merge one document into another: values of `overrides` win
let merged = defaults |> Ini.append (Ini.toSeq overrides)
```

C#:

```csharp
var config = Ini.FromFile("defaults.ini");
config = config switch
{
    { ResultValue: { } ini } => ini.Append([KeyValuePair.Create("server:port", portFromArgs), KeyValuePair.Create("timeout", "10")]),
    var error => error
};

// merge one document into another: values of `overrides` win
var merged = defaults.Append(overrides);
```

## Sections

`section` returns one section as a new document, and `removeSection` returns everything except that section.
`sections` lists the section names, with `""` for the root. A dotted section like `server.dev` is separate
from `server`, so removing `server` keeps `server.dev`. To remove both, remove each one by name, or loop over
the names as shown below.

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

In F#, `Ini.toSeq` returns `(key, value)` tuples and `Ini.ofSeq` builds a document from them. In C#, the
document is itself an `IEnumerable<KeyValuePair<string, string>>`, so you can pass it straight to `foreach`,
LINQ and `Ini.Create(ini)`. The same pairs are also available as `ini.KeyValuePairs`, which is handy when you
need the sequence itself rather than the document.

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

`toLines` / `ToLines` write the document in a standard layout. Root parameters come first, without a header.
Then each section follows as a `[section]` block, with a blank line between blocks. Sections are sorted
case-insensitively, with a parent before its children (`server` before `server.dev`), and parameters are
sorted within each section. An empty value is written as `x =`. `toString` / `ToString` joins the lines with
`Environment.NewLine`, and `toFile` / `ToFile` writes them to a file.

The output doesn't reproduce the original file: comments, blank lines, the original order and duplicate keys
are lost. All values are kept, so writing a document and reading it back gives an equal document.

## API

| `Ini` member                    | `Ini` function                       | Result                                            |
|---------------------------------|--------------------------------------|---------------------------------------------------|
| `Ini.Empty`                     | `Ini.empty`                          | `Ini`                                             |
| `Ini.Create pairs`              | `Ini.ofSeq pairs`                    | `Result<Ini, ParserError>`                        |
| `Ini.FromLines lines`           | `Ini.fromLines lines`                | `Result<Ini, ParserError>`                        |
| `Ini.FromFile path`             | `Ini.fromFile path`                  | `Result<Ini, ParserError>`                        |
| `ini.Append pairs`              | `Ini.append pairs ini`               | `Result<Ini, ParserError>`                        |
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
| `ini.KeyValuePairs`             | `Ini.toSeq ini`                      | `seq<KeyValuePair<string, string>>`               |
| `ini.Sections`                  | `Ini.sections ini`                   | `seq<string>`                                     |
| `ini.Section name`              | `Ini.section name ini`               | `Ini`                                             |
| `ini.RemoveSection name`        | `Ini.removeSection name ini`         | `Ini`                                             |
| `ini.ToLines()`                 | `Ini.toLines ini`                    | `seq<string>`                                     |
| `ini.ToFile path`               | `Ini.toFile path ini`                | `unit`                                            |
| `ini.ToString()`                | `Ini.toString ini`                   | `string`                                          |

From C#, `string option` is `FSharpOption<string>` and `Result<Ini, ParserError>` is
`FSharpResult<Ini, ParserError>`.

`Ini` implements `IEnumerable<KeyValuePair<string, string>>`, `IEquatable<Ini>` and `IComparable<Ini>`.
Documents are compared by their keys first and then by their values, and equal documents have the same hash
code. Letter case in names, the order of entries, comments and blank lines don't affect the comparison.

## Build

```shell
dotnet build
dotnet test
```

## License

[MIT](https://github.com/glokhov/fini/blob/main/LICENSE)
