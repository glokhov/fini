namespace Fini.Tests

[<AutoOpen>]
module Helpers =

    open System
    open System.IO
    open Fini

    let key text = Key.parse text

    let name text = Name.get text

    let ok result =
        match result with
        | Ok value -> value
        | Error err -> failwith $"expected Ok, got Error %A{err}"

    let error result =
        match result with
        | Ok value -> failwith $"expected Error, got Ok %A{value}"
        | Error err -> err

    let ini lines = Ini.fromLines lines |> ok

    let keys doc = Ini.keys doc |> Seq.map string |> List.ofSeq

    let values doc = Ini.values doc |> List.ofSeq

    let lines doc = Ini.toLines doc |> List.ofSeq

    type TempFile(contents: string list) =
        let path = Path.GetTempFileName()

        do File.WriteAllLines(path, contents)

        new() = new TempFile([])

        member _.Path = path

        interface IDisposable with
            member _.Dispose() =
                if File.Exists path then
                    File.Delete path

module NameTests =

    open System
    open Fini
    open Xunit

    [<Theory>]
    [<InlineData("server")>]
    [<InlineData("server.dev")>]
    [<InlineData("a-b_c")>]
    [<InlineData("Ünïcode")>]
    let ``a valid name is accepted unchanged`` text =
        Assert.Equal(text, Name.tryGet text |> ok |> string)
        Assert.Equal(text, Name.get text |> string)

    [<Fact>]
    let ``the empty string is the root name`` () =
        Assert.Equal(Name.empty, Name.get "")
        Assert.Equal("", string Name.empty)

    [<Theory>]
    [<InlineData("a b")>]
    [<InlineData(" a")>]
    [<InlineData("a ")>]
    [<InlineData("a\t")>]
    [<InlineData("a\n")>]
    [<InlineData("\na")>]
    [<InlineData("a=b")>]
    [<InlineData("a:b")>]
    [<InlineData("a;b")>]
    [<InlineData("a#b")>]
    [<InlineData("[a]")>]
    [<InlineData("a]")>]
    let ``an invalid name is rejected, not trimmed`` text =
        Assert.Equal(InvalidName text, Name.tryGet text |> error)
        Assert.Throws<ArgumentException>(fun () -> Name.get text |> ignore) |> ignore

    [<Fact>]
    let ``names are compared case-insensitively`` () =
        Assert.Equal(Name.get "Server", Name.get "server")
        Assert.Equal(hash (Name.get "Server"), hash (Name.get "server"))
        Assert.Equal(0, compare (Name.get "Server") (Name.get "server"))

    [<Fact>]
    let ``a name keeps its original spelling`` () = Assert.Equal("Server", Name.get "Server" |> string)

    [<Fact>]
    let ``the root name sorts first`` () =
        let sorted = [ name "b"; Name.empty; name "A" ] |> List.sort |> List.map string
        Assert.Equal<string list>([ ""; "A"; "b" ], sorted)

    [<Fact>]
    let ``a parent name sorts before its children`` () =
        Assert.True(compare (name "server") (name "server.dev") < 0)

