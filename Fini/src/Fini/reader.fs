module internal Reader

open Types

let fromParserLines (lines: Line list) : Result<Map<MKey, Value>, string> =
    let rec loop lines section (map: Map<MKey, Value>) =
        match lines with
        | [] -> map |> Ok
        | Section section :: tail -> loop tail section map
        | Parameter parameter :: tail -> loop tail section (map.Add((section, fst parameter), (snd parameter)))
    loop lines "" Map.empty

let fromLines (contents: string seq) : Result<Map<MKey, Value>, string> = contents |> Parser.parse |> Result.bind fromParserLines

let fromString (contents: string) : Result<Map<MKey, Value>, string> = contents |> Lines.split |> fromLines