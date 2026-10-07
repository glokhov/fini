module Fini.Tests.IniTests

open System
open System.Collections.Generic
open Fini
open Xunit

// ---------------------------------------------------------------- helpers

let private create lines =
    match Ini.tryCreate lines with
    | Ok ini -> ini
    | Error err -> failwith $"expected Ok, got Error: %s{err}"

let private error lines =
    match Ini.tryCreate lines with
    | Ok _ -> failwith "expected Error, got Ok"
    | Error err -> err

let private keys ini = Ini.keys ini |> List.ofSeq

let private values ini = Ini.values ini |> List.ofSeq

// ---------------------------------------------------------------- empty

[<Fact>]
let ``empty is empty`` () = Assert.True(Ini.isEmpty Ini.empty)

[<Fact>]
let ``empty has no entries`` () =
    Assert.Equal(0, Ini.count Ini.empty)
    Assert.Empty(keys Ini.empty)
    Assert.Empty(values Ini.empty)

[<Fact>]
let ``empty finds nothing`` () =
    Assert.Equal<string option>(None, Ini.tryFind "a:x" Ini.empty)
    Assert.Equal<string option>(None, Ini.tryFindNested "a:x" Ini.empty)
    Assert.False(Ini.containsKey "a:x" Ini.empty)

[<Fact>]
let ``no lines creates an empty ini`` () =
    let ini = create []
    Assert.True(Ini.isEmpty ini)

[<Fact>]
let ``an ini with entries is not empty`` () =
    let ini = create [ "x = 1" ]
    Assert.False(Ini.isEmpty ini)
    Assert.Equal(1, Ini.count ini)

// ---------------------------------------------------------------- parsing: ignored lines

[<Theory>]
[<InlineData("")>]
[<InlineData(" ")>]
[<InlineData("\t")>]
[<InlineData("   \t  ")>]
let ``whitespace lines are ignored`` line =
    let ini = create [ line ]
    Assert.True(Ini.isEmpty ini)

[<Theory>]
[<InlineData("; a comment")>]
[<InlineData("# a comment")>]
[<InlineData("  ;  indented comment  ")>]
[<InlineData(";")>]
[<InlineData("#")>]
[<InlineData("; x = 1")>]
let ``comment lines are ignored`` line =
    let ini = create [ line ]
    Assert.True(Ini.isEmpty ini)

[<Fact>]
let ``comments and blank lines are skipped between parameters`` () =
    let ini =
        create
            [ "; leading comment"
              ""
              "root = 0"
              "   "
              "[alpha]"
              "# hash comment"
              "x = 1" ]

    Assert.Equal<string list>([ ":root"; "alpha:x" ], keys ini)

// ---------------------------------------------------------------- parsing: parameters

[<Fact>]
let ``a parameter outside any section is keyed at the root`` () =
    let ini = create [ "x = 1" ]
    Assert.Equal<string list>([ ":x" ], keys ini)
    Assert.Equal<string option>(Some "1", Ini.tryFind ":x" ini)

[<Fact>]
let ``a parameter inside a section is keyed by section and name`` () =
    let ini = create [ "[alpha]"; "x = 1" ]
    Assert.Equal<string list>([ "alpha:x" ], keys ini)

[<Fact>]
let ``a dotted section name is preserved`` () =
    let ini = create [ "[alpha.beta]"; "x = 1" ]
    Assert.Equal<string list>([ "alpha.beta:x" ], keys ini)

[<Fact>]
let ``surrounding whitespace is trimmed from sections names and values`` () =
    let ini = create [ "[  alpha  ]"; "   x   =   one two   " ]
    Assert.Equal<string list>([ "alpha:x" ], keys ini)
    Assert.Equal<string list>([ "one two" ], values ini)

[<Fact>]
let ``a parameter may have an empty value`` () =
    let ini = create [ "x =" ]
    Assert.Equal<string option>(Some "", Ini.tryFind "x" ini)

