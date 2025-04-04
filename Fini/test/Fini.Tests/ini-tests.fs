module Ini.Tests

open System.IO
open Fini
open FsUnit
open Xunit

// empty

[<Fact>]
let ``if empty, isEmpty and count is 0`` () =
    let ini = Ini.empty

    Ini.isEmpty ini |> should be True
    Ini.count ini |> should equal 0

// read

[<Fact>]
let ``fromReader reads from a stream successfully`` () =
    use reader = File.OpenText "input.ini"

    let ini =
        match Ini.fromReader reader with
        | Ok ini -> ini
        | Error _ -> Ini.empty

    Ini.isEmpty ini |> should be False
    Ini.count ini |> should equal 8

[<Fact>]
let ``fromFile reads from a file successfully`` () =
    let ini =
        match Ini.fromFile "input.ini" with
        | Ok ini -> ini
        | Error _ -> Ini.empty

    Ini.isEmpty ini |> should be False
    Ini.count ini |> should equal 8

[<Fact>]
let ``appendFromReader reads from a stream successfully`` () =
    use reader = File.OpenText "input.ini"

    let ini =
        match Ini.empty |> Ini.add "" "key" "value" |> Ini.appendFromReader reader with
        | Ok ini -> ini
        | Error _ -> Ini.empty

    Ini.isEmpty ini |> should be False
    Ini.count ini |> should equal 9


[<Fact>]
let ``appendFromFile reads from a file successfully`` () =
    let ini =
        match Ini.empty |> Ini.add "" "key" "value" |> Ini.appendFromFile "input.ini" with
        | Ok ini -> ini
        | Error _ -> Ini.empty

    Ini.isEmpty ini |> should be False
    Ini.count ini |> should equal 9

// parameters

[<Fact>]
let ``parameters returns triples of section/key/value`` () =
    let ini =
        match Ini.fromFile "input.ini" with
        | Ok ini -> ini
        | Error _ -> Ini.empty

    let list = Ini.parameters ini |> List.ofSeq
    let head = list |> List.head
    let last = list |> List.last
    
    list.Length |> should equal 8
    head |> should equal ("", "g-key", "g-value")
    last |> should equal ("foo.bar", "g-key", "g-f-b-value")

[<Fact>]
let ``sections returns section names`` () =
    let ini =
        match Ini.fromFile "input.ini" with
        | Ok ini -> ini
        | Error _ -> Ini.empty

    let list = Ini.sections ini |> List.ofSeq
    let head = list |> List.head
    let last = list |> List.last
    
    list.Length |> should equal 4
    head |> should equal ""
    last |> should equal "foo.bar"

[<Fact>]
let ``keys returns keys of a given section`` () =
    let ini =
        match Ini.fromFile "input.ini" with
        | Ok ini -> ini
        | Error _ -> Ini.empty

    let list = Ini.keys "foo.bar" ini |> List.ofSeq
    let head = list |> List.head
    let last = list |> List.last
    
    list.Length |> should equal 3
    head |> should equal "a-key"
    last |> should equal "g-key"

[<Fact>]
let ``values returns values of a given section`` () =
    let ini =
        match Ini.fromFile "input.ini" with
        | Ok ini -> ini
        | Error _ -> Ini.empty

    let list = Ini.values "foo.bar" ini |> List.ofSeq
    let head = list |> List.head
    let last = list |> List.last
    
    list.Length |> should equal 3
    head |> should equal "a-value"
    last |> should equal "g-f-b-value"

[<Fact>]
let ``section returns a key/value ini`` () =
    let ini =
        match Ini.fromFile "input.ini" with
        | Ok ini -> ini
        | Error _ -> Ini.empty

    let ini = Ini.section "foo.bar" ini
    let head = ini |> Map.find "a-key"
    let last = ini |> Map.find "g-key"
    
    ini.Count |> should equal 3
    head |> should equal "a-value"
    last |> should equal "g-f-b-value"

// find

[<Fact>]
let ``if exist, findNested returns values from parent section`` () =
    let ini =
        match Ini.fromFile "input.ini" with
        | Ok ini -> ini
        | Error _ -> Ini.empty

    let a_value =
        match ini |> Ini.findNested "foo.bar" "a-key" with
        | Some v -> v
        | None -> "none"

    let f_value =
        match ini |> Ini.findNested "foo.bar" "f-key" with
        | Some v -> v
        | None -> "none"

    let o_value =
        match ini |> Ini.findNested "foo.bar" "o-key" with
        | Some v -> v
        | None -> "none"

    a_value |> should equal "a-value"
    f_value |> should equal "f-b-value"
    o_value |> should equal "o-value"

[<Fact>]
let ``if exists, find returns some value`` () =
    let ini =
        match Ini.fromFile "input.ini" with
        | Ok ini -> ini
        | Error _ -> Ini.empty

    let value =
        match ini |> Ini.find "foo.bar" "a-key" with
        | Some v -> v
        | None -> "none"

    value |> should equal "a-value"

[<Fact>]
let ``if don't exist, find returns none`` () =
    let ini =
        match Ini.fromFile "input.ini" with
        | Ok ini -> ini
        | Error _ -> Ini.empty

    let value =
        match ini |> Ini.find "foo.bar" "b-key" with
        | Some v -> v
        | None -> "none"

    value |> should equal "none"

// add

[<Fact>]
let ``if exists, add changes the value successfully`` () =
    let ini =
        match Ini.fromFile "input.ini" with
        | Ok ini -> ini |> Ini.add "" "g-key" "a-value"
        | Error _ -> Ini.empty

    let value =
        match ini |> Ini.find "" "g-key" with
        | Some v -> v
        | None -> "none"

    Ini.count ini |> should equal 8
    value |> should equal "a-value"