module KeyTests =

    open System
    open Fini
    open Xunit

    // ---------------------------------------------------------------- parse

    [<Theory>]
    [<InlineData("server:host", "server:host")>]
    [<InlineData("server.dev:port", "server.dev:port")>]
    [<InlineData("host", ":host")>]
    [<InlineData(":host", ":host")>]
    let ``a valid key is parsed`` text expected =
        Assert.Equal(expected, Key.tryParse text |> ok |> string)
        Assert.Equal(expected, Key.parse text |> string)

    [<Fact>]
    let ``a key without a colon is in the root section`` () = Assert.Equal(key ":host", key "host")

    [<Theory>]
    [<InlineData("")>]
    [<InlineData(":")>]
    [<InlineData("server:")>]
    [<InlineData("a:b:c")>]
    [<InlineData("a b")>]
    [<InlineData("server:a b")>]
    [<InlineData("server:x\n")>]
    [<InlineData("[server]:x")>]
    let ``an invalid key is rejected`` text =
        Assert.True(Key.tryParse text |> Result.isError)
        Assert.Throws<ArgumentException>(fun () -> Key.parse text |> ignore) |> ignore

    [<Fact>]
    let ``the error names the whole key when it has too many colons`` () =
        Assert.Equal(InvalidName "a:b:c", Key.tryParse "a:b:c" |> error)

    [<Fact>]
    let ``the error names the invalid part`` () =
        Assert.Equal(InvalidName "a b", Key.tryParse "a b:x" |> error)
        Assert.Equal(InvalidName "x y", Key.tryParse "a:x y" |> error)
        Assert.Equal(InvalidName "", Key.tryParse "a:" |> error)

    // ---------------------------------------------------------------- get

    [<Fact>]
    let ``a key is built from a section and a parameter`` () =
        Assert.Equal(key "server:host", Key.tryGet "server" "host" |> ok)
        Assert.Equal(key "server:host", Key.get "server" "host")

    [<Fact>]
    let ``an empty section builds a root key`` () = Assert.Equal(key "host", Key.get "" "host")

    [<Theory>]
    [<InlineData("server", "")>]
    [<InlineData("server", "a:b")>]
    [<InlineData("a:b", "x")>]
    [<InlineData("a b", "x")>]
    let ``invalid parts are rejected`` section parameter =
        Assert.True(Key.tryGet section parameter |> Result.isError)
        Assert.Throws<ArgumentException>(fun () -> Key.get section parameter |> ignore) |> ignore

    // ---------------------------------------------------------------- equality and order

    [<Fact>]
    let ``keys are compared case-insensitively`` () =
        Assert.Equal(key "Server:Host", key "server:host")
        Assert.Equal(hash (key "Server:Host"), hash (key "server:host"))
        Assert.Equal(0, compare (key "Server:Host") (key "server:host"))

    [<Fact>]
    let ``keys are ordered by section, then by parameter`` () =
        let sorted = [ key "b:a"; key "a:z"; key "z"; key "a:b"; key "a" ] |> List.sort |> List.map string

        Assert.Equal<string list>([ ":a"; ":z"; "a:b"; "a:z"; "b:a" ], sorted)

    [<Fact>]
    let ``a key keeps its original spelling`` () = Assert.Equal("Server:Host", key "Server:Host" |> string)

    [<Fact>]
    let ``a key converts implicitly from and to a string`` () =
        let fromString: Key = Key.op_Implicit "server:host"
        let toString: string = Key.op_Implicit fromString
        Assert.Equal("server:host", toString)