// ---------------------------------------------------------------- whitespace around names

// regression: the name charset excluded a literal space but not a tab, so a tab
// after a name was captured as part of the name and ended up in the key

[<Fact>]
let ``a tab after a section name is not part of the key`` () =
    Assert.Equal<Ini>(create [ "[server]"; "a = 1" ], create [ "[server\t]"; "a = 1" ])

[<Fact>]
let ``a tab after a parameter name is not part of the key`` () =
    Assert.Equal<Ini>(create [ "x = 1" ], create [ "x\t= 1" ])

[<Theory>]
[<InlineData("[name]")>]
[<InlineData("[name\t]")>]
[<InlineData("[\tname]")>]
[<InlineData("[\tname\t]")>]
[<InlineData("[ name ]")>]
[<InlineData("[\t name \t]")>]
[<InlineData("[name\v]")>]
[<InlineData("[name\f]")>]
let ``whitespace around a section name is trimmed`` section =
    let ini = create [ section; "x = 1" ]
    Assert.Equal<string list>([ "name:x" ], keys ini)
    Assert.True(ini |> Ini.containsKey "name:x")

[<Theory>]
[<InlineData("name = 1")>]
[<InlineData("name\t= 1")>]
[<InlineData("\tname = 1")>]
[<InlineData("\tname\t= 1")>]
[<InlineData("name\t=\t1")>]
[<InlineData("name\v= 1")>]
[<InlineData("name\f= 1")>]
let ``whitespace around a parameter name is trimmed`` line =
    let ini = create [ line ]
    Assert.Equal<string list>([ ":name" ], keys ini)
    Assert.Equal<string option>(Some "1", Ini.tryFind "name" ini)

[<Fact>]
let ``whitespace around a dotted section name is trimmed`` () =
    let ini = create [ "[\talpha.beta\t]"; "x = 1" ]
    Assert.Equal<string list>([ "alpha.beta:x" ], keys ini)

[<Fact>]
let ``whitespace around a section header line is ignored`` () =
    let ini = create [ "\t[alpha]\t"; "x = 1" ]
    Assert.Equal<string list>([ "alpha:x" ], keys ini)

[<Theory>]
[<InlineData("[name\tnested]")>]
[<InlineData("[name nested]")>]
[<InlineData("na\tme = 1")>]
[<InlineData("na me = 1")>]
let ``whitespace inside a name is rejected`` line =
    Assert.Equal($"Cannot parse line: %s{line}.", error [ line ])

[<Fact>]
let ``tabs are trimmed from a value but kept inside it`` () =
    let ini = create [ "x =\ta\tb\t" ]
    Assert.Equal<string option>(Some "a\tb", Ini.tryFind "x" ini)

[<Fact>]
let ``only the first equals sign separates name from value`` () =
    let ini = create [ "x = a=b=c" ]
    Assert.Equal<string option>(Some "a=b=c", Ini.tryFind "x" ini)

[<Theory>]
[<InlineData("x = 1 ; note", "1 ; note")>]
[<InlineData("x = 1 # note", "1 # note")>]
[<InlineData("x = ;#", ";#")>]
[<InlineData("x = https://example.com/doc#section", "https://example.com/doc#section")>]
let ``a comment marker inside a value is part of the value`` (line, expected) =
    let ini = create [ line ]
    Assert.Equal<string option>(Some expected, Ini.tryFind "x" ini)

[<Fact>]
let ``a later parameter overwrites an earlier one with the same key`` () =
    let ini = create [ "[alpha]"; "x = 1"; "x = 2" ]
    Assert.Equal(1, Ini.count ini)
    Assert.Equal<string option>(Some "2", Ini.tryFind "alpha:x" ini)

[<Fact>]
let ``a section may be reopened later in the file`` () =
    let ini = create [ "[alpha]"; "x = 1"; "[beta]"; "y = 2"; "[alpha]"; "z = 3" ]
    Assert.Equal<string list>([ "alpha:x"; "alpha:z"; "beta:y" ], keys ini)

