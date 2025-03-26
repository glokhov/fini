module internal Reader

open System.IO
open Types
open TakeUntil

let rec private loop (lines: Line list) (section: Section) (map: Map<MKey, Value>) : Result<Map<MKey, Value>, string> =
    match lines with
    | [] -> map |> Ok
    | Empty :: tail -> loop tail section map
    | Section section :: tail -> loop tail section map
    | Parameter parameter :: tail -> loop tail section (map.Add((section, fst parameter), (snd parameter)))

let appendFromReader (map: Map<MKey, Value>) (reader: TextReader) : Result<Map<MKey, Value>, string> =
    use reader = reader
    reader |> IO.TextReader.readLines |> Seq.indexed |> Seq.map Parser.fromLine |> Seq.takeUntil _.IsError |> Seq.toList |> Result.combine |> Result.bind (fun lines -> loop lines "" map)
    
let appendFromFile (map: Map<MKey, Value>) (path: string) : Result<Map<MKey, Value>, string> =
    path |> IO.File.openText |> Result.bind (appendFromReader map)

let fromReader (reader: TextReader) : Result<Map<MKey, Value>, string> = reader |> appendFromReader Map.empty

let fromFile (path: string) : Result<Map<MKey, Value>, string> = path |> appendFromFile Map.empty
