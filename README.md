# Fini

[![NuGet](https://img.shields.io/nuget/vpre/Fini.svg)](https://www.nuget.org/packages/Fini)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/glokhov/fini/blob/main/LICENSE)

A simple, **immutable** INI parser for .NET, written in F#.

Fini flattens an INI document into a sorted, case-insensitive map of `section:parameter` keys. Sections may be
dotted (`[server.dev]`), and `tryFindNested` walks that hierarchy upwards so child sections inherit values from
their parents — useful for layered configuration.

## Install

```shell
dotnet add package Fini --prerelease
```

Targets `net10.0`.

## Quick start

```fsharp
open Fini

let config =
    [ "; application settings"
      "timeout = 30"
      ""
      "[server]"
      "host = localhost"
      "port = 8080"
      ""
      "[server.dev]"
      "port = 5000" ]

match Ini.fromLines config with
| Error err -> eprintfn $"%s{err}"
| Ok ini ->
    Ini.tryFind "server:host" ini         // Some "localhost"
    Ini.tryFind "server:port" ini         // Some "8080"
    Ini.tryFind "timeout" ini             // Some "30"

    // inherited from [server] and from the root section
    Ini.tryFindNested "server.dev:port" ini     // Some "5000"
    Ini.tryFindNested "server.dev:host" ini     // Some "localhost"
    Ini.tryFindNested "server.dev:timeout" ini  // Some "30"
```

`fromLines` parses a sequence of lines, so it is agnostic about where the text came from. `fromFile` reads a
file and parses it in one step:

```fsharp
open System.IO

let a = File.ReadLines "app.ini" |> Ini.fromLines
let b = Ini.fromFile "app.ini"           // the same, and reports IO errors as Error
```

## Keys

Every parameter is addressed by a single key of the form `section:parameter`:

| INI                        | Key               |
|----------------------------|-------------------|
| `x = 1` (before a section) | `:x`              |
| `[alpha]` + `x = 1`        | `alpha:x`         |
| `[alpha.beta]` + `x = 1`   | `alpha.beta:x`    |

A key without a `:` is taken to mean the root section, so `"x"` and `":x"` are equivalent.

Keys are compared ordinal case-insensitively — `alpha:x`, `ALPHA:X` and `Alpha:X` all refer to the same
parameter. Values keep their original case. `keys`, `values` and `toSeq` are returned in key order, aligned
with each other.

Key order compares the section first and the parameter second, with the root section ahead of every named
one. So all root parameters come first, each section is one contiguous run, and a parent section precedes
its children — `:timeout`, `server:host`, `server:port`, `server.dev:port`.

Two `Ini` values compare equal when they hold the same keys with the same values. Name case, declaration
order, comments and blank lines are not part of the comparison; values and their case are.

## Hierarchical lookup

`tryFind` is an exact lookup. `tryFindNested` starts at the requested section and walks up the dotted
hierarchy until the parameter is found, ending at the root section:

```
server.dev.eu:port  ->  server.dev:port  ->  server:port  ->  :port
```

The most specific match wins, and siblings and children are never searched. Given the config above,
`tryFindNested "server.dev:port"` returns `Some "5000"` (its own) while `tryFindNested "server.dev:host"`
returns `Some "localhost"` (inherited from `[server]`).

## API

```fsharp
// construction
Ini.empty         : Ini
Ini.fromLines     : string seq -> Result<Ini, string>
Ini.fromFile      : string -> Result<Ini, string>
Ini.ofSeq         : KeyValuePair<string, string> seq -> Result<Ini, string>

// appending
Ini.appendLines   : string seq -> Ini -> Result<Ini, string>
Ini.appendFile    : string -> Ini -> Result<Ini, string>

// inspection
Ini.isEmpty       : Ini -> bool
Ini.count         : Ini -> int
Ini.keys          : Ini -> string seq
Ini.values        : Ini -> string seq
Ini.toSeq         : Ini -> KeyValuePair<string, string> seq
Ini.containsKey   : string -> Ini -> bool

// rendering
Ini.toLines       : Ini -> string seq
Ini.toString      : Ini -> string
Ini.toFile        : string -> Ini -> Result<unit, string>

// lookup
Ini.tryFind       : string -> Ini -> string option
Ini.tryFindNested : string -> Ini -> string option
Ini.find          : string -> Ini -> string
Ini.findNested    : string -> Ini -> string

// mutation
Ini.add           : string -> string -> Ini -> Result<Ini, string>
Ini.remove        : string -> Ini -> Ini
```

The `Ini` is always the last parameter, so lookups compose with `|>`:

```fsharp
ini |> Ini.containsKey "server:port"            // true
ini |> Ini.tryFindNested "server.dev:timeout"   // Some "30"
```

### Naming

The `try` prefix marks a function that returns an `option`, and nothing else. That is `tryFind` and
`tryFindNested`: a missing key is not a failure, it is an answer, and the only thing to report is its
absence.

`find` and `findNested` are their partial counterparts, by design: same lookup, no `option`, and a
`KeyNotFoundException` when the key is not there. Use them where a missing key is a bug rather than a case
to handle, and `tryFind` / `tryFindNested` everywhere else.

Everything that can genuinely fail says so in its return type instead, as `Result<_, string>` — `fromLines`,
`fromFile`, `ofSeq`, `appendLines`, `appendFile`, `add` and `toFile`. Everything else — `isEmpty`,
`count`, `keys`, `values`, `toSeq`, `toLines`, `toString`, `containsKey`, `remove` — cannot fail and returns
its value directly.

## Adding and removing

`add` overwrites an existing key, matching the parse semantics where a duplicate key wins. `remove` is a
no-op when the key is absent. Both return a new `Ini` and leave the original untouched:

```fsharp
Ini.empty
|> Ini.add "server:host" "localhost"
|> Result.bind (Ini.add "server:port" "8080")
|> Result.map (Ini.remove "server:host")
|> Result.map Ini.count     // Ok 1
```

`add` returns a `Result` because a key written in code is not constrained the way a parsed one is. It is
checked against the same charset the parser accepts, so that everything an `Ini` holds can be written out
and read back:

```fsharp
Ini.add "a b" "1" Ini.empty          // Error "Invalid key: a b."
Ini.add "alpha:x:y" "1" Ini.empty    // Error "Invalid key: alpha:x:y."
Ini.add "alpha:" "1" Ini.empty       // Error "Invalid key: alpha:."
```

Keys are never trimmed — not by `add`, not by lookup — so `" x "` is rejected rather than stored under a
key that the same string could not find again.

Only `add`, `ofSeq` and the parser put keys into the map, so only they validate. `remove` and the lookups
take the key as given and need no check: a key the parser would reject cannot be in the map, so `tryFind`
returns `None`, `containsKey` returns `false`, and `remove` has nothing to delete.

```fsharp
ini |> Ini.containsKey "a b"    // false
ini |> Ini.remove "a b"         // unchanged
```

## Pairs

`toSeq` returns the contents as `KeyValuePair<string, string>` in key order, and `ofSeq` builds an `Ini`
from the same shape. The two are inverses:

```fsharp
open System.Collections.Generic

let ini = Ini.fromLines [ "timeout = 30"; "[server]"; "port = 8080" ]

ini |> Result.map (Ini.toSeq >> Seq.toList)
// Ok [[:timeout, 30]; [server:port, 8080]]

ini |> Result.bind (Ini.toSeq >> Ini.ofSeq)
// Ok {the same Ini}
```

The pair type is the same on both sides of the language boundary, so `toSeq` feeds `ofSeq` directly and an
`Ini` can be piped into `ofSeq` as it stands — it is itself a `KeyValuePair` sequence. In F# the pairs have
to be constructed, which is the cost of the shared shape:

```fsharp
Ini.ofSeq [ KeyValuePair("x", "1"); KeyValuePair("a b", "2") ]
// Error "Invalid key: a b."
```

`ofSeq` is an insertion point, so it validates every key exactly as `add` does and reports the first one it
rejects. A later pair overwrites an earlier one with the same key, and a key without a `:` lands in the root
section:

```fsharp
Ini.ofSeq [ KeyValuePair("alpha:x", "1"); KeyValuePair("ALPHA:X", "2") ] |> Result.map Ini.count
// Ok 1

Ini.ofSeq [ KeyValuePair("x", "1") ] |> Result.map Ini.keys
// Ok (seq [":x"])
```

## Appending

An `Ini` is immutable. `appendLines` returns a new instance, merging additional lines over the existing
ones; later parameters overwrite earlier ones with the same key. Each call starts at the root section, so an
appended `port = 9090` lands at `:port` unless the appended lines open a section of their own.

```fsharp
let ini = Ini.fromLines [ "[server]"; "port = 8080" ]

ini |> Result.bind (Ini.appendLines [ "[server]"; "port = 9090" ])
    |> Result.map (Ini.tryFind "server:port")   // Ok (Some "9090")
```

`appendFile` is the same for a file on disk, and layers naturally — a shipped default, then a machine
override, then a user one:

```fsharp
Ini.fromFile "defaults.ini"
|> Result.bind (Ini.appendFile "/etc/app.ini")
|> Result.bind (Ini.appendFile "~/.app.ini")
```

## Rendering

`toLines` is the inverse of `fromLines`: it renders an `Ini` back to a sequence of lines. `toString` joins
them with `\n`, and `toFile` writes them with `File.WriteAllLines`.

```fsharp
let ini = Ini.fromLines [ "; the request timeout"; "timeout = 30"; "[server.dev]"; "port = 5000"
                          "[server]"; "host = localhost"; "port = 8080" ]

ini |> Result.map Ini.toLines
// Ok (seq [ "timeout = 30"; ""; "[server]"; "host = localhost"; "port = 8080"; ""
//           "[server.dev]"; "port = 5000" ])
```

The layout is canonical: root-section parameters first with no header, then one `[name]` header per section
followed by its parameters, everything in key order, with a blank line before each header. An empty value is
written as `x =`, and an empty `Ini` renders to no lines at all.

Key order does the work: the root section sorts ahead of every named one, so its parameters are rendered
before any header — `:` is `0x3A`, and a section name starting with a digit or `-` would otherwise sort
first, leaving a root parameter after a header and read back as part of that section.

Rendering is **canonical, not faithful**. An `Ini` keeps neither comments, blank lines, declaration order,
repeated-section layout nor value padding, so none of those come back:

```fsharp
Ini.fromLines [ "; a comment"; ""; "[beta]"; "y   =   2"; "[alpha]"; "x = 1"; "[beta]"; "z = 3" ]
|> Result.map (Ini.toLines >> List.ofSeq)
// Ok [ "[alpha]"; "x = 1"; ""; "[beta]"; "y = 2"; "z = 3" ]
```

What does come back is the content: `fromLines (toLines ini)` equals `ini` for every document the parser
accepts. The exception is a value that only `add` or `ofSeq` could have introduced, since those do not
constrain the value — surrounding whitespace is trimmed on the way back in, and a value containing a newline
renders as two lines, the second of which does not parse:

```fsharp
Ini.add "x" " padded " Ini.empty |> Result.map Ini.toString   // Ok "x =  padded "  -> reads back as "padded"
```

This is the other half of what [escaping](#inline-comments) is for, and it is the one case where a
round trip can lose or break.

## Files

`fromFile` and `appendFile` read the file with `File.ReadLines`, so a document is streamed rather than held
in memory twice, and parsing stops at the first line it cannot read. The file is closed either way,
including when parsing stops early. `toFile` writes with `File.WriteAllLines`, so the file ends with a line
terminator and an existing file is overwritten:

```fsharp
Ini.fromFile "defaults.ini"
|> Result.bind (Ini.appendFile "~/.app.ini")
|> Result.bind (Ini.toFile "merged.ini")     // Ok ()
```

All three report an IO failure as `Error` rather than raising it, with the message of the underlying
exception:

```fsharp
Ini.fromFile "no-such-file.ini"
// Error "Could not find file 'C:\...\no-such-file.ini'."

Ini.fromFile ""
// Error "The value cannot be an empty string. (Parameter 'path')"
```

A `null` path is the one case they do raise on: it is checked up front, like every other `null` argument.

## C#

The same surface is exposed as members, with `TryFind` and `TryFindNested` following the usual
`bool` + `out` pattern:

```csharp
using Fini;

var result = Ini.FromFile("app.ini");

if (result.IsError)
{
    Console.Error.WriteLine(result.ErrorValue);
    return;
}

var ini = result.ResultValue;

if (ini.TryFind("server:host", out var host))
{
    Console.WriteLine(host);
}

// walks up: server.dev -> server -> root
if (ini.TryFindNested("server.dev:timeout", out var timeout))
{
    Console.WriteLine(timeout);
}

Console.WriteLine(ini.Count);
Console.WriteLine(ini.ContainsKey("SERVER:PORT"));  // True
```

| Member                                         | Returns                               |
|------------------------------------------------|---------------------------------------|
| `Ini.Empty`                                    | `Ini`                                 |
| `Ini.FromLines(IEnumerable<string>)`           | `Result<Ini, string>`                 |
| `Ini.FromFile(string)`                         | `Result<Ini, string>`                 |
| `Ini.OfSeq(IEnumerable<KeyValuePair<string, string>>)` | `Result<Ini, string>`           |
| `ini.AppendLines(IEnumerable<string>)`         | `Result<Ini, string>`                 |
| `ini.AppendFile(string)`                       | `Result<Ini, string>`                 |
| `ini.IsEmpty`                                  | `bool`                                |
| `ini.Count`                                    | `int`                                 |
| `ini.Keys`, `ini.Values`                       | `IEnumerable<string>`                 |
| `ini.KeyValuePairs`                            | `IEnumerable<KeyValuePair<string, string>>`  |
| `ini.ContainsKey(string)`                      | `bool`                                |
| `ini.TryFind(string, out string)`              | `bool`                                |
| `ini.TryFindNested(string, out string)`        | `bool`                                |
| `ini.Find(string)`, `ini.FindNested(string)`   | `string`                              |
| `ini.Add(string, string)`                      | `Result<Ini, string>`                 |
| `ini.Remove(string)`                           | `Ini`                                 |
| `ini.ToLines()`                                | `IEnumerable<string>`                 |
| `ini.ToString()`                               | `string`                              |
| `ini.ToFile(string)`                           | `Result<Unit, string>`                |

`Ini` implements `IEnumerable<KeyValuePair<string, string>>`, so it can be iterated or queried with LINQ.
Pairs come out in key order, the same order as `Keys` and `Values`. `KeyValuePair` deconstructs, so a
`foreach` can name both halves while LINQ sees `Key` and `Value`:

```csharp
foreach (var (key, value) in ini)
{
    Console.WriteLine($"{key} = {value}");
}

var ports = ini.Where(pair => pair.Key.EndsWith(":port")).Select(pair => pair.Value);
```

`Ini.OfSeq` takes the same shape straight back, so enumeration needs no projection — and an `Ini` can be
passed as it stands, since it is itself a `KeyValuePair` sequence:

```csharp
var copy = Ini.OfSeq(ini.KeyValuePairs);
var same = Ini.OfSeq(ini);
```

## Syntax

| Line                | Meaning                                                           |
|---------------------|-------------------------------------------------------------------|
| empty or whitespace | ignored                                                           |
| `; text`, `# text`  | comment, ignored                                                  |
| `[name]`            | opens a section; surrounding whitespace is trimmed                |
| `name = value`      | a parameter in the current section; both sides are trimmed        |

Details:

- A parameter name cannot contain `=`, `:`, `;`, `#`, `[`, `]` or whitespace. The same applies to section
  names. `:` is reserved as the key separator, so every key holds exactly one of them and always splits
  unambiguously into a section and a parameter. A value may still contain `:` freely —
  `url = https://example.com:8080` is fine.
- Only the first `=` separates the name from the value, so `x = a=b` gives `x` the value `a=b`.
- A value may be empty: `x =` yields `Some ""`.
- Comments are recognised on their own line only — see [Inline comments](#inline-comments) below.
- A section may be reopened later in the document; its parameters are merged.
- A duplicate key overwrites the earlier one.

### Inline comments

Inline comments are **not supported**. A `;` or `#` is only a comment marker at the start of a line; anywhere
inside a value it is an ordinary character. Everything after the first `=`, once trimmed, is the value:

```fsharp
Ini.fromLines [ "x = 1 ; note" ]
|> Result.map (Ini.tryFind "x")   // Ok (Some "1 ; note")  — not Some "1"
```

This is deliberate. Stripping `;` and `#` from values would make those characters impossible to write, and
they occur in real configuration values:

```ini
separators = ;#
fragment   = https://example.com/doc#section
```

Supporting inline comments therefore requires an escape mechanism first, so that a value can opt out of
comment handling. Escaping is planned for a future release; until then, keep comments on their own line:

```ini
; the request timeout, in seconds
timeout = 30
```

## Errors

Parsing is total — nothing throws for malformed input. The first line that cannot be parsed is reported:

```fsharp
Ini.fromLines [ "not a valid line" ]
// Error "Cannot parse line: not a valid line."
```

`add` and `ofSeq` report the key they rejected:

```fsharp
Ini.add "a b" "1" Ini.empty
// Error "Invalid key: a b."
```

`fromFile` and `appendFile` report an unreadable file the same way, as `Error`, and `toFile` an unwritable
one. Those are the expected failures, and they are all in the return type. What raises instead:

- `find` and `findNested` raise `KeyNotFoundException` when the key is not present. That is their purpose:
  they are the partial counterparts of `tryFind` and `tryFindNested`, for call sites where a missing key is
  a bug.
- Passing `null` where a path, a sequence of lines, a sequence of pairs, or a key is expected throws
  `ArgumentNullException`. An empty path does not — it reaches `File.ReadLines` and comes back as
  `Error`.

## License

[MIT](https://github.com/glokhov/fini/blob/main/LICENSE) © Gennadiy Lokhov