module ParsingTests =

    open System.IO
    open Fini
    open Xunit

    // ---------------------------------------------------------------- ignored lines

    [<Fact>]
    let ``no lines give an empty document`` () = Assert.True(ini [] |> Ini.isEmpty)

    [<Theory>]
    [<InlineData("")>]
    [<InlineData(" ")>]
    [<InlineData("\t ")>]
    let ``blank lines are ignored`` line = Assert.True(ini [ line ] |> Ini.isEmpty)

    [<Theory>]
    [<InlineData("; comment")>]
    [<InlineData("# comment")>]
    [<InlineData("  ; indented")>]
    [<InlineData(";")>]
    [<InlineData("; x = 1")>]
    [<InlineData("# [section]")>]
    let ``comments are ignored`` line = Assert.True(ini [ line ] |> Ini.isEmpty)

    // ---------------------------------------------------------------- parameters

    [<Fact>]
    let ``a parameter before any header is in the root section`` () =
        Assert.Equal<string list>([ ":x" ], ini [ "x = 1" ] |> keys)

    [<Fact>]
    let ``a parameter after a header is in that section`` () =
        let doc = ini [ "[server]"; "host = localhost" ]
        Assert.Equal(Some "localhost", doc |> Ini.tryFind (key "server:host"))

    [<Theory>]
    [<InlineData("x=1", "1")>]
    [<InlineData("  x   =   1  ", "1")>]
    [<InlineData("x =", "")>]
    [<InlineData("x = a = b", "a = b")>]
    [<InlineData("x = a:b;c#d", "a:b;c#d")>]
    [<InlineData("x = [a]", "[a]")>]
    [<InlineData("x = hello world", "hello world")>]
    let ``the value is everything after the first equals sign, trimmed`` line expected =
        Assert.Equal(Some expected, ini [ line ] |> Ini.tryFind (key "x"))

    [<Fact>]
    let ``values keep their case`` () =
        Assert.Equal(Some "MiXeD", ini [ "x = MiXeD" ] |> Ini.tryFind (key "x"))

    [<Fact>]
    let ``a later duplicate wins`` () =
        let doc = ini [ "[s]"; "x = 1"; "X = 2" ]
        Assert.Equal(1, Ini.count doc)
        Assert.Equal(Some "2", doc |> Ini.tryFind (key "s:x"))

    // ---------------------------------------------------------------- sections

    [<Theory>]
    [<InlineData("[server]")>]
    [<InlineData("  [ server ]  ")>]
    [<InlineData("[server] ")>]
    let ``a section header may be padded`` header =
        Assert.Equal(Some "1", ini [ header; "x = 1" ] |> Ini.tryFind (key "server:x"))

    [<Fact>]
    let ``a section can be reopened`` () =
        let doc = ini [ "[a]"; "x = 1"; "[b]"; "y = 2"; "[a]"; "z = 3" ]
        Assert.Equal<string list>([ "a:x"; "a:z"; "b:y" ], keys doc)

    [<Fact>]
    let ``a dotted header is a single flat section`` () =
        let doc = ini [ "[server.dev]"; "port = 5000" ]
        Assert.Equal(Some "5000", doc |> Ini.tryFind (key "server.dev:port"))
        Assert.Equal(None, doc |> Ini.tryFind (key "server:port"))

    [<Fact>]
    let ``an empty section adds nothing`` () = Assert.True(ini [ "[empty]" ] |> Ini.isEmpty)

    // ---------------------------------------------------------------- errors

    [<Theory>]
    [<InlineData("oops")>]
    [<InlineData("= 1")>]
    [<InlineData("a b = 1")>]
    [<InlineData("a:b = 1")>]
    [<InlineData("[]")>]
    [<InlineData("[a b]")>]
    [<InlineData("[a:b]")>]
    [<InlineData("[a")>]
    [<InlineData("a]")>]
    [<InlineData("[a] x = 1")>]
    [<InlineData("x = 1\n[y]")>]
    let ``an invalid line is reported as it is`` line =
        Assert.Equal(InvalidLine line, Ini.fromLines [ "x = 1"; line ] |> error)

    [<Fact>]
    let ``the first invalid line is reported`` () =
        Assert.Equal(InvalidLine "first", Ini.fromLines [ "first"; "second" ] |> error)

    [<Fact>]
    let ``reading stops at the first invalid line`` () =
        let mutable read = 0

        let source =
            seq {
                for line in [ "x = 1"; "oops"; "y = 2"; "z = 3" ] do
                    read <- read + 1
                    yield line
            }

        Ini.fromLines source |> error |> ignore
        Assert.Equal(2, read)

    // ---------------------------------------------------------------- files

    [<Fact>]
    let ``a file is parsed`` () =
        use file = new TempFile([ "; comment"; "timeout = 30"; "[server]"; "host = localhost" ])
        let doc = Ini.fromFile file.Path |> ok
        Assert.Equal<string list>([ ":timeout"; "server:host" ], keys doc)

    [<Fact>]
    let ``an invalid line in a file is reported`` () =
        use file = new TempFile([ "x = 1"; "oops" ])
        Assert.Equal(InvalidLine "oops", Ini.fromFile file.Path |> error)

    [<Fact>]
    let ``a missing file throws`` () =
        let path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName())
        Assert.Throws<FileNotFoundException>(fun () -> Ini.fromFile path |> ignore) |> ignore

