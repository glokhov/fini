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
let b = Ini.fromFile "app.ini"           // the same thing, written once
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

## Sections

`sections` lists every distinct section name present in the `Ini`, including the root section (`""`) when
it has parameters of its own. `section` returns the parameters of one named section as a standalone `Ini`,
keyed exactly as in the original — a key keeps its section prefix, so a lookup against the result still
needs it:

```fsharp
let ini =
    Ini.fromLines [ "root = 0"; "[server]"; "host = localhost"; "[server.dev]"; "port = 5000" ]
    |> Result.defaultWith failwith

Ini.sections ini   // seq [""; "server"; "server.dev"]

let server = Ini.section "server" ini
Ini.keys server              // seq ["server:host"]
Ini.tryFind "server:host" server   // Some "localhost"
Ini.tryFind "host" server          // None — the prefix is kept, not stripped
```

`section` matches the name case-insensitively, the same as every other lookup, and merges every differently
cased reopening of that section into one result. A dotted child section is a section in its own right and is
never included in its parent's — `section "server"` does not pull in `server.dev`, and `sections` lists both
as separate entries. A name with no parameters, including one that does not exist at all, returns something
equal to `Ini.empty` rather than raising.

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
Ini.sections      : Ini -> string seq
Ini.section       : string -> Ini -> Ini

// rendering
Ini.toLines       : Ini -> string seq
Ini.toString      : Ini -> string
Ini.toFile        : string -> Ini -> unit

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

Everything that can fail on its *content* says so in its return type instead, as `Result<_, string>` —
`fromLines`, `fromFile`, `ofSeq`, `appendLines`, `appendFile` and `add`. There are exactly two such
failures, an unparsable line and an invalid key, and they are the two a caller is expected to handle.

