# Fini

[![NuGet](https://img.shields.io/nuget/vpre/Fini.svg)](https://www.nuget.org/packages/Fini)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/glokhov/fini/blob/main/LICENSE)

A simple, **immutable** INI parser for .NET, written in F#.

An `Ini` is a value: every operation that changes a document returns a new one, the original is untouched,
and two documents can be compared with `=`. Parsing and key validation report failure as
`Result<Ini, string>` rather than by raising. The API is usable from both F# and C#.

## Install

```shell
dotnet add package Fini --prerelease
```

## Quick start

F#:

```fsharp
open Fini

let ini =
    match Ini.fromLines [ "timeout = 30"
                          "[server]"
                          "host = localhost"
                          "[server.dev]"
                          "port = 5000" ] with
    | Ok ini -> ini
    | Error err -> failwith err

let host = ini |> Ini.tryFind "server:host"             // Some "localhost"
let inherited = ini |> Ini.tryFindNested "server.dev:host"  // Some "localhost"
let port = ini |> Ini.find "server.dev:port"            // "5000"
let timeout = ini |> Ini.find "timeout"                 // "30"

// Ok of a new document; `ini` still has its three entries
let updated = ini |> Ini.add "server:port" "8080"
```

C#:

```csharp
using Fini;

var result = Ini.FromLines([
    "timeout = 30",
    "[server]",
    "host = localhost",
    "[server.dev]",
    "port = 5000",
]);

if (result.IsError)
{
    Console.Error.WriteLine(result.ErrorValue);
    return;
}

var ini = result.ResultValue;

ini.TryFind("server:host", out var host);                   // true, "localhost"
ini.TryFindNested("server.dev:host", out var inherited);    // true, "localhost"
var port = ini.Find("server.dev:port");                     // "5000"

var updated = ini.Add("server:port", "8080").ResultValue;

foreach (var (key, value) in updated)
{
    Console.WriteLine($"{key} = {value}");
}
```

Files round-trip:

```fsharp
Ini.fromFile "settings.ini"
|> Result.map (Ini.add "server:port" "8080")
```

## The document model

A document is **flattened**: every parameter is one entry keyed `section:parameter`. There is no nested
object to walk, so lookup, enumeration and rendering all work on the same flat sequence of pairs, in key
order.

```ini
timeout = 30

[server]
host = localhost
```

is two entries, `:timeout` and `server:host`.

## File format

| Line | Meaning |
| --- | --- |
| `[name]` | opens a section; the following parameters belong to it |
| `name = value` | a parameter in the current section |
| `; text` or `# text` | comment, ignored |
| blank or whitespace | ignored |
| anything else | `Error "Cannot parse line: <line>."` |

**Names** — a section name and a parameter name may use any characters except whitespace and
`= : ; # [ ]`, and must be non-empty. So `[]`, `[a b]`, `a b = 1` and `[alpha:beta]` are all errors. The
colon is reserved as the key separator, which is what makes every key split unambiguously into exactly two
parts.

**Values** — everything after the **first** `=`, with surrounding whitespace trimmed. A value may contain
`=`, `:`, `;` and `#`, and may be empty:

| Line | Value |
| --- | --- |
| `x = a=b=c` | `a=b=c` |
| `url = https://example.com:8080/a:b` | `https://example.com:8080/a:b` |
| `fragment = https://example.com/doc#rfc` | `https://example.com/doc#rfc` |
| `empty =` | the empty string |

**Whitespace** is trimmed around a header, around a name and around a value, but kept inside a value:
`x =\ta\tb\t` yields `"a\tb"`.

**Parameters before the first header** belong to the root section, keyed `:name`.

**Duplicates** — a later parameter overwrites an earlier one with the same key, and a section may be
reopened later in the file.

Parsing is a single lazy pass that stops at the first unparsable line: a 10,000-line source with a bad
line at position 3 reads three lines, and `fromFile` closes the file handle on the way out.

## Keys

A key is `section:parameter`. A key with no colon refers to the root section, so `x` and `:x` are the same
key.

Keys are compared **case-insensitively** (`OrdinalIgnoreCase`), both halves; values are kept and compared
as written.

```fsharp
ini |> Ini.tryFind "ALPHA:X"   // finds alpha:x
```

Keys are never trimmed or repaired. A key the parser would reject is therefore simply absent rather than
an error: `tryFind` gives `None`, `containsKey` gives `false`, `remove` is a no-op, and `find` raises
`KeyNotFoundException`. On the way *in* — `add` and `ofSeq` — the same key is an `Error "Invalid key: …"`,
which keeps the invariant that anything `toLines` writes, `fromLines` can read back.

Two `Ini` values are equal when they hold the same keys and the same values. Section and parameter name
case, declaration order, comments and blank lines are not taken into account; values and their case are.
Equal documents have equal hash codes.

## Nested sections

A dotted section name is just a name — the parser stores `[server.dev]` verbatim. The hierarchy is
interpreted on lookup: `tryFindNested` / `findNested` try the given section first, then each ancestor in
turn, then the root.

```ini
timeout = 30

[server]
host = localhost
port = 8080

[server.dev]
port = 5000
```

```fsharp
ini |> Ini.tryFindNested "server.dev:port"      // Some "5000"      — the section itself
ini |> Ini.tryFindNested "server.dev:host"      // Some "localhost" — inherited from [server]
ini |> Ini.tryFindNested "server.dev:timeout"   // Some "30"        — inherited from the root
ini |> Ini.tryFindNested "server.dev:nope"      // None
```

`tryFind` never walks the hierarchy; it is an exact-key lookup.

## Appending

`appendLines` and `appendFile` layer a document over an existing one — a shipped default, then a machine
override, then a user one. Later layers win on a shared key, and keys only present in earlier layers
survive.

```fsharp
Ini.fromFile "defaults.ini"
|> Result.bind (Ini.appendFile "machine.ini")
|> Result.bind (Ini.appendFile "user.ini")
```

Each call **starts at the root section**, by design: a layer's lines are parsed by exactly the rules
`fromLines` uses, so a file's keys depend on that file alone. Load order decides which value wins, never
which key a line writes to, and `Ini.empty |> Ini.appendLines lines` is `Ini.fromLines lines`. An appended
layer opens its own sections when it means to write into them — appending `port = 9090` lands at `:port`
regardless of the sections already present in the document it is layered onto.

## Rendering

`toLines`, `toString` and `toFile` emit a canonical document: root parameters first with no header, then
one `[section]` block per section separated by a blank line.

```fsharp
Ini.fromLines [ "; comment"; "root = 0"; "[beta]"; "y = 2"; "[alpha]"; "x = 1" ]
|> Result.map Ini.toLines
// [ "root = 0"; ""; "[alpha]"; "x = 1"; ""; "[beta]"; "y = 2" ]
```

Order is the key order used everywhere else: the root section first, then sections `OrdinalIgnoreCase`
with a parent ahead of its children, then parameters within a section. An empty value renders as `x =`.
`toString` joins the lines with `\n`; `toFile` writes them with `File.WriteAllLines`.

The output is normalised, not a faithful copy of the input: comments, blank lines, original ordering and
duplicate entries are not preserved. Values are, so `toFile` followed by `fromFile` gives back an equal
document.

## Error handling

Three kinds of failure, kept apart:

- **Content** — an unparsable line or an invalid key. Returned as `Error` of
  `Result<Ini, string>`: `fromLines`, `fromFile`, `ofSeq`, `appendLines`, `appendFile`, `add`.
- **A missing key** — an answer, not a failure. `tryFind` / `tryFindNested` return an `option`
  (`TryFind` / `TryFindNested` with an `out` parameter in C#). `find` / `findNested` are their partial
  counterparts and raise `KeyNotFoundException`.
- **A bad argument or a filesystem problem** — raised. A `null` key, value, line, sequence, pair member or
  path raises `ArgumentNullException` naming the parameter; the I/O exceptions of `File.ReadLines` and
  `File.WriteAllLines` propagate unchanged (`FileNotFoundException`, `DirectoryNotFoundException`, …).

Only the `try` functions return an `option`, and only content failures return a `Result` — the signature
of `add` or `fromLines` already says the call may not produce an `Ini`.

## API

The `Ini` is always the last parameter of the F# functions, so they compose with `|>`.

| F# | C# | |
| --- | --- | --- |
| `Ini.empty` | `Ini.Empty` | the empty document |
| `Ini.fromLines : string seq -> Result<Ini, string>` | `Ini.FromLines` | parse lines |
| `Ini.fromFile : string -> Result<Ini, string>` | `Ini.FromFile` | parse a file |
| `Ini.ofSeq : KeyValuePair<string, string> seq -> Result<Ini, string>` | `Ini.OfSeq` | build from pairs |
| `Ini.appendLines : string seq -> Ini -> Result<Ini, string>` | `AppendLines` | layer lines over a document |
| `Ini.appendFile : string -> Ini -> Result<Ini, string>` | `AppendFile` | layer a file over a document |
| `Ini.add : string -> string -> Ini -> Result<Ini, string>` | `Add` | add or overwrite one key |
| `Ini.remove : string -> Ini -> Ini` | `Remove` | remove one key; no-op when absent |
| `Ini.tryFind : string -> Ini -> string option` | `TryFind(key, out value)` | exact lookup |
| `Ini.tryFindNested : string -> Ini -> string option` | `TryFindNested(key, out value)` | lookup walking up the hierarchy |
| `Ini.find : string -> Ini -> string` | `Find` | exact lookup, raises |
| `Ini.findNested : string -> Ini -> string` | `FindNested` | nested lookup, raises |
| `Ini.containsKey : string -> Ini -> bool` | `ContainsKey` | |
| `Ini.isEmpty : Ini -> bool` | `IsEmpty` | |
| `Ini.count : Ini -> int` | `Count` | number of parameters |
| `Ini.keys : Ini -> string seq` | `Keys` | keys in key order |
| `Ini.values : Ini -> string seq` | `Values` | values, aligned with `keys` |
| `Ini.toSeq : Ini -> KeyValuePair<string, string> seq` | `KeyValuePairs` | pairs in key order |
| `Ini.sections : Ini -> string seq` | `Sections` | distinct section names, `""` for the root |
| `Ini.section : string -> Ini -> Ini` | `Section` | one section as a standalone document |
| `Ini.toLines : Ini -> string seq` | `ToLines()` | render |
| `Ini.toString : Ini -> string` | `ToString()` | render, joined with `\n` |
| `Ini.toFile : string -> Ini -> unit` | `ToFile` | render to a file |

`Ini` implements `IEnumerable<KeyValuePair<string, string>>`, so C# can `foreach` and LINQ over it
directly, and `Ini.OfSeq(ini)` round-trips a document.

`Section` matches the name case-insensitively, excludes dotted children, keeps the keys as they are in the
original document, and renders back to a valid standalone document:

```fsharp
ini |> Ini.section "alpha" |> Ini.toLines   // [ "[alpha]"; "x = 1" ]
```

## Not supported yet

- **Escaping.** There is no escape character, so a value cannot carry a newline, or leading or trailing
  whitespace.
- **Inline comments.** `x = 1 ; note` is the value `1 ; note`. Stripping `;` and `#` from values without
  an escape mechanism would make those characters impossible to write, and they occur in real values
  (`separators = ;#`, `fragment = https://example.com/doc#section`), so inline comments wait on escaping.
- **Comment preservation.** Comments and blank lines are read and discarded, not carried through a
  round-trip.
- **Line numbers in parse errors.** The message names the line's text, not its position.

## Build

```shell
dotnet build
dotnet test
```

## License

[MIT](https://github.com/glokhov/fini/blob/main/LICENSE)
