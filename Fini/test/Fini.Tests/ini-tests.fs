module Ini.Tests

open System.IO
open Fini
open Xunit

// empty

[<Fact>]
let ``if empty, isEmpty and count is 0`` () =
    let ini = Ini.empty

    Assert.True(Ini.isEmpty ini)
    Assert.Equal(0, Ini.count ini)

// read

[<Fact>]
let ``fromReader reads from a stream successfully`` () =
    use reader = File.OpenText "input.ini"

    let ini =
        match Ini.fromReader reader with
        | Ok ini -> ini
        | Error _ -> Ini.empty

    Assert.False(Ini.isEmpty ini)
    Assert.Equal(8, Ini.count ini)

[<Fact>]
let ``fromFile reads from a file successfully`` () =
    let ini =
        match Ini.fromFile "input.ini" with
        | Ok ini -> ini
        | Error _ -> Ini.empty

    Assert.False(Ini.isEmpty ini)
    Assert.Equal(8, Ini.count ini)

[<Fact>]
let ``appendFromReader reads from a stream successfully`` () =
    use reader = File.OpenText "input.ini"

    let ini =
        match Ini.empty |> Ini.add "" "key" "value" |> Ini.appendFromReader reader with
        | Ok ini -> ini
        | Error _ -> Ini.empty

    Assert.False(Ini.isEmpty ini)
    Assert.Equal(9, Ini.count ini)


[<Fact>]
let ``appendFromFile reads from a file successfully`` () =
    let ini =
        match Ini.empty |> Ini.add "" "key" "value" |> Ini.appendFromFile "input.ini" with
        | Ok ini -> ini
        | Error _ -> Ini.empty

    Assert.False(Ini.isEmpty ini)
    Assert.Equal(9, Ini.count ini)

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

    Assert.Equal(8, list.Length)
    Assert.Equal(("", "g-key", "g-value"), head)
    Assert.Equal(("foo.bar", "g-key", "g-f-b-value"), last)

[<Fact>]
let ``sections returns section names`` () =
    let ini =
        match Ini.fromFile "input.ini" with
        | Ok ini -> ini
        | Error _ -> Ini.empty

    let list = Ini.sections ini |> List.ofSeq
    let head = list |> List.head
    let last = list |> List.last

    Assert.Equal(4, list.Length)
    Assert.Equal("", head)
    Assert.Equal("foo.bar", last)

[<Fact>]
let ``keys returns keys of a given section`` () =
    let ini =
        match Ini.fromFile "input.ini" with
        | Ok ini -> ini
        | Error _ -> Ini.empty

    let list = Ini.keys "foo.bar" ini |> List.ofSeq
    let head = list |> List.head
    let last = list |> List.last

    Assert.Equal(3, list.Length)
    Assert.Equal("a-key", head)
    Assert.Equal("g-key", last)

[<Fact>]
let ``values returns values of a given section`` () =
    let ini =
        match Ini.fromFile "input.ini" with
        | Ok ini -> ini
        | Error _ -> Ini.empty

    let list = Ini.values "foo.bar" ini |> List.ofSeq
    let head = list |> List.head
    let last = list |> List.last

    Assert.Equal(3, list.Length)
    Assert.Equal("a-value", head)
    Assert.Equal("g-f-b-value", last)

[<Fact>]
let ``section returns a key/value ini`` () =
    let ini =
        match Ini.fromFile "input.ini" with
        | Ok ini -> ini
        | Error _ -> Ini.empty

    let ini = Ini.section "foo.bar" ini
    let head = ini |> Map.find "a-key"
    let last = ini |> Map.find "g-key"

    Assert.Equal(3, ini.Count)
    Assert.Equal("a-value", head)
    Assert.Equal("g-f-b-value", last)

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

    Assert.Equal("a-value", a_value)
    Assert.Equal("f-b-value", f_value)
    Assert.Equal("o-value", o_value)

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

    Assert.Equal("a-value", value)

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

    Assert.Equal("none", value)

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

    Assert.Equal(8, Ini.count ini)
    Assert.Equal("a-value", value)

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

    Assert.Equal(9, Ini.count ini)
    Assert.Equal("a-value", value)

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

    Assert.Equal(8, Ini.count ini)
    Assert.Equal("a-value", value)

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

    Assert.Equal(9, Ini.count ini)
    Assert.Equal("a-value", value)

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

    Assert.Equal(7, Ini.count ini)
    Assert.Equal("none", value)

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

    Assert.Equal(8, Ini.count ini)
    Assert.Equal("none", value)

// global

[<Fact>]
let ``global section find/add/change/remove tests`` () =
    let ini = Ini.empty |> Ini.addGlobal "key" "value"
    
    let value = match ini |> Ini.findGlobal "key" with | Some value -> value | None -> "none"
    
    Assert.Equal("value", value)
    
    let ini = ini |> Ini.addGlobal "key" "replaced"
    
    let value = match ini |> Ini.findGlobal "key" with | Some value -> value | None -> "none"
    
    Assert.Equal("replaced", value)
    
    let ini = ini |> Ini.changeGlobal "key" (fun _ -> Some "changed")
    
    let value = match ini |> Ini.findGlobal "key" with | Some value -> value | None -> "none"
    
    Assert.Equal("changed", value)
    
    let ini = ini |> Ini.removeGlobal "key"
    
    let value = match ini |> Ini.findGlobal "key" with | Some value -> value | None -> "none"
    
    Assert.Equal("none", value)

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

    Assert.Equal(output, result)

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

    Assert.Equal(output, result)

[<Fact>]
let ``toFilePretty writes to a file successfully`` () =
    let temp = Path.GetTempFileName()

    match Ini.fromFile "f-input.ini" with
    | Ok ini -> ini |> Ini.toFilePretty temp |> ignore
    | Error _ -> Assert.Fail()

    let output = File.ReadAllText "f-output.ini"
    let result = File.ReadAllText temp

    File.Delete temp

    Assert.Equal(output, result)
