module internal Writer

open Types

let formatSection (scn: Section) : string = $"[{scn}]"

let formatParameter (par: Parameter) : string = $"{fst par}={snd par}"

let parameters (map: MParameter seq) : Parameter list =  map |> Seq.map parameter |> Seq.toList

let lines (map: MParameter seq) : string list = map |> Seq.head |> section |> formatSection |> List.singleton

let cleanUp (lines: string list) : string list =
    let rec loop lines acc =
        match lines with
        | [] -> acc
        | "[]" :: tail -> loop tail acc
        | head :: tail -> loop tail (head :: acc)
    loop lines List.empty

let writeSection (map: MParameter seq) : string seq =
    let rec loop parameters acc =
        match parameters with
        | [] -> acc
        | head :: tail -> loop tail ((formatParameter head) :: acc)
    loop (parameters map) (lines map) |> cleanUp |> Seq.ofList

let toLines (map: Map<MKey, Value>) : string seq = map |> Map.toSeq |> Seq.groupBy section |> Seq.map snd |> Seq.collect writeSection

let toString (map: Map<MKey, Value>) : string = map |> toLines |> Lines.join