// ---------------------------------------------------------------- parsing: errors

[<Theory>]
[<InlineData("x")>]
[<InlineData("[unclosed")>]
[<InlineData("unopened]")>]
[<InlineData("[]")>]
[<InlineData("a b = 1")>]
[<InlineData("[alpha] trailing")>]
let ``an unparsable line is an error`` line =
    let err = error [ line ]
    Assert.Equal($"Cannot parse line: %s{line}.", err)

[<Fact>]
let ``the first unparsable line determines the error`` () =
    let err = error [ "x = 1"; "oops"; "also bad" ]
    Assert.Equal("Cannot parse line: oops.", err)

// ---------------------------------------------------------------- lazy input

[<Fact>]
let ``parsing stops at the first unparsable line`` () =
    let consumed = ref 0

    let lines =
        seq {
            for i in 1..10000 do
                consumed.Value <- consumed.Value + 1
                if i = 3 then yield "not a valid line" else yield $"x{i} = {i}"
        }

    Assert.Equal("Cannot parse line: not a valid line.", error lines)
    Assert.Equal(3, consumed.Value)

[<Fact>]
let ``the whole sequence is read when every line parses`` () =
    let consumed = ref 0

    let lines =
        seq {
            for i in 1..100 do
                consumed.Value <- consumed.Value + 1
                yield $"x{i} = {i}"
        }

    Assert.Equal(100, Ini.count (create lines))
    Assert.Equal(100, consumed.Value)

[<Fact>]
let ``the enumerator is disposed when parsing stops early`` () =
    // the finally block of a sequence expression runs when the enumerator is
    // disposed, so this fails if the enumerator is abandoned instead
    let disposed = ref false
    let pulledPastError = ref false

    let lines =
        seq {
            try
                yield "ok = 1"
                yield "not a valid line"
                pulledPastError.Value <- true
                yield "never = reached"
            finally
                disposed.Value <- true
        }

    Assert.Equal("Cannot parse line: not a valid line.", error lines)
    Assert.True(disposed.Value, "the enumerator was not disposed")
    Assert.False(pulledPastError.Value, "enumeration continued past the error")

[<Fact(Timeout = 10000)>]
let ``an infinite sequence containing an unparsable line terminates`` () =
    let lines =
        seq {
            let mutable i = 0

            while true do
                i <- i + 1
                if i = 5 then yield "not a valid line" else yield $"x{i} = {i}"
        }

    Assert.Equal("Cannot parse line: not a valid line.", error lines)

// ---------------------------------------------------------------- the colon is reserved

[<Theory>]
[<InlineData("[alpha:beta]")>]
[<InlineData("[:alpha]")>]
[<InlineData("[alpha:]")>]
let ``a section name cannot contain a colon`` line =
    let err = error [ line ]
    Assert.Equal($"Cannot parse line: %s{line}.", err)

[<Theory>]
[<InlineData("beta:x = 1")>]
[<InlineData(":x = 1")>]
[<InlineData("x: = 1")>]
let ``a parameter name cannot contain a colon`` line =
    let err = error [ line ]
    Assert.Equal($"Cannot parse line: %s{line}.", err)

[<Fact>]
let ``a section name and a parameter name cannot be confused`` () =
    // both of these once produced the single key "alpha:beta:x"
    Assert.Equal("Cannot parse line: [alpha:beta].", error [ "[alpha:beta]"; "x = 1" ])
    Assert.Equal("Cannot parse line: beta:x = 1.", error [ "[alpha]"; "beta:x = 1" ])

[<Fact>]
let ``every key holds exactly one colon and splits unambiguously`` () =
    let ini = create [ "timeout = 30"; "[server]"; "host = localhost"; "[server.dev]"; "port = 5000" ]

    for key in Ini.keys ini do
        Assert.Equal(key.IndexOf ':', key.LastIndexOf ':')

    Assert.Equal<string list>([ ":timeout"; "server.dev:port"; "server:host" ], keys ini)

