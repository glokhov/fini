module Readme.Tests

open System.Diagnostics
open Fini
open Xunit

[<Fact>]
let ``tests for getting started`` () =

    // read from file

    let ini =
        match Ini.fromFile "readme.ini" with
        | Ok ini -> ini
        | Error _ -> Ini.empty

    // find global

    let value =
        match ini |> Ini.findGlobal "global_key" with
        | Some value -> value
        | None -> "none"

    let equal = value = "global_value"

    Debug.Assert equal

    let value =
        match ini |> Ini.findGlobal "one_key" with
        | Some value -> value
        | None -> "none"
    
    let equal = value = "none"
    
    Debug.Assert equal

    // find

    let value =
        match ini |> Ini.find "one" "one_key" with
        | Some value -> value
        | None -> "none"

    let equal = value = "one_value"

    Debug.Assert equal

    let value =
        match ini |> Ini.find "one" "two_key" with
        | Some value -> value
        | None -> "none"

    let equal = value = "none"

    Debug.Assert equal

    // find nested

    let value =
        match ini |> Ini.findNested "one.two" "one_key" with
        | Some value -> value
        | None -> "none"

    let equal = value = "one_value"

    Debug.Assert equal

    let value =
        match ini |> Ini.findNested "one.two" "two_key" with
        | Some value -> value
        | None -> "none"

    let equal = value = "two_value"

    Debug.Assert equal

    // add

    let ini = ini |> Ini.add "three" "three_key" "replaced_three_value"

    let value = ini |> Ini.find "three" "three_key"

    let equal = (value = Some "replaced_three_value")

    Debug.Assert equal

    let ini = ini |> Ini.add "four" "four_key" "four_value"

    let value = ini |> Ini.find "four" "four_key"

    let equal = (value = Some "four_value")

    Debug.Assert equal

    // change

    // remove

    let ini = ini |> Ini.remove "one" "one_key"

    let value = ini |> Ini.find "one" "one_key"

    let equal = (value = None)

    Debug.Assert equal

    // write to file

    ini |> Ini.toFile "readme_final.ini" |> ignore