module BuildingTests =

    open Fini
    open Xunit

    // ---------------------------------------------------------------- empty

    [<Fact>]
    let ``empty has nothing`` () =
        Assert.True(Ini.isEmpty Ini.empty)
        Assert.Equal(0, Ini.count Ini.empty)
        Assert.Empty(Ini.keys Ini.empty)
        Assert.Empty(Ini.values Ini.empty)
        Assert.Empty(Ini.sections Ini.empty)

    // ---------------------------------------------------------------- add

    [<Fact>]
    let ``add returns a new document`` () =
        let doc = Ini.empty |> Ini.add (key "s:x") "1"
        Assert.Equal(Some "1", doc |> Ini.tryFind (key "s:x"))
        Assert.True(Ini.isEmpty Ini.empty)

    [<Fact>]
    let ``add replaces an existing value`` () =
        let doc = ini [ "[s]"; "x = 1" ] |> Ini.add (key "S:X") "2"
        Assert.Equal(1, Ini.count doc)
        Assert.Equal(Some "2", doc |> Ini.tryFind (key "s:x"))

    [<Fact>]
    let ``add takes the spelling of the latest key`` () =
        let doc = ini [ "[s]"; "x = 1" ] |> Ini.add (key "S:X") "2"
        Assert.Equal<string list>([ "S:X" ], keys doc)

    [<Fact>]
    let ``add keeps the value as it is`` () =
        let doc = Ini.empty |> Ini.add (key "x") "  padded  "
        Assert.Equal(Some "  padded  ", doc |> Ini.tryFind (key "x"))

    // ---------------------------------------------------------------- remove

    [<Fact>]
    let ``remove deletes a key`` () =
        let doc = ini [ "x = 1"; "y = 2" ] |> Ini.remove (key "X")
        Assert.Equal<string list>([ ":y" ], keys doc)

    [<Fact>]
    let ``remove of a missing key does nothing`` () =
        let doc = ini [ "x = 1" ]
        Assert.Equal<Ini>(doc, doc |> Ini.remove (key "s:x"))

    // ---------------------------------------------------------------- ofSeq and append

    [<Fact>]
    let ``ofSeq builds a document from pairs`` () =
        let doc = Ini.ofSeq [ key "s:x", "1"; key "y", "2" ]
        Assert.Equal<string list>([ ":y"; "s:x" ], keys doc)

    [<Fact>]
    let ``ofSeq lets a later duplicate win`` () =
        let doc = Ini.ofSeq [ key "x", "1"; key "X", "2" ]
        Assert.Equal<string list>([ "2" ], values doc)

    [<Fact>]
    let ``append adds pairs on top`` () =
        let doc = ini [ "x = 1"; "y = 2" ] |> Ini.append [ key "y", "20"; key "z", "30" ]
        Assert.Equal<string list>([ "1"; "20"; "30" ], values doc)

    [<Fact>]
    let ``append merges one document into another`` () =
        let defaults = ini [ "[s]"; "a = 1"; "b = 2" ]
        let overrides = ini [ "[s]"; "b = 20"; "c = 30" ]
        let merged = defaults |> Ini.append (Ini.toSeq overrides)
        Assert.Equal<Ini>(ini [ "[s]"; "a = 1"; "b = 20"; "c = 30" ], merged)

    // ---------------------------------------------------------------- appendLines and appendFile

    [<Fact>]
    let ``appendLines layers new values on top`` () =
        let doc = ini [ "[s]"; "x = 1"; "y = 2" ] |> Ini.appendLines [ "[s]"; "y = 20" ] |> ok
        Assert.Equal<Ini>(ini [ "[s]"; "x = 1"; "y = 20" ], doc)

    [<Fact>]
    let ``appendLines starts in the root section`` () =
        let doc = ini [ "[s]"; "x = 1" ] |> Ini.appendLines [ "y = 2" ] |> ok
        Assert.Equal<string list>([ ":y"; "s:x" ], keys doc)

    [<Fact>]
    let ``appendLines reports an invalid line`` () =
        Assert.Equal(InvalidLine "oops", ini [ "x = 1" ] |> Ini.appendLines [ "oops" ] |> error)

    [<Fact>]
    let ``appendFile layers a file on top`` () =
        use file = new TempFile([ "x = 10"; "[s]"; "y = 2" ])
        let doc = ini [ "x = 1"; "z = 3" ] |> Ini.appendFile file.Path |> ok
        Assert.Equal<Ini>(ini [ "x = 10"; "z = 3"; "[s]"; "y = 2" ], doc)

    [<Fact>]
    let ``appendFile reports an invalid line`` () =
        use file = new TempFile([ "oops" ])
        Assert.Equal(InvalidLine "oops", Ini.empty |> Ini.appendFile file.Path |> error)

    [<Fact>]
    let ``the original document is not changed`` () =
        let doc = ini [ "x = 1" ]
        doc |> Ini.add (key "y") "2" |> ignore
        doc |> Ini.append [ key "z", "3" ] |> ignore
        doc |> Ini.appendLines [ "w = 4" ] |> ignore
        doc |> Ini.remove (key "x") |> ignore
        Assert.Equal<string list>([ ":x" ], keys doc)