[<Fact>]
let ``a value may still contain colons`` () =
    let ini = create [ "url = https://example.com:8080/a:b" ]
    Assert.Equal<string option>(Some "https://example.com:8080/a:b", Ini.tryFind "url" ini)

// ---------------------------------------------------------------- lookup

[<Fact>]
let ``tryFind returns None for a missing key`` () =
    let ini = create [ "[alpha]"; "x = 1" ]
    Assert.Equal<string option>(None, Ini.tryFind "alpha:nope" ini)
    Assert.Equal<string option>(None, Ini.tryFind "nope:x" ini)

[<Fact>]
let ``a key without a separator refers to the root section`` () =
    let ini = create [ "x = 1" ]
    Assert.Equal<string option>(Some "1", Ini.tryFind "x" ini)
    Assert.True(Ini.containsKey "x" ini)

[<Fact>]
let ``a root key does not match a sectioned parameter`` () =
    let ini = create [ "[alpha]"; "x = 1" ]
    Assert.Equal<string option>(None, Ini.tryFind "x" ini)

[<Theory>]
[<InlineData("alpha:x")>]
[<InlineData("ALPHA:X")>]
[<InlineData("Alpha:X")>]
let ``keys are compared case insensitively`` key =
    let ini = create [ "[alpha]"; "x = 1" ]
    Assert.True(Ini.containsKey key ini)
    Assert.Equal<string option>(Some "1", Ini.tryFind key ini)

[<Fact>]
let ``values keep their original case`` () =
    let ini = create [ "x = MixedCase" ]
    Assert.Equal<string option>(Some "MixedCase", Ini.tryFind "x" ini)

[<Fact>]
let ``lookups take the ini last so they compose with pipe`` () =
    let ini = create [ "root = 0"; "[alpha]"; "x = 1" ]

    Assert.True(ini |> Ini.containsKey "alpha:x")
    Assert.False(ini |> Ini.containsKey "alpha:nope")
    Assert.Equal<string option>(Some "1", ini |> Ini.tryFind "alpha:x")
    Assert.Equal<string option>(Some "0", ini |> Ini.tryFindNested "alpha:root")

[<Fact>]
let ``keys and values are aligned and ordered`` () =
    let ini = create [ "[beta]"; "y = 2"; "[alpha]"; "x = 1" ]
    Assert.Equal<string list>([ "alpha:x"; "beta:y" ], keys ini)
    Assert.Equal<string list>([ "1"; "2" ], values ini)

// ---------------------------------------------------------------- nested lookup

[<Fact>]
let ``tryFindNested finds an exact match`` () =
    let ini = create [ "[alpha.beta]"; "x = 1" ]
    Assert.Equal<string option>(Some "1", Ini.tryFindNested "alpha.beta:x" ini)

[<Fact>]
let ``tryFindNested falls back to the parent section`` () =
    let ini = create [ "[alpha]"; "x = 1"; "[alpha.beta]"; "y = 2" ]
    Assert.Equal<string option>(Some "1", Ini.tryFindNested "alpha.beta:x" ini)

[<Fact>]
let ``tryFindNested walks the whole hierarchy up to the root`` () =
    let ini = create [ "root = 0"; "[alpha]"; "x = 1"; "[alpha.beta]"; "y = 2" ]
    Assert.Equal<string option>(Some "2", Ini.tryFindNested "alpha.beta.gamma:y" ini)
    Assert.Equal<string option>(Some "1", Ini.tryFindNested "alpha.beta.gamma:x" ini)
    Assert.Equal<string option>(Some "0", Ini.tryFindNested "alpha.beta.gamma:root" ini)

