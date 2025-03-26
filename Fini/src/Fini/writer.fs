module internal Writer

open System.IO
open Types

let formatSection (scn: Section) : string = $"[{scn}]"
let formatParameter (par: Parameter) : string = $"{fst par}={snd par}"

let parameters (map: MParameter list) : Parameter list = map |> List.map parameter
let lines (map: MParameter list) : string list = map |> List.head |> section |> formatSection |> List.singleton

let cleanUp (lines: string list) : string list =
    let rec loop lines acc =
        match lines with
        | [] -> acc
        | "[]" :: tail -> loop tail acc
        | head :: tail -> loop tail (head :: acc)

    loop lines List.empty

let ofSection (map: MParameter list) : string list =
    let rec loop parameters acc =
        match parameters with
        | [] -> acc
        | head :: tail -> loop tail ((formatParameter head) :: acc)

    loop (parameters map) (lines map) |> cleanUp

let toLines (map: Map<MKey, Value>) : string list =
    map
    |> Map.toList
    |> List.groupBy section
    |> List.map snd
    |> List.collect ofSection

let toWriter (map: Map<MKey, Value>) (writer: TextWriter) : Result<unit, string> =
    map
    |> toLines
    |> List.map (IO.TextWriter.writeLine writer)
    |> Result.combine
    |> Result.bind (fun _ -> IO.TextWriter.flush writer)

let toFile (map: Map<MKey, Value>) (path: string) : Result<unit, string> =
    path
    |> IO.File.createText
    |> Result.bind (fun writer ->
        use writer = writer
        toWriter map writer)
