# Fini

[![NuGet](https://img.shields.io/nuget/vpre/Fini.svg)](https://www.nuget.org/packages/Fini)
[![Build](https://github.com/glokhov/fini/actions/workflows/build.yml/badge.svg)](https://github.com/glokhov/fini/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/glokhov/fini/blob/main/LICENSE)

A simple INI parser written in F#, usable from F# and C# alike.

Documents are **immutable**: any operation that changes a document returns a new one instead. Two documents are
equal if they have the same keys and values. The structure is flat: each value is stored under one `Key`, made of
a section name and a parameter name and written `section:parameter`.

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
    | Error err -> failwith $"%A{err}"                         // InvalidLine "oops"

ini |> Ini.tryFind (Key.parse "server:host")                   // Some "localhost"
ini |> Ini.tryFind (Key.parse "server.dev:host")               // None — exact lookup
ini |> Ini.tryFind (Key.parse "timeout")                       // Some "30" — no colon means the root section

ini |> Ini.tryFindNested (Key.parse "server.dev:port")         // Some "5000"      — from the section itself
ini |> Ini.tryFindNested (Key.parse "server.dev:host")         // Some "localhost" — inherited from [server]
ini |> Ini.tryFindNested (Key.parse "server.dev:timeout")      // Some "30"        — inherited from the root

ini |> Ini.find (Key.parse "server:host")                      // "localhost" — throws if missing
ini |> Ini.tryFindAs<int> (Key.parse "server:port")            // Some (Ok 8080) — net10.0 only
ini |> Ini.tryFindWith int (Key.parse "server:port")           // Some (Ok 8080)
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
string? iTimeout = ini.TryFindNested("server.dev:timeout")?.Value; // "30"        — inherited from the root

string sHost = ini.Find("server:host");                            // "localhost" — throws if missing
int port = ini.FindAs<int>("server:port");                         // 8080 — net10.0 only
int port2 = ini.FindWith(int.Parse, "server:port");                // 8080
```

F# code uses the functions in the `Ini`, `Key` and `Name` modules. They take the document as the last argument, so
they work with `|>`. C# code uses the methods and properties of the `Ini`, `Key` and `Name` types. Both return the
same F# `option` and `Result` types. In C# these are `FSharpOption<T>` and `FSharpResult<T, TError>` from
`Microsoft.FSharp.Core`, and you can match on them with any C# pattern matching: `switch` expressions, `switch`
statements and `is`. Every example below is shown in both languages.

## Keys and names

A `Name` is a section or parameter name. It may not be empty or contain whitespace or any of `=`, `:`, `;`, `#`,
`[`, `]`. The one exception is `Name.empty`, the empty name of the root section.

A `Key` is a section name and a parameter name, written `section:parameter`. The parameter name can't be empty. The
section name can be, which means the root section, so `x` and `:x` are the same key. Names and keys are
case-insensitive (`OrdinalIgnoreCase`) but keep their spelling; values are case-sensitive.

| Key               |                                   |
|-------------------|-----------------------------------|
| `server:host`     | valid                             |
| `host`, `:host`   | valid, the root section           |
| `server.dev:port` | valid — a dot is part of the name |
| `a b`             | `InvalidName "a b"` — whitespace  |
| `server:`         | `InvalidName ""` — empty name     |
| `a:b:c`           | `InvalidName "a:b:c"` — two colons |

Names and keys are never trimmed or corrected: `" a"` and `"a\n"` are rejected, not turned into `a`. Each type
has a checked and a throwing way to build it:

| F#                       | C#                       | Invalid input                  |
|--------------------------|--------------------------|--------------------------------|
| `Name.tryGet "server"`   | `Name.TryGet("server")`  | `Error (InvalidName name)`     |
| `Name.get "server"`      | `Name.Get("server")`     | throws `ArgumentException`     |
| `Key.tryParse "s:x"`     | `Key.TryParse("s:x")`    | `Error (InvalidName name)`     |
| `Key.parse "s:x"`        | `Key.Parse("s:x")`       | throws `ArgumentException`     |
| `Key.tryGet "s" "x"`     | `Key.TryGet("s", "x")`   | `Error (InvalidName name)`     |
| `Key.get "s" "x"`        | `Key.Get("s", "x")`      | throws `ArgumentException`     |

Both types convert implicitly from and to `string`. The conversion from a string uses `Parse` / `Get`, so an
invalid string throws `ArgumentException`. Use `TryParse` / `TryGet` for input you don't control.

F#: the conversion applies to method arguments, so `ini.TryFind "server:host"` works. Module functions take a
`Key`, so pass `Key.parse "server:host"`. `string key` gives the text back.

```fsharp
let key =
    match Key.tryParse userInput with
    | Ok key -> key
    | Error (InvalidName name) -> failwith $"bad name: {name}"
    | Error err -> failwith $"%A{err}"

let port = ini |> Ini.tryFind key
```

C#: every method that takes a `Key` or a `Name` also accepts a string.

```csharp
Key key = Key.TryParse(userInput) switch
{
    { ResultValue: { } k } => k,
    { ErrorValue: ParserError.InvalidName { Name: var name } } => throw new ArgumentException($"bad name: {name}"),
    { ErrorValue: var err } => throw new ArgumentException(err.ToString())
};

string? port = ini.TryFind(key)?.Value;
string? host = ini.TryFind("server:host")?.Value; // implicit conversion
```

A `Key` exposes its two parts as `Section` and `Parameter`, both `Name` values. A key without a section, like
`host`, has `Section` equal to `Name.empty`.

```fsharp
let k = Key.parse "server:host"
k.Section          // Name.get "server"
k.Parameter        // Name.get "host"
Key.section k      // Name.get "server" — equivalent module function
Key.parameter k    // Name.get "host"
```

```csharp
Key k = "server:host";
Name section = k.Section;     // "server"
Name parameter = k.Parameter; // "host"
```

Root keys are always written with the colon, so `keys` and enumeration give `:timeout`, not `timeout`.

## Lookups return an option

A missing key is not an error. `tryFind` / `TryFind` and `tryFindNested` / `TryFindNested` return `Some value` if
the key exists and `None` if it doesn't.

F#:

```fsharp
let host =
    match ini |> Ini.tryFind (Key.parse "server:host") with
    | Some host -> host
    | None -> "0.0.0.0"

let timeout =
    match ini |> Ini.tryFind (Key.parse "timeout") with
    | Some "0" -> "no timeout"
    | Some t -> $"timeout: {t}"
    | None -> "default timeout"
```

C#: an option is a class, and `None` is `null`. Match `Some` with a property pattern on `Value`, and `None` with
`null`. A `null` never matches a property pattern, so these arms cover every case and no `_` arm is needed.

```csharp
string host = ini.TryFind("server:host") switch
{
    { Value: var h } => h,
    null => "0.0.0.0"
};

string timeout = ini.TryFind("timeout") switch
{
    { Value: "0" } => "no timeout",
    { Value: var t } => $"timeout: {t}",
    null => "default timeout"
};
```

Reading `Value` on a `None` throws `NullReferenceException`, and the `null` arm is what prevents that. The `IsSome`
and `IsNone` members can't be called from C#, because F# compiles them as static helpers.

If a missing key is a bug rather than an expected case, `find` / `Find` and `findNested` / `FindNested` return the
value directly and throw `KeyNotFoundException` when it's missing.

## Nested lookup

Sections don't nest in the file: `[server.dev]` is just a section named `server.dev`. The dots matter only to the
nested lookups, which look in `server.dev`, then `server`, then the root, and return the first value found. A
missing section in between is skipped, so `server.a.b:host` also finds `server:host`. Nested lookups never look
into child sections.

## Parsing returns a result

The four functions that read lines can reject a line, so they return `Ok ini` or `Error (InvalidLine line)`. These
are `fromLines`, `fromFile`, `appendLines` and `appendFile`.

F#:

```fsharp
let config =
    match Ini.fromFile "app.ini" with
    | Ok ini -> ini
    | Error _ -> Ini.empty

// chaining — each step runs only if the previous one succeeded, so you get the first error
let layered =
    Ini.fromFile "defaults.ini"
    |> Result.bind (Ini.appendFile "app.ini")
```

C#: a result is a struct, so it is never `null`, and it has no case classes to match on. Instead it has both a
`ResultValue` and an `ErrorValue` property, and the one that isn't set is `default`. On success the document is
never `null`, so `{ ResultValue: { } ini }` matches `Ok`, and `{ ErrorValue: var err }` after it matches `Error`.

```csharp
Ini config = Ini.FromFile("app.ini") switch
{
    { ResultValue: { } ini } => ini,
    { ErrorValue: _ } => Ini.Empty
};

// chaining — each step runs only if the previous one succeeded, so you get the first error
var layered = Ini.FromFile("defaults.ini");
layered = layered switch
{
    { ResultValue: { } ini } => ini.AppendFile("app.ini"),
    var error => error
};
```

This doesn't work when the result holds a value type, as with the typed lookups below. On an error, `ResultValue`
is then `0` or `false`, not `null`, so the `Ok` arm also checks `IsOk: true`.

## Building

`add`, `append`, `ofSeq` and `remove` take keys that are already valid, so they can't fail and return the new
document directly.

F#:

```fsharp
let ini =
    Ini.empty
    |> Ini.add (Key.parse "timeout") "30"
    |> Ini.add (Key.parse "server:host") "localhost"
    |> Ini.remove (Key.parse "timeout")

let other = Ini.ofSeq [ Key.parse "server:port", "8080" ]
```

C#:

```csharp
Ini ini = Ini.Empty
    .Add("timeout", "30")
    .Add("server:host", "localhost")
    .Remove("timeout");

Ini other = Ini.Create([new KeyValuePair<Key, string>("server:port", "8080")]);
```

`add` replaces an existing value, and the key takes the spelling of the latest `add`. Values are stored exactly as
given; only values read from lines are trimmed. `remove` of a missing key does nothing.

## Errors

Problems with the content are returned as errors, not thrown:

```fsharp
type ParserError =
    | InvalidName of Name: string
    | InvalidLine of Line: string
    | InvalidValue of Value: string
```

Each case holds the exact text that caused the problem. `Name.tryGet`, `Key.tryParse` and `Key.tryGet` return
`InvalidName`, the four parsing functions return `InvalidLine`, and the `try…As` and `try…With` lookups return
`InvalidValue`. A `null` argument, a file system problem or a throwing function (`get`, `parse`, `find…`, implicit
conversions) throws an exception.

F#:

```fsharp
let describe err =
    match err with
    | InvalidName name -> $"bad name: {name}"
    | InvalidLine line -> $"bad line: {line}"
    | InvalidValue value -> $"bad value: {value}"
```

C#: each case is a nested type, and its text is a property named after the field. Match the type and the property
together:

```csharp
static string Describe(ParserError err) => err switch
{
    ParserError.InvalidName { Name: var name } => $"bad name: {name}",
    ParserError.InvalidLine { Line: var line } => $"bad line: {line}",
    ParserError.InvalidValue { Value: var value } => $"bad value: {value}",
    _ => "unreachable"
};

ParserError expected = ParserError.NewInvalidLine("oops");
```

The C# compiler can't tell that the switch covers every `ParserError` case, so the `_` arm is needed. Errors are
compared by value — `ParserError.NewInvalidLine("oops")` is equal to any other `InvalidLine "oops"` — and print as
`InvalidLine "oops"`.

## Typed lookup

There are two ways to convert a value:

- `…As<'T>` uses `IParsable<'T>`: the primitive types, `Guid`, `DateTime`, `TimeSpan`, and your own types. Parsing
  uses `CultureInfo.InvariantCulture`. These exist only in the `net10.0` build.
- `…With` takes a parser function, `string -> 'T` in F# and `Func<string, T>` in C#. Any exception it throws counts
  as a parse failure. These exist in both builds.

The `try` forms return an option that contains a result, so the two kinds of failure stay separate: `None` means
the key is missing, and `Some (Error (InvalidValue value))` means the value couldn't be parsed. The parser isn't
called for a missing key. The forms without `try` return the value and throw: `KeyNotFoundException` for a missing
key, and the parse exception (`FormatException` for `…As`, whatever the parser throws for `…With`) for a bad value.

|          | `IParsable`        | parser function      |
|----------|--------------------|----------------------|
| exact    | `tryFindAs`        | `tryFindWith`        |
|          | `findAs`           | `findWith`           |
| nested   | `tryFindNestedAs`  | `tryFindNestedWith`  |
|          | `findNestedAs`     | `findNestedWith`     |

F#:

```fsharp
let port =
    match ini |> Ini.tryFindAs<int> (Key.parse "server:port") with
    | Some (Ok port) -> port
    | Some (Error err) -> failwith (describe err)
    | None -> 8080

let timeout = ini |> Ini.findNestedAs<TimeSpan> (Key.parse "server.dev:timeout")

let debug =
    match ini |> Ini.tryFindWith (fun s -> s = "yes") (Key.parse "server:debug") with
    | Some (Ok debug) -> debug
    | _ -> false
```

C#: the option's `Value` is the result, so the patterns are nested. `int` is a value type, so the `Ok` arm checks
`IsOk: true` instead of checking that `ResultValue` isn't `null`.

```csharp
int port = ini.TryFindAs<int>("server:port") switch
{
    { Value: { IsOk: true, ResultValue: var p } } => p,
    { Value.ErrorValue: var err } => throw new FormatException(Describe(err)),
    null => 8080
};

TimeSpan timeout = ini.FindNestedAs<TimeSpan>("server.dev:timeout");

bool debug = ini.TryFindWith(s => s == "yes", "server:debug") is { Value: { IsOk: true, ResultValue: true } };
```

`IParsable<T>` doesn't accept `NumberStyles`, so each type uses its own default rules. For example, `float` /
`double` allow a thousands separator and read `"1,234"` as `1234.0`, but `int` doesn't and returns
`InvalidValue "1,234"`. Use a `…With` parser when you need other rules.

## File format

| Line           | Meaning                            |
|----------------|------------------------------------|
| `[name]`       | opens a section                    |
| `name = value` | a parameter in the current section |
| `; text`       | comment                            |
| `# text`       | comment                            |
| blank          | ignored                            |
| anything else  | `InvalidLine`                      |

Names follow the rules above, with whitespace allowed around them. The value is everything after the first `=`,
with whitespace trimmed from both ends. It can contain `=`, `:`, `;`, `#` and `[`, and it can be empty. If the
same key appears twice, the later value wins, and a section can be opened again further down the file. Lines are
read one at a time, and reading stops at the first invalid line — if line 3 of a 10,000-line file is invalid, only
three lines are read.

## Layering

`appendLines` / `appendFile` read new parameters on top of an existing document. If both have the same key, the
new value wins; all other existing parameters are kept. Each new file or list of lines starts in the root section,
no matter which section the previous one ended in. So the load order decides which value wins, but never changes
which key a line is stored under.

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

`append` / `Append` layers key/value pairs instead of lines. If a key appears twice, the later value wins. It
accepts the same pairs that `toSeq` returns and that enumerating a document gives, so you can merge one document
into another.

F#:

```fsharp
// values of `overrides` win
let merged = defaults |> Ini.append (Ini.toSeq overrides)
```

C#:

```csharp
// values of `overrides` win
Ini merged = defaults.Append(overrides);
```

## Sections

`section` returns one section as a new document, and `removeSection` returns everything except that section.
`sections` lists the section names in order, with `Name.empty` (printed as `""`) first if there are root
parameters. A dotted section like `server.dev` is separate from `server`, so removing `server` keeps `server.dev`.
To remove both, remove each one by name.

F#:

```fsharp
ini |> Ini.section (Name.get "server") |> Ini.toLines // seq [ "[server]"; "host = localhost"; "port = 8080" ]

let withoutServer =
    ini
    |> Ini.sections
    |> Seq.filter (fun name -> name = Name.get "server" || (string name).StartsWith "server.")
    |> Seq.fold (fun doc name -> Ini.removeSection name doc) ini
```

C#:

```csharp
ini.Section("server").ToLines(); // [ "[server]", "host = localhost", "port = 8080" ]

Ini withoutServer = ini.Sections
    .Where(name => name.Equals((Name)"server") || name.ToString().StartsWith("server."))
    .Aggregate(ini, (doc, name) => doc.RemoveSection(name));
```

## Enumeration and rendering

In F#, `Ini.toSeq` returns `(Key * string)` tuples in key order and `Ini.ofSeq` builds a document from them. In C#,
the document is itself an `IEnumerable<KeyValuePair<Key, string>>`, so you can pass it straight to `foreach`, LINQ
and `Ini.Create`. The same pairs are also available as `ini.KeyValuePairs`.

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

`toLines` / `ToLines` write the document in a standard layout. Root parameters come first, without a header. Then
each section follows as a `[section]` block, with a blank line between blocks. Sections are sorted
case-insensitively, so a parent comes before its children (`server` before `server.dev`), and parameters are
sorted within each section. An empty value is written as `x =`. `toString` / `ToString` joins the lines with
`Environment.NewLine`, and `toFile` / `ToFile` writes them to a file.

The output doesn't reproduce the original file: comments, blank lines, the original order and duplicate keys are
lost. All values are kept, so writing a document and reading it back gives an equal document.

## Equality and comparison

`Ini` implements `IEquatable<Ini>`, `IComparable<Ini>` and `IComparable`, so documents work with `=`, `compare`,
`Set` and `Map` in F#, and with `Equals`, `HashSet`, `Dictionary` and `Order()` in C#. Documents are compared by
their keys first and then by their values, and equal documents have the same hash code. Letter case in names, the
order of entries, comments and blank lines don't affect the comparison. `Key` and `Name` are equatable and
comparable in the same way.

In C#, `==` on `Ini`, `Key` or `Name` compares references, not values. Use `Equals`.

## API

| `Ini` member                            | `Ini` function                               | Result                                   |
|-----------------------------------------|----------------------------------------------|------------------------------------------|
| `Ini.Empty`                             | `Ini.empty`                                  | `Ini`                                    |
| `Ini.Create pairs`                      | `Ini.ofSeq pairs`                            | `Ini`                                    |
| `Ini.FromLines lines`                   | `Ini.fromLines lines`                        | `Result<Ini, ParserError>`               |
| `Ini.FromFile path`                     | `Ini.fromFile path`                          | `Result<Ini, ParserError>`               |
| `ini.Append pairs`                      | `Ini.append pairs ini`                       | `Ini`                                    |
| `ini.AppendLines lines`                 | `Ini.appendLines lines ini`                  | `Result<Ini, ParserError>`               |
| `ini.AppendFile path`                   | `Ini.appendFile path ini`                    | `Result<Ini, ParserError>`               |
| `ini.Add(key, value)`                   | `Ini.add key value ini`                      | `Ini`                                    |
| `ini.Remove key`                        | `Ini.remove key ini`                         | `Ini`                                    |
| `ini.TryFind key`                       | `Ini.tryFind key ini`                        | `string option`                          |
| `ini.TryFindNested key`                 | `Ini.tryFindNested key ini`                  | `string option`                          |
| `ini.Find key`                          | `Ini.find key ini`                           | `string`                                 |
| `ini.FindNested key`                    | `Ini.findNested key ini`                     | `string`                                 |
| `ini.TryFindAs<T> key`                  | `Ini.tryFindAs<'T> key ini`                  | `Result<'T, ParserError> option` — `net10.0` only |
| `ini.TryFindNestedAs<T> key`            | `Ini.tryFindNestedAs<'T> key ini`            | `Result<'T, ParserError> option` — `net10.0` only |
| `ini.FindAs<T> key`                     | `Ini.findAs<'T> key ini`                     | `'T` — `net10.0` only                    |
| `ini.FindNestedAs<T> key`               | `Ini.findNestedAs<'T> key ini`               | `'T` — `net10.0` only                    |
| `ini.TryFindWith(parser, key)`          | `Ini.tryFindWith parser key ini`             | `Result<'T, ParserError> option`         |
| `ini.TryFindNestedWith(parser, key)`    | `Ini.tryFindNestedWith parser key ini`       | `Result<'T, ParserError> option`         |
| `ini.FindWith(parser, key)`             | `Ini.findWith parser key ini`                | `'T`                                     |
| `ini.FindNestedWith(parser, key)`       | `Ini.findNestedWith parser key ini`          | `'T`                                     |
| `ini.ContainsKey key`                   | `Ini.containsKey key ini`                    | `bool`                                   |
| `ini.IsEmpty`                           | `Ini.isEmpty ini`                            | `bool`                                   |
| `ini.Count`                             | `Ini.count ini`                              | `int`                                    |
| `ini.Keys`                              | `Ini.keys ini`                               | `seq<Key>`                               |
| `ini.Values`                            | `Ini.values ini`                             | `seq<string>`                            |
| `foreach` / LINQ over `ini`             | `Ini.toSeq ini`                              | pairs, in key order                      |
| `ini.KeyValuePairs`                     | `Ini.toSeq ini`                              | `seq<KeyValuePair<Key, string>>`         |
| `ini.Sections`                          | `Ini.sections ini`                           | `seq<Name>`                              |
| `ini.Section name`                      | `Ini.section name ini`                       | `Ini`                                    |
| `ini.RemoveSection name`                | `Ini.removeSection name ini`                 | `Ini`                                    |
| `ini.ToLines()`                         | `Ini.toLines ini`                            | `seq<string>`                            |
| `ini.ToFile path`                       | `Ini.toFile path ini`                        | `unit`                                   |
| `ini.ToString()`                        | `Ini.toString ini`                           | `string`                                 |

| `Key` / `Name` member        | Function                  | Result                     |
|------------------------------|---------------------------|----------------------------|
| `Key.TryParse key`           | `Key.tryParse key`        | `Result<Key, ParserError>` |
| `Key.Parse key`              | `Key.parse key`           | `Key`                      |
| `Key.TryGet(section, name)`  | `Key.tryGet section name` | `Result<Key, ParserError>` |
| `Key.Get(section, name)`     | `Key.get section name`    | `Key`                      |
| `key.Section`                | `Key.section key`         | `Name`                     |
| `key.Parameter`              | `Key.parameter key`       | `Name`                     |
| `Name.Empty`                 | `Name.empty`              | `Name`                     |
| `Name.TryGet name`           | `Name.tryGet name`        | `Result<Name, ParserError>`|
| `Name.Get name`              | `Name.get name`           | `Name`                     |

From C#, `string option` is `FSharpOption<string>` and `Result<Ini, ParserError>` is
`FSharpResult<Ini, ParserError>`.

## Build

```shell
dotnet build
dotnet test
```

## License

[MIT](https://github.com/glokhov/fini/blob/main/LICENSE)