[<Fact>]
let ``tryFindNested prefers the most specific section`` () =
    let ini = create [ "x = root"; "[alpha]"; "x = alpha"; "[alpha.beta]"; "x = beta" ]
    Assert.Equal<string option>(Some "beta", Ini.tryFindNested "alpha.beta:x" ini)
    Assert.Equal<string option>(Some "alpha", Ini.tryFindNested "alpha:x" ini)
    Assert.Equal<string option>(Some "root", Ini.tryFindNested "x" ini)

[<Fact>]
let ``tryFindNested returns None when no ancestor has the parameter`` () =
    let ini = create [ "[alpha]"; "x = 1" ]
    Assert.Equal<string option>(None, Ini.tryFindNested "alpha.beta:nope" ini)

[<Fact>]
let ``tryFindNested does not search sibling or child sections`` () =
    let ini = create [ "[alpha.beta]"; "x = 1" ]
    Assert.Equal<string option>(None, Ini.tryFindNested "alpha.gamma:x" ini)
    Assert.Equal<string option>(None, Ini.tryFindNested "alpha:x" ini)

// ---------------------------------------------------------------- equality

[<Fact>]
let ``two inis with the same content are equal`` () =
    let a = create [ "[alpha]"; "x = 1" ]
    let b = create [ "[alpha]"; "x = 1" ]
    Assert.Equal<Ini>(a, b)
    Assert.Equal(hash a, hash b)

[<Fact>]
let ``equality ignores the case of section and parameter names`` () =
    let a = create [ "[alpha]"; "x = 1" ]
    let b = create [ "[ALPHA]"; "X = 1" ]
    Assert.Equal<Ini>(a, b)
    Assert.Equal(hash a, hash b)

[<Fact>]
let ``equality ignores declaration order`` () =
    let a = create [ "[alpha]"; "x = 1"; "[beta]"; "y = 2" ]
    let b = create [ "[beta]"; "y = 2"; "[alpha]"; "x = 1" ]
    Assert.Equal<Ini>(a, b)

[<Fact>]
let ``equality ignores comments and blank lines`` () =
    let a = create [ "[alpha]"; "x = 1" ]
    let b = create [ "; a comment"; ""; "[alpha]"; "# another"; "x = 1"; "" ]
    Assert.Equal<Ini>(a, b)

[<Fact>]
let ``equality respects values`` () =
    let a = create [ "[alpha]"; "x = 1" ]
    Assert.NotEqual<Ini>(a, create [ "[alpha]"; "x = 2" ])

[<Fact>]
let ``equality respects the case of values`` () =
    let a = create [ "x = Value" ]
    Assert.NotEqual<Ini>(a, create [ "x = value" ])

[<Fact>]
let ``an empty ini equals a freshly parsed empty document`` () =
    Assert.Equal<Ini>(Ini.empty, create [])
    Assert.Equal<Ini>(Ini.empty, create [ "; only a comment" ])

// ---------------------------------------------------------------- append

[<Fact>]
let ``tryAppend adds entries to an existing ini`` () =
    let ini = create [ "[alpha]"; "x = 1" ]

    match Ini.tryAppend [ "[beta]"; "y = 2" ] ini with
    | Error err -> failwith err
    | Ok appended -> Assert.Equal<string list>([ "alpha:x"; "beta:y" ], keys appended)

[<Fact>]
let ``tryAppend overwrites existing keys`` () =
    let ini = create [ "[alpha]"; "x = 1" ]

    match Ini.tryAppend [ "[alpha]"; "x = 2" ] ini with
    | Error err -> failwith err
    | Ok appended ->
        Assert.Equal(1, Ini.count appended)
        Assert.Equal<string option>(Some "2", Ini.tryFind "alpha:x" appended)

[<Fact>]
let ``tryAppend leaves the original ini unchanged`` () =
    let ini = create [ "[alpha]"; "x = 1" ]

    match Ini.tryAppend [ "[alpha]"; "x = 2" ] ini with
    | Error err -> failwith err
    | Ok _ -> Assert.Equal<string option>(Some "1", Ini.tryFind "alpha:x" ini)

