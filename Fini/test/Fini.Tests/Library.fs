module Fini.Tests.IniTests

open System
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