module LookupTests =

    open System.Collections.Generic
    open Fini
    open Xunit

    let private doc =
        ini
            [ "timeout = 30"
              "[server]"
              "host = localhost"
              "port = 8080"
              "[server.dev]"
              "port = 5000"
              "[server.dev.local]"
              "debug = true" ]

    // ---------------------------------------------------------------- exact

    [<Fact>]
    let ``tryFind returns the value`` () =
        Assert.Equal(Some "localhost", doc |> Ini.tryFind (key "server:host"))
        Assert.Equal(Some "30", doc |> Ini.tryFind (key "timeout"))

    [<Fact>]
    let ``tryFind ignores case`` () =
        Assert.Equal(Some "localhost", doc |> Ini.tryFind (key "SERVER:HOST"))

    [<Fact>]
    let ``tryFind does not inherit`` () =
        Assert.Equal(None, doc |> Ini.tryFind (key "server.dev:host"))

    [<Fact>]
    let ``find returns the value`` () = Assert.Equal("8080", doc |> Ini.find (key "server:port"))

    [<Fact>]
    let ``find throws for a missing key`` () =
        Assert.Throws<KeyNotFoundException>(fun () -> doc |> Ini.find (key "server:missing") |> ignore)
        |> ignore

    [<Fact>]
    let ``containsKey checks an exact key`` () =
        Assert.True(doc |> Ini.containsKey (key "Server:Port"))
        Assert.False(doc |> Ini.containsKey (key "server.dev:host"))

    // ---------------------------------------------------------------- nested

    [<Theory>]
    [<InlineData("server.dev:port", "5000")>] // the section itself
    [<InlineData("server.dev:host", "localhost")>] // the parent
    [<InlineData("server.dev:timeout", "30")>] // the root
    [<InlineData("server.dev.local:debug", "true")>]
    [<InlineData("server.dev.local:port", "5000")>] // the nearest ancestor wins
    [<InlineData("server.dev.local:host", "localhost")>]
    [<InlineData("other:timeout", "30")>]
    [<InlineData("timeout", "30")>]
    let ``tryFindNested walks up the dotted sections`` text expected =
        Assert.Equal(Some expected, doc |> Ini.tryFindNested (key text))

    [<Fact>]
    let ``tryFindNested skips missing intermediate sections`` () =
        Assert.Equal(Some "localhost", doc |> Ini.tryFindNested (key "server.a.b:host"))

    [<Fact>]
    let ``tryFindNested does not look into children`` () =
        Assert.Equal(None, doc |> Ini.tryFindNested (key "server:debug"))

    [<Fact>]
    let ``tryFindNested returns None when no ancestor has the key`` () =
        Assert.Equal(None, doc |> Ini.tryFindNested (key "server.dev:missing"))

    [<Fact>]
    let ``findNested returns the inherited value`` () =
        Assert.Equal("localhost", doc |> Ini.findNested (key "server.dev:host"))

    [<Fact>]
    let ``findNested throws when no ancestor has the key`` () =
        Assert.Throws<KeyNotFoundException>(fun () -> doc |> Ini.findNested (key "server.dev:missing") |> ignore)
        |> ignore