[<Fact>]
let ``tryAppend starts at the root section`` () =
    let ini = create [ "[alpha]"; "x = 1" ]

    match Ini.tryAppend [ "y = 2" ] ini with
    | Error err -> failwith err
    | Ok appended -> Assert.Equal<string list>([ ":y"; "alpha:x" ], keys appended)

[<Fact>]
let ``tryAppend reports an unparsable line`` () =
    let ini = create [ "[alpha]"; "x = 1" ]

    match Ini.tryAppend [ "oops" ] ini with
    | Ok _ -> failwith "expected Error, got Ok"
    | Error err -> Assert.Equal("Cannot parse line: oops.", err)

// ---------------------------------------------------------------- add

let private added key value ini =
    match Ini.tryAdd key value ini with
    | Ok ini -> ini
    | Error err -> failwith $"expected Ok, got Error: %s{err}"

let private addError key value ini =
    match Ini.tryAdd key value ini with
    | Ok _ -> failwith "expected Error, got Ok"
    | Error err -> err

[<Fact>]
let ``add inserts a parameter into the root section`` () =
    let ini = added "x" "1" Ini.empty
    Assert.Equal<string list>([ ":x" ], keys ini)
    Assert.Equal<string option>(Some "1", Ini.tryFind "x" ini)

[<Theory>]
[<InlineData("alpha:x", "alpha:x")>]
[<InlineData("alpha.beta:x", "alpha.beta:x")>]
[<InlineData(":x", ":x")>]
[<InlineData("x", ":x")>]
let ``add accepts a sectioned key`` (key, expected) =
    let ini = added key "1" Ini.empty
    Assert.Equal<string list>([ expected ], keys ini)
    Assert.Equal<string option>(Some "1", Ini.tryFind key ini)

[<Fact>]
let ``add overwrites an existing key`` () =
    let ini = create [ "[alpha]"; "x = 1" ] |> added "alpha:x" "2"
    Assert.Equal(1, Ini.count ini)
    Assert.Equal<string option>(Some "2", Ini.tryFind "alpha:x" ini)

[<Fact>]
let ``add matches an existing key case insensitively`` () =
    let ini = create [ "[alpha]"; "x = 1" ] |> added "ALPHA:X" "2"
    Assert.Equal(1, Ini.count ini)
    Assert.Equal<string option>(Some "2", Ini.tryFind "alpha:x" ini)

[<Fact>]
let ``add leaves the original ini unchanged`` () =
    let ini = create [ "[alpha]"; "x = 1" ]
    added "alpha:x" "2" ini |> ignore
    added "beta:y" "2" ini |> ignore
    Assert.Equal(1, Ini.count ini)
    Assert.Equal<string option>(Some "1", Ini.tryFind "alpha:x" ini)

[<Fact>]
let ``add stores the value verbatim`` () =
    let ini = added "x" " a ; b # c = d " Ini.empty
    Assert.Equal<string option>(Some " a ; b # c = d ", Ini.tryFind "x" ini)

[<Fact>]
let ``add accepts an empty value`` () =
    Assert.Equal<string option>(Some "", added "x" "" Ini.empty |> Ini.tryFind "x")

[<Theory>]
[<InlineData("")>]
[<InlineData("   ")>]
[<InlineData("a b")>]
[<InlineData("a=b")>]
[<InlineData("a;b")>]
[<InlineData("a#b")>]
[<InlineData("[a]")>]
[<InlineData("alpha:x:y")>]
[<InlineData("alpha:")>]
[<InlineData(":")>]
[<InlineData("alpha : x")>]
[<InlineData("alpha:a b")>]
[<InlineData("a b:x")>]
[<InlineData(" alpha:x")>]
[<InlineData("alpha:x ")>]
let ``add rejects a key the parser could not read back`` key =
    Assert.Equal($"Invalid key: %s{key}.", addError key "1" Ini.empty)

