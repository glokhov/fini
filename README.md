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

match Ini.tryCreate config with
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

Fini parses a sequence of lines, so it is agnostic about where the text came from:

```fsharp
open System.IO

let ini = File.ReadLines "app.ini" |> Ini.tryCreate
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
parameter. Values keep their original case. `keys` and `values` are returned in key order, aligned with each
other.

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
Ini.empty         : Ini
Ini.isEmpty       : Ini -> bool
Ini.count         : Ini -> int
Ini.keys          : Ini -> string seq
Ini.values        : Ini -> string seq
Ini.tryCreate     : string seq -> Result<Ini, string>
Ini.tryAppend     : string seq -> Ini -> Result<Ini, string>
Ini.containsKey   : string -> Ini -> bool
Ini.tryFind       : string -> Ini -> string option
Ini.tryFindNested : string -> Ini -> string option
```

The `Ini` is always the last parameter, so lookups compose with `|>`:

```fsharp
ini |> Ini.containsKey "server:port"            // true
ini |> Ini.tryFindNested "server.dev:timeout"   // Some "30"
```

An `Ini` is immutable. `tryAppend` returns a new instance, merging additional lines over the existing ones;
later parameters overwrite earlier ones with the same key. Each call starts at the root section, so an
appended `port = 9090` lands at `:port` unless the appended lines open a section of their own.

```fsharp
let ini = Ini.tryCreate [ "[server]"; "port = 8080" ]

ini |> Result.bind (Ini.tryAppend [ "[server]"; "port = 9090" ])
    |> Result.map (Ini.tryFind "server:port")   // Ok (Some "9090")
```

## C#

The same surface is exposed as members, with `TryFind` and `TryFindNested` following the usual
`bool` + `out` pattern:

```csharp
using Fini;

var result = Ini.TryCreate(File.ReadLines("app.ini"));

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

| Member                                       | Returns            |
|----------------------------------------------|--------------------|
| `Ini.Empty`                                  | `Ini`              |
| `Ini.TryCreate(IEnumerable<string>)`         | `Result<Ini, string>` |
| `ini.TryAppend(IEnumerable<string>)`         | `Result<Ini, string>` |
| `ini.IsEmpty`                                | `bool`             |
| `ini.Count`                                  | `int`              |
| `ini.Keys`, `ini.Values`                     | `IEnumerable<string>` |
| `ini.ContainsKey(string)`                    | `bool`             |
| `ini.TryFind(string, out string)`            | `bool`             |
| `ini.TryFindNested(string, out string)`      | `bool`             |

## Syntax

| Line                | Meaning                                                           |
|---------------------|-------------------------------------------------------------------|
| empty or whitespace | ignored                                                           |
| `; text`, `# text`  | comment, ignored                                                  |
| `[name]`            | opens a section; surrounding whitespace is trimmed                |
| `name = value`      | a parameter in the current section; both sides are trimmed        |

Details:

- A parameter name cannot contain `=`, `;`, `#`, `[`, `]` or whitespace. The same applies to section names.
- Only the first `=` separates the name from the value, so `x = a=b` gives `x` the value `a=b`.
- A value may be empty: `x =` yields `Some ""`.
- Comments are recognised on their own line only — see [Inline comments](#inline-comments) below.
- A section may be reopened later in the document; its parameters are merged.
- A duplicate key overwrites the earlier one.

### Inline comments

Inline comments are **not supported**. A `;` or `#` is only a comment marker at the start of a line; anywhere
inside a value it is an ordinary character. Everything after the first `=`, once trimmed, is the value:

```fsharp
Ini.tryCreate [ "x = 1 ; note" ]
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
Ini.tryCreate [ "not a valid line" ]
// Error "Cannot parse line: not a valid line."
```

Passing `null` where a sequence of lines or a key is expected throws `ArgumentNullException`.

## License

[MIT](https://github.com/glokhov/fini/blob/main/LICENSE) © Gennadiy Lokhov