Nothing else is in the return type. A filesystem failure raises, so `toFile` returns `unit`, and `fromFile`
and `appendFile` carry a `Result` about the document rather than about the file. A malformed argument
raises too. See [Errors](#errors).

Everything else — `isEmpty`, `count`, `keys`, `values`, `toSeq`, `toLines`, `toString`, `containsKey`,
`remove`, `sections`, `section` — returns its value directly: given an argument at all, there is nothing to
report.

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
Ini.add "" "1" Ini.empty             // Error "Invalid key: ."
```

Keys are never trimmed — not by `add`, not by lookup — so `" x "` is rejected rather than stored under a
key that the same string could not find again.

A `null` argument is not an `Error` but an `ArgumentNullException`. `add` requires a key *and* a value that
are not `null`, so the two failures do not mix: an `Error` means the key is not one the parser could read
back, an exception means the call itself is broken.

```fsharp
Ini.add null "1" Ini.empty           // raises ArgumentNullException
Ini.add "x" null Ini.empty           // raises ArgumentNullException
```

A value is otherwise unconstrained: `add "x" ""` succeeds, just as `x =` does when parsed, so `add` and
the parser agree on what a value may be. See [Errors](#errors).

Only `add`, `ofSeq` and the parser put keys into the map, so only they check a key against the charset.
`remove` and the lookups take the key as given: a key the parser would reject cannot be in the map, so
`tryFind` returns `None`, `containsKey` returns `false`, and `remove` has nothing to delete — including an
empty key, which is not a special case here, only an unused one.

```fsharp
ini |> Ini.containsKey "a b"    // false
ini |> Ini.remove "a b"         // unchanged
ini |> Ini.containsKey ""       // false
```

They do still reject a `null` key, which is a broken call rather than a miss:

```fsharp
ini |> Ini.containsKey null     // raises ArgumentNullException
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

`ofSeq` is an insertion point, so it checks every key against the same charset `add` does and reports the
first one it rejects. It is not guarded the way `add` is, though: the keys arrive inside a sequence rather
than as arguments, so a blank one comes back as `Error "Invalid key: ."` instead of raising. A later pair
overwrites an earlier one with the same key, and a key without a `:` lands in the root section:

```fsharp
Ini.ofSeq [ KeyValuePair("alpha:x", "1"); KeyValuePair("ALPHA:X", "2") ] |> Result.map Ini.count
// Ok 1

Ini.ofSeq [ KeyValuePair("x", "1") ] |> Result.map Ini.keys
// Ok (seq [":x"])
```

A `null` key or a `null` value inside a pair still raises `ArgumentNullException`, same as `add`:

```fsharp
Ini.ofSeq [ KeyValuePair(null, "1") ]   // raises ArgumentNullException
Ini.ofSeq [ KeyValuePair("x", null) ]   // raises ArgumentNullException
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

Starting each call at the root section is what makes that layering work. Every layer is parsed by exactly
the rules `fromLines` uses, so its keys depend on the layer alone: load order decides which value wins,
never which key a line writes to, and `appendLines lines Ini.empty` is `fromLines lines`. Appending is
layering, not concatenation — if a call continued from the last section of the document it was applied to,
adding a trailing `[logging]` section to `defaults.ini` would silently move every root parameter of
`~/.app.ini` into it.

`appendLines` guards the sequence itself and every line inside it: a `null` sequence or a `null` element
anywhere in it raises `ArgumentNullException`, exactly as `fromLines` does, since `fromLines` is
`appendLines` starting from `Ini.empty`.

```fsharp
Ini.appendLines null ini                // raises ArgumentNullException
Ini.appendLines [ "x = 1"; null ] ini   // raises ArgumentNullException
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
accepts. The exception is a value that only `add` or `ofSeq` could have introduced — `add` constrains a
value only by rejecting a blank one, and `ofSeq` not at all, so surrounding whitespace is trimmed on the
way back in, and a value containing a newline renders as two lines, the second of which does not parse:

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
|> Result.map (Ini.toFile "merged.ini")      // Ok ()
```

The `Result` those three carry is about the document, not about the file. `fromFile` and `appendFile`
return `Error` for a line they cannot parse; `toFile` cannot fail that way at all, so it returns `unit`.
Everything the filesystem can refuse raises, exactly as `File.ReadLines` and `File.WriteAllLines` raise it:

```fsharp
Ini.fromFile "no-such-file.ini"        // raises FileNotFoundException
Ini.toFile "no-such-dir/app.ini" ini   // raises DirectoryNotFoundException
Ini.fromFile ""                        // raises ArgumentException
Ini.fromFile null                      // raises ArgumentNullException
```

So an unreadable file is not an `Error` to match on, it is an exception to handle or to prevent. A caller
who wants it alongside the parse errors is one wrapper away:

```fsharp
let tryFromFile path =
    try Ini.fromFile path with ex -> Error ex.Message
```

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
| `ini.Sections`                                 | `IEnumerable<string>`                 |
| `ini.Section(string)`                          | `Ini`                                 |
| `ini.TryFind(string, out string)`              | `bool`                                |
| `ini.TryFindNested(string, out string)`        | `bool`                                |
| `ini.Find(string)`, `ini.FindNested(string)`   | `string`                              |
| `ini.Add(string, string)`                      | `Result<Ini, string>`                 |
| `ini.Remove(string)`                           | `Ini`                                 |
| `ini.ToLines()`                                | `IEnumerable<string>`                 |
| `ini.ToString()`                               | `string`                              |
| `ini.ToFile(string)`                           | `void`                                |

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

Exactly two failures are in the return type, and both are about content. Parsing is total — nothing throws
for malformed input — and the first line that cannot be parsed is reported:

```fsharp
Ini.fromLines [ "not a valid line" ]
// Error "Cannot parse line: not a valid line."
```

`add` and `ofSeq` report the key they rejected:

```fsharp
Ini.add "a b" "1" Ini.empty
// Error "Invalid key: a b."
```

Everything else raises:

- **A missing, unreadable or unwritable file, or an empty path.** `fromFile` and `appendFile` let the
  exception out of `File.ReadLines`, `toFile` out of `File.WriteAllLines`, unchanged — including
  `ArgumentException` for `""`, which is `File.ReadLines`'s own guard, not Fini's. See [Files](#files).
- **A missing key, from `find` and `findNested`** — `KeyNotFoundException`. That is their purpose:
  they are the partial counterparts of `tryFind` and `tryFindNested`, for call sites where a missing key is
  a bug. A blank key behaves the same way: it cannot be in the map, so it is simply not found.
- **A `null` argument** — `ArgumentNullException`. A path, a sequence of lines, every line inside it, a
  sequence of pairs, the key and value inside each pair, a key, and the value given to `add` are all
  guarded.

```fsharp
Ini.add null "1" ini         // raises ArgumentNullException
Ini.add "x" null ini         // raises ArgumentNullException
Ini.tryFind null ini         // raises ArgumentNullException
Ini.fromFile null            // raises ArgumentNullException
```

A blank key or value is not a broken call: `add "" "1"` and `ofSeq [ KeyValuePair("", "1") ]` both report
`Error "Invalid key: ."`, since an empty string is not a key the parser could have produced, and a blank
value is simply stored — `add "x" ""` succeeds, just as `x =` does when parsed. A blank key given to a
lookup or to `remove` is never in the map either, so it is a miss, not a failure:

```fsharp
Ini.add "" "1" ini         // Error "Invalid key: ."
Ini.add "x" "" ini          // Ok (stores "" verbatim)
Ini.tryFind "" ini          // None
Ini.containsKey "" ini      // false
Ini.remove "" ini           // unchanged
```

## License

[MIT](https://github.com/glokhov/fini/blob/main/LICENSE) © Gennadiy Lokhov