[<Fact>]
let ``a key is not trimmed so add and tryFind always agree`` () =
    // lookups do not trim, so accepting an untrimmed key in add would store a key
    // that the same string cannot find again
    Assert.Equal("Invalid key:  x .", addError " x " "1" Ini.empty)

    for key in [ "x"; ":x"; "alpha:x"; "alpha.beta:x" ] do
        Assert.Equal<string option>(Some "1", added key "1" Ini.empty |> Ini.tryFind key)
        Assert.True(added key "1" Ini.empty |> Ini.containsKey key)

[<Fact>]
let ``a key added in code can be found by a nested lookup`` () =
    let ini = added "alpha:x" "1" Ini.empty
    Assert.Equal<string option>(Some "1", Ini.tryFindNested "alpha.beta.gamma:x" ini)

[<Fact>]
let ``add equals parsing the same parameter`` () =
    Assert.Equal<Ini>(create [ "[alpha]"; "x = 1" ], added "alpha:x" "1" Ini.empty)

// ---------------------------------------------------------------- remove

[<Fact>]
let ``remove deletes a key`` () =
    let ini = create [ "[alpha]"; "x = 1"; "y = 2" ] |> Ini.remove "alpha:x"
    Assert.Equal<string list>([ "alpha:y" ], keys ini)

[<Fact>]
let ``remove is a no-op for a missing key`` () =
    let ini = create [ "[alpha]"; "x = 1" ]
    Assert.Equal<Ini>(ini, ini |> Ini.remove "beta:y")
    Assert.Equal<Ini>(ini, ini |> Ini.remove "alpha:nope")

[<Fact>]
let ``remove is a no-op on an empty ini`` () =
    Assert.Equal<Ini>(Ini.empty, Ini.empty |> Ini.remove "alpha:x")

[<Theory>]
[<InlineData("alpha:x")>]
[<InlineData("ALPHA:X")>]
let ``remove matches a key case insensitively`` key =
    Assert.True(create [ "[alpha]"; "x = 1" ] |> Ini.remove key |> Ini.isEmpty)

[<Fact>]
let ``remove targets the root section for a key without a separator`` () =
    let ini = create [ "x = 1"; "[alpha]"; "x = 2" ] |> Ini.remove "x"
    Assert.Equal<string list>([ "alpha:x" ], keys ini)

[<Fact>]
let ``remove leaves the original ini unchanged`` () =
    let ini = create [ "[alpha]"; "x = 1" ]
    ini |> Ini.remove "alpha:x" |> ignore
    Assert.Equal<string option>(Some "1", Ini.tryFind "alpha:x" ini)

[<Fact>]
let ``remove ignores an invalid key`` () =
    let ini = create [ "[alpha]"; "x = 1" ]
    Assert.Equal<Ini>(ini, ini |> Ini.remove "a b")

// ---------------------------------------------------------------- find

[<Fact>]
let ``find returns the value`` () =
    let ini = create [ "x = 0"; "[alpha]"; "x = 1" ]
    Assert.Equal("1", ini |> Ini.find "alpha:x")
    Assert.Equal("0", ini |> Ini.find "x")
    Assert.Equal("1", ini |> Ini.find "ALPHA:X")

[<Fact>]
let ``find raises for a missing key`` () =
    let ini = create [ "[alpha]"; "x = 1" ]
    Assert.Throws<KeyNotFoundException>(fun () -> ini |> Ini.find "alpha:nope" |> ignore)
    |> ignore
    Assert.Throws<KeyNotFoundException>(fun () -> ini |> Ini.find "x" |> ignore) |> ignore

[<Fact>]
let ``findNested walks the hierarchy up to the root`` () =
    let ini = create [ "root = 0"; "[alpha]"; "x = 1" ]
    Assert.Equal("1", ini |> Ini.findNested "alpha.beta:x")
    Assert.Equal("0", ini |> Ini.findNested "alpha.beta:root")

