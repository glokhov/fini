module internal Reader

open Types

let rec private loop (lines: Line list) (section: Section) (map: Map<MKey, Value>) : Result<Map<MKey, Value>, string> =
    match lines with
    | [] -> map |> Ok
    | Section section :: tail -> loop tail section map
    | Parameter parameter :: tail -> loop tail section (map.Add((section, fst parameter), (snd parameter)))

let appendLines (map: Map<MKey, Value>) (contents: string seq) : Result<Map<MKey, Value>, string> = contents |> Parser.parse |> Result.bind (fun lines -> loop lines "" map)

let appendString (map: Map<MKey, Value>) (contents: string) : Result<Map<MKey, Value>, string> = contents |> Lines.split |> appendLines map

let fromLines (contents: string seq) : Result<Map<MKey, Value>, string> = contents |> appendLines Map.empty

let fromString (contents: string) : Result<Map<MKey, Value>, string> = contents |> appendString Map.empty