module TypedLookupTests =

    open System
    open System.Collections.Generic
    open Fini
    open Xunit

    let private doc =
        ini
            [ "timeout = 00:00:30"
              "[server]"
              "port = 8080"
              "ratio = 1.5"
              "name = web"
              "[server.dev]"
              "port = abc" ]

    // ---------------------------------------------------------------- IParsable

    [<Fact>]
    let ``tryFindAs parses the value`` () =
        Assert.Equal(Some(Ok 8080), doc |> Ini.tryFindAs<int> (key "server:port"))
        Assert.Equal(Some(Ok 1.5), doc |> Ini.tryFindAs<float> (key "server:ratio"))
        Assert.Equal(Some(Ok(TimeSpan.FromSeconds 30.0)), doc |> Ini.tryFindAs<TimeSpan> (key "timeout"))

    [<Fact>]
    let ``tryFindAs uses the invariant culture`` () =
        let original = Globalization.CultureInfo.CurrentCulture

        try
            Globalization.CultureInfo.CurrentCulture <- Globalization.CultureInfo.GetCultureInfo "de-DE"
            Assert.Equal(Some(Ok 1.5), doc |> Ini.tryFindAs<float> (key "server:ratio"))
        finally
            Globalization.CultureInfo.CurrentCulture <- original

    [<Fact>]
    let ``tryFindAs reports a value that does not parse`` () =
        Assert.Equal(Some(Error(InvalidValue "web")), doc |> Ini.tryFindAs<int> (key "server:name"))

    [<Fact>]
    let ``tryFindAs returns None for a missing key`` () =
        Assert.Equal(None, doc |> Ini.tryFindAs<int> (key "server:missing"))

    [<Fact>]
    let ``tryFindNestedAs parses the inherited value`` () =
        Assert.Equal(Some(Ok 1.5), doc |> Ini.tryFindNestedAs<float> (key "server.dev:ratio"))
        Assert.Equal(Some(Error(InvalidValue "abc")), doc |> Ini.tryFindNestedAs<int> (key "server.dev:port"))
        Assert.Equal(None, doc |> Ini.tryFindNestedAs<int> (key "server.dev:missing"))

    [<Fact>]
    let ``findAs parses the value`` () =
        Assert.Equal(8080, doc |> Ini.findAs<int> (key "server:port"))
        Assert.Equal(8080, doc |> Ini.findNestedAs<int> (key "server.prod:port"))

    [<Fact>]
    let ``findAs throws for a value that does not parse`` () =
        Assert.Throws<FormatException>(fun () -> doc |> Ini.findAs<int> (key "server:name") |> ignore)
        |> ignore

        Assert.Throws<FormatException>(fun () -> doc |> Ini.findNestedAs<int> (key "server.dev:port") |> ignore)
        |> ignore

    [<Fact>]
    let ``findAs throws for a missing key`` () =
        Assert.Throws<KeyNotFoundException>(fun () -> doc |> Ini.findAs<int> (key "missing") |> ignore)
        |> ignore

        Assert.Throws<KeyNotFoundException>(fun () -> doc |> Ini.findNestedAs<int> (key "a.b:missing") |> ignore)
        |> ignore

    // ---------------------------------------------------------------- custom parser

    type Mode =
        | Fast
        | Slow

    let private parseMode text =
        match text with
        | "web" -> Fast
        | "abc" -> Slow
        | _ -> failwith $"unknown mode {text}"

    [<Fact>]
    let ``tryFindWith applies the parser`` () =
        Assert.Equal(Some(Ok Fast), doc |> Ini.tryFindWith parseMode (key "server:name"))
        Assert.Equal(Some(Ok Slow), doc |> Ini.tryFindNestedWith parseMode (key "server.dev:port"))

    [<Fact>]
    let ``tryFindWith turns an exception into InvalidValue`` () =
        Assert.Equal(Some(Error(InvalidValue "8080")), doc |> Ini.tryFindWith parseMode (key "server:port"))
        Assert.Equal(Some(Error(InvalidValue "8080")), doc |> Ini.tryFindNestedWith parseMode (key "server.x:port"))

    [<Fact>]
    let ``tryFindWith returns None without calling the parser`` () =
        let mutable called = false
        let parser (_: string) = called <- true

        Assert.Equal(None, doc |> Ini.tryFindWith parser (key "missing"))
        Assert.Equal(None, doc |> Ini.tryFindNestedWith parser (key "a.b:missing"))
        Assert.False called

    [<Fact>]
    let ``findWith applies the parser`` () =
        Assert.Equal(Fast, doc |> Ini.findWith parseMode (key "server:name"))
        Assert.Equal(Fast, doc |> Ini.findNestedWith parseMode (key "server.dev:name"))

    [<Fact>]
    let ``findWith lets the parser exception through`` () =
        Assert.Throws<Exception>(fun () -> doc |> Ini.findWith parseMode (key "server:port") |> ignore)
        |> ignore

    [<Fact>]
    let ``findWith throws for a missing key`` () =
        Assert.Throws<KeyNotFoundException>(fun () -> doc |> Ini.findWith parseMode (key "missing") |> ignore)
        |> ignore

        Assert.Throws<KeyNotFoundException>(fun () -> doc |> Ini.findNestedWith parseMode (key "a:missing") |> ignore)
        |> ignore

