module Map.Tests

open System.IO
open Fini.Map
open Xunit

// read and write

[<Fact>]
let ``appendFromFile then toFile`` () =
    let temp = Path.GetTempFileName()

    match empty |> appendFromFile "input.ini" with
    | Ok map -> map |> toFile temp |> ignore
    | Error _ -> Assert.Fail()

    let output = File.ReadAllText "output.ini"
    let result = File.ReadAllText temp

    File.Delete temp

    Assert.Equal(output, result)

// find

[<Fact>]
let ``findNested returns values from parent section`` () =
    let map =
        match empty |> appendFromFile "input.ini" with
        | Ok map -> map
        | Error _ -> empty

    let a =
        match map |> findNested "foo.bar" "a-key" with
        | Some v -> v
        | None -> ""

    let f =
        match map |> findNested "foo.bar" "f-key" with
        | Some v -> v
        | None -> ""

    let o =
        match map |> findNested "foo.bar" "o-key" with
        | Some v -> v
        | None -> ""

    Assert.Equal("a-value", a)
    Assert.Equal("f-b-value", f)
    Assert.Equal("o-value", o)

// parameters

[<Fact>]
let ``parameters returns triples of section/key/value`` () =
    let map =
        match empty |> appendFromFile "input.ini" with
        | Ok map -> map
        | Error _ -> empty
    
    let list = parameters map |> List.ofSeq
    let head = list |> List.head
    let last = list |> List.last
    
    Assert.Equal(8, list.Length)
    Assert.Equal(("", "g-key", "g-value"), head)
    Assert.Equal(("foo.bar", "g-key", "g-f-b-value"), last)

[<Fact>]
let ``sections returns section names`` () =
    let map =
        match empty |> appendFromFile "input.ini" with
        | Ok map -> map
        | Error _ -> empty
    
    let list = sections map |> List.ofSeq
    let head = list |> List.head
    let last = list |> List.last
    
    Assert.Equal(4, list.Length)
    Assert.Equal("", head)
    Assert.Equal("foo.bar", last)

[<Fact>]
let ``keys returns keys of a given section`` () =
    let map =
        match empty |> appendFromFile "input.ini" with
        | Ok map -> map
        | Error _ -> empty
    
    let list = keys "foo.bar" map |> List.ofSeq
    let head = list |> List.head
    let last = list |> List.last
    
    Assert.Equal(3, list.Length)
    Assert.Equal("a-key", head)
    Assert.Equal("g-key", last)

[<Fact>]
let ``values returns values of a given section`` () =
    let map =
        match empty |> appendFromFile "input.ini" with
        | Ok map -> map
        | Error _ -> empty
    
    let list = values "foo.bar" map |> List.ofSeq
    let head = list |> List.head
    let last = list |> List.last
    
    Assert.Equal(3, list.Length)
    Assert.Equal("a-value", head)
    Assert.Equal("g-f-b-value", last)

[<Fact>]
let ``section returns a key/value map`` () =
    let map =
        match empty |> appendFromFile "input.ini" with
        | Ok map -> map
        | Error _ -> empty
    
    let map = section "foo.bar" map
    let head = map |> find "a-key"
    let last = map |> find "g-key"
    
    Assert.Equal(3, map.Count)
    Assert.Equal("a-value", head)
    Assert.Equal("g-f-b-value", last)