[<Fact>]
let ``findNested raises for a key no ancestor has`` () =
    let ini = create [ "[alpha]"; "x = 1" ]

    let err =
        Assert.Throws<KeyNotFoundException>(fun () -> ini |> Ini.findNested "alpha.beta:nope" |> ignore)

    Assert.Equal("Key not found: alpha.beta:nope.", err.Message)

// ---------------------------------------------------------------- argument checks

[<Fact>]
let ``tryCreate rejects null lines`` () =
    Assert.Throws<ArgumentNullException>(fun () -> Ini.tryCreate null |> ignore) |> ignore

[<Fact>]
let ``tryAppend rejects null lines`` () =
    Assert.Throws<ArgumentNullException>(fun () -> Ini.tryAppend null Ini.empty |> ignore)
    |> ignore

[<Fact>]
let ``lookups reject a null key`` () =
    let ini = create [ "x = 1" ]
    Assert.Throws<ArgumentNullException>(fun () -> Ini.tryFind null ini |> ignore) |> ignore
    Assert.Throws<ArgumentNullException>(fun () -> Ini.tryFindNested null ini |> ignore)
    |> ignore
    Assert.Throws<ArgumentNullException>(fun () -> Ini.containsKey null ini |> ignore)
    |> ignore
    Assert.Throws<ArgumentNullException>(fun () -> Ini.find null ini |> ignore) |> ignore
    Assert.Throws<ArgumentNullException>(fun () -> Ini.findNested null ini |> ignore)
    |> ignore

[<Fact>]
let ``mutations reject a null key`` () =
    let ini = create [ "x = 1" ]
    Assert.Throws<ArgumentNullException>(fun () -> Ini.tryAdd null "1" ini |> ignore) |> ignore
    |> ignore
    Assert.Throws<ArgumentNullException>(fun () -> Ini.remove null ini |> ignore) |> ignore


// ---------------------------------------------------------------- enumeration

let private pairs (ini: Ini) =
    [ for pair in (ini :> IEnumerable<KeyValuePair<string, string>>) -> pair.Key, pair.Value ]

[<Fact>]
let ``an ini enumerates its pairs in key order`` () =
    let ini = create [ "[beta]"; "y = 2"; "[alpha]"; "x = 1" ]
    Assert.Equal<(string * string) list>([ "alpha:x", "1"; "beta:y", "2" ], pairs ini)

[<Fact>]
let ``enumeration agrees with keys and values`` () =
    let ini = create [ "root = 0"; "[beta]"; "y = 2"; "[alpha]"; "x = 1" ]
    Assert.Equal<string list>(keys ini, pairs ini |> List.map fst)
    Assert.Equal<string list>(values ini, pairs ini |> List.map snd)
    Assert.Equal(Ini.count ini, pairs ini |> List.length)

[<Fact>]
let ``an empty ini enumerates nothing`` () = Assert.Empty(pairs Ini.empty)

[<Fact>]
let ``the non generic enumerator yields the same pairs`` () =
    let ini = create [ "[beta]"; "y = 2"; "[alpha]"; "x = 1" ]
    let enumerator = (ini :> Collections.IEnumerable).GetEnumerator()

    let acc =
        [ while enumerator.MoveNext() do
              match enumerator.Current with
              | :? KeyValuePair<string, string> as pair -> yield pair.Key, pair.Value
              | other -> failwith $"unexpected element: %A{other}" ]

    Assert.Equal<(string * string) list>([ "alpha:x", "1"; "beta:y", "2" ], acc)

[<Fact>]
let ``enumeration terminates on a repeated pass`` () =
    // both GetEnumerator implementations were once self-recursive, which looped forever
    let ini = create [ "[alpha]"; "x = 1" ]

    for _ in 1..3 do
        Assert.Equal(1, pairs ini |> List.length)