module SectionTests =

    open Fini
    open Xunit

    let private doc =
        ini
            [ "timeout = 30"
              "[server]"
              "host = localhost"
              "port = 8080"
              "[server.dev]"
              "port = 5000"
              "[Client]"
              "id = 1" ]

    [<Fact>]
    let ``sections lists the names in order, with the root first`` () =
        Assert.Equal<string list>([ ""; "Client"; "server"; "server.dev" ], doc |> Ini.sections |> Seq.map string |> List.ofSeq)

    [<Fact>]
    let ``sections has no root when there are no root parameters`` () =
        Assert.Equal<string list>([ "a" ], ini [ "[a]"; "x = 1" ] |> Ini.sections |> Seq.map string |> List.ofSeq)

    [<Fact>]
    let ``section returns one section with its keys`` () =
        let server = doc |> Ini.section (name "SERVER")
        Assert.Equal<string list>([ "server:host"; "server:port" ], keys server)

    [<Fact>]
    let ``section of the root returns the root parameters`` () =
        Assert.Equal<string list>([ ":timeout" ], doc |> Ini.section Name.empty |> keys)

    [<Fact>]
    let ``section of a missing name is empty`` () =
        Assert.True(doc |> Ini.section (name "missing") |> Ini.isEmpty)

    [<Fact>]
    let ``removeSection keeps everything else, including children`` () =
        let rest = doc |> Ini.removeSection (name "server")
        Assert.Equal<string list>([ ":timeout"; "Client:id"; "server.dev:port" ], keys rest)

    [<Fact>]
    let ``removeSection of the root removes the root parameters`` () =
        Assert.False(doc |> Ini.removeSection Name.empty |> Ini.containsKey (key "timeout"))

    [<Fact>]
    let ``removeSection of a missing name does nothing`` () =
        Assert.Equal<Ini>(doc, doc |> Ini.removeSection (name "missing"))

    [<Fact>]
    let ``a section and its children can be removed together`` () =
        let rest =
            doc
            |> Ini.sections
            |> Seq.filter (fun n -> n = name "server" || (string n).StartsWith "server.")
            |> Seq.fold (fun d n -> d |> Ini.removeSection n) doc

        Assert.Equal<string list>([ ":timeout"; "Client:id" ], keys rest)

module RenderingTests =

    open System
    open System.IO
    open Fini
    open Xunit

    let private doc =
        ini
            [ "; dropped"
              "timeout = 30"
              "[server.dev]"
              "port = 5000"
              "[server]"
              "port = 8080"
              "host = localhost"
              "[Client]"
              "empty =" ]

    let private expected =
        [ "timeout = 30"
          ""
          "[Client]"
          "empty ="
          ""
          "[server]"
          "host = localhost"
          "port = 8080"
          ""
          "[server.dev]"
          "port = 5000" ]

    [<Fact>]
    let ``toLines writes the standard layout`` () = Assert.Equal<string list>(expected, lines doc)

    [<Fact>]
    let ``toLines of an empty document is empty`` () = Assert.Empty(Ini.toLines Ini.empty)

    [<Fact>]
    let ``toLines without root parameters starts with a header`` () =
        Assert.Equal<string list>([ "[a]"; "x = 1" ], ini [ "[a]"; "x = 1" ] |> lines)

    [<Fact>]
    let ``toLines with only root parameters has no header`` () =
        Assert.Equal<string list>([ "x = 1"; "y = 2" ], ini [ "y = 2"; "x = 1" ] |> lines)

    [<Fact>]
    let ``toString joins the lines`` () =
        Assert.Equal(String.Join(Environment.NewLine, expected), Ini.toString doc)
        Assert.Equal(Ini.toString doc, doc.ToString())

    [<Fact>]
    let ``toFile writes the lines`` () =
        use file = new TempFile()
        doc |> Ini.toFile file.Path
        Assert.Equal<string list>(expected, File.ReadAllLines file.Path |> List.ofArray)

    [<Fact>]
    let ``a written document reads back equal`` () =
        use file = new TempFile()
        doc |> Ini.toFile file.Path
        Assert.Equal<Ini>(doc, Ini.fromFile file.Path |> ok)
        Assert.Equal<Ini>(doc, Ini.fromLines (Ini.toLines doc) |> ok)

    [<Fact>]
    let ``values that look like syntax survive a round trip`` () =
        let tricky = Ini.ofSeq [ key "a", "b = c"; key "s:x", "; not a comment"; key "s:y", "[z]" ]
        Assert.Equal<Ini>(tricky, Ini.fromLines (Ini.toLines tricky) |> ok)