[<Fact>]
let ``if don't exist, add adds new value successfully`` () =
    let ini =
        match Ini.fromFile "input.ini" with
        | Ok ini -> ini |> Ini.add "" "a-key" "a-value"
        | Error _ -> Ini.empty

    let value =
        match ini |> Ini.find "" "a-key" with
        | Some v -> v
        | None -> "none"

    Ini.count ini |> should equal 9
    value |> should equal "a-value"

// change

[<Fact>]
let ``if exists, change changes the value successfully`` () =
    let ini =
        match Ini.fromFile "input.ini" with
        | Ok ini ->
            ini
            |> Ini.change "" "g-key" (fun x ->
                match x with
                | Some v -> Some <| v.Replace('g', 'a')
                | None -> Some "some")
        | Error _ -> Ini.empty

    let value =
        match ini |> Ini.find "" "g-key" with
        | Some v -> v
        | None -> "none"

    Ini.count ini |> should equal 8
    value |> should equal "a-value"

[<Fact>]
let ``if don't exist, change adds new value successfully`` () =
    let ini =
        match Ini.fromFile "input.ini" with
        | Ok ini ->
            ini
            |> Ini.change "" "a-key" (fun x ->
                match x with
                | Some _ -> Some "some"
                | None -> Some "a-value")
        | Error _ -> Ini.empty

    let value =
        match ini |> Ini.find "" "a-key" with
        | Some v -> v
        | None -> "none"

    Ini.count ini |> should equal 9
    value |> should equal "a-value"

// remove

[<Fact>]
let ``if exists, remove removes the value successfully`` () =
    let ini =
        match Ini.fromFile "input.ini" with
        | Ok ini -> ini |> Ini.remove "" "g-key"
        | Error _ -> Ini.empty

    let value =
        match ini |> Ini.find "" "g-key" with
        | Some v -> v
        | None -> "none"

    Ini.count ini |> should equal 7
    value |> should equal "none"

[<Fact>]
let ``if don't exist, remove adds new value successfully`` () =
    let ini =
        match Ini.fromFile "input.ini" with
        | Ok ini -> ini |> Ini.remove "" "a-key"
        | Error _ -> Ini.empty

    let value =
        match ini |> Ini.find "" "a-key" with
        | Some v -> v
        | None -> "none"

    Ini.count ini |> should equal 8
    value |> should equal "none"

// global

[<Fact>]
let ``global section find/add/change/remove tests`` () =
    let ini = Ini.empty |> Ini.addGlobal "key" "value"
    
    let value = match ini |> Ini.findGlobal "key" with | Some value -> value | None -> "none"
    
    value |> should equal "value"
    
    let ini = ini |> Ini.addGlobal "key" "replaced"
    
    let value = match ini |> Ini.findGlobal "key" with | Some value -> value | None -> "none"
    
    value |> should equal "replaced"
    
    let ini = ini |> Ini.changeGlobal "key" (fun _ -> Some "changed")
    
    let value = match ini |> Ini.findGlobal "key" with | Some value -> value | None -> "none"
    
    value |> should equal "changed"
    
    let ini = ini |> Ini.removeGlobal "key"
    
    let value = match ini |> Ini.findGlobal "key" with | Some value -> value | None -> "none"
    
    value |> should equal "none"

// write

[<Fact>]
let ``toFile writes to a file successfully`` () =
    let temp = Path.GetTempFileName()

    match Ini.fromFile "input.ini" with
    | Ok ini -> ini |> Ini.toFile temp |> ignore
    | Error _ -> Assert.Fail()

    let output = File.ReadAllText "output.ini"
    let result = File.ReadAllText temp

    File.Delete temp

    result |> should equal output

[<Fact>]
let ``toWriter writes to a stream successfully`` () =
    let temp = Path.GetTempFileName()
    use writer = File.CreateText temp

    match Ini.fromFile "input.ini" with
    | Ok ini -> ini |> Ini.toWriter writer |> ignore
    | Error _ -> Assert.Fail()

    writer.Close()

    let output = File.ReadAllText "output.ini"
    let result = File.ReadAllText temp

    File.Delete temp

    result |> should equal output

[<Fact>]
let ``toFilePretty writes to a file successfully`` () =
    let temp = Path.GetTempFileName()

    match Ini.fromFile "f-input.ini" with
    | Ok ini -> ini |> Ini.toFilePretty temp |> ignore
    | Error _ -> Assert.Fail()

    let output = File.ReadAllText "f-output.ini"
    let result = File.ReadAllText temp

    File.Delete temp

    result |> should equal output

[<Fact>]
let ``toFilePretty writes to a file successfully 2`` () =
    let temp = Path.GetTempFileName()

    match Ini.fromFile "g-input.ini" with
    | Ok ini -> ini |> Ini.toFilePretty temp |> ignore
    | Error _ -> Assert.Fail()

    let output = File.ReadAllText "g-output.ini"
    let result = File.ReadAllText temp

    File.Delete temp

    result |> should equal output

[<Fact>]
let ``toString returns simple string`` () =
    let result =
        match Ini.fromFile "input.ini" with
        | Ok ini -> ini |> Ini.toString
        | Error _ -> "epic fail"

    let output = File.ReadAllText "output.ini"

    result |> should equal (output.TrimEnd())

[<Fact>]
let ``toStringPretty returns pretty string`` () =
    let result =
        match Ini.fromFile "f-input.ini" with
        | Ok ini -> ini |> Ini.toStringPretty
        | Error _ -> "epic fail"

    let output = File.ReadAllText "f-output.ini"

    result |> should equal (output.TrimEnd())