module EqualityTests =

    open System
    open System.Collections.Generic
    open Fini
    open Xunit

    // ---------------------------------------------------------------- enumeration

    [<Fact>]
    let ``toSeq returns pairs in key order`` () =
        let pairs =
            ini [ "[b]"; "x = 2"; "[a]"; "x = 1" ]
            |> Ini.toSeq
            |> Seq.map (fun (k, v) -> string k, v)
            |> List.ofSeq

        Assert.Equal<(string * string) list>([ "a:x", "1"; "b:x", "2" ], pairs)

    [<Fact>]
    let ``the document enumerates key value pairs`` () =
        let doc = ini [ "x = 1"; "[s]"; "y = 2" ]
        let pairs = doc |> Seq.map (fun p -> string p.Key, p.Value) |> List.ofSeq
        Assert.Equal<(string * string) list>([ ":x", "1"; "s:y", "2" ], pairs)
        Assert.Equal<KeyValuePair<Key, string> list>(List.ofSeq doc, List.ofSeq doc.KeyValuePairs)

    [<Fact>]
    let ``keys and values follow the same order`` () =
        let doc = ini [ "[b]"; "x = 2"; "y = 3"; "[a]"; "x = 1" ]
        Assert.Equal<string list>([ "a:x"; "b:x"; "b:y" ], keys doc)
        Assert.Equal<string list>([ "1"; "2"; "3" ], values doc)

    // ---------------------------------------------------------------- equality

    [<Fact>]
    let ``documents with the same keys and values are equal`` () =
        let a = ini [ "; comment"; "[S]"; "X = 1"; "y = 2" ]
        let b = ini [ "[s]"; "Y = 2"; ""; "x = 1" ]
        Assert.Equal<Ini>(a, b)
        Assert.True(a.Equals b)
        Assert.True((a :> IEquatable<Ini>).Equals b)
        Assert.Equal(hash a, hash b)

    [<Fact>]
    let ``values are compared case-sensitively`` () = Assert.NotEqual<Ini>(ini [ "x = a" ], ini [ "x = A" ])

    [<Fact>]
    let ``different keys are not equal`` () =
        Assert.NotEqual<Ini>(ini [ "x = 1" ], ini [ "[s]"; "x = 1" ])

    // ---------------------------------------------------------------- comparison

    [<Fact>]
    let ``documents are comparable`` () =
        Assert.Equal(0, compare (ini [ "X = 1" ]) (ini [ "x = 1" ]))
        Assert.True(compare (ini [ "x = 1" ]) (ini [ "x = 2" ]) < 0)
        Assert.True(compare (ini [ "a = 9" ]) (ini [ "b = 1" ]) < 0)

    [<Fact>]
    let ``documents can be sorted and used in sets`` () =
        let set = Set.ofList [ ini [ "x = 2" ]; ini [ "x = 1" ]; ini [ "X = 1" ] ]
        Assert.Equal(2, set.Count)
        Assert.Equal<Ini>(ini [ "x = 1" ], Set.minElement set)

    [<Fact>]
    let ``documents implement IComparable`` () =
        let a: IComparable = ini [ "x = 1" ]
        Assert.True(a.CompareTo(ini [ "x = 2" ]) < 0)
