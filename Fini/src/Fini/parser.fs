module internal Parser

open Microsoft.FSharp.Collections
open Regex
open TakeUntil

[<Literal>]
let CommentRegex: string = @"(.*?)(\s*#\s*)(.*)"

[<Literal>]
let EmptySpaceRegex: string = @"^\s*$"

[<Literal>]
let SectionRegex: string = @"^(\s*\[\s*)([^\]\s]+)(\s*\]\s*)$"

[<Literal>]
let ParameterRegex: string = @"^(\s*)(\S+?)(\s*=\s*)(.*?)(\s*)$"

type Line =
    | Section of string
    | Parameter of (string * string)

let (|ParseSection|_|) (txt: string) : string option =
    match txt with
    | ParseRegex SectionRegex [ _; name; _ ] -> Some name
    | _ -> None

let (|ParseParameter|_|) (txt: string) : (string * string) option =
    match txt with
    | ParseRegex ParameterRegex [ _; key; _; value; _ ] -> Some <| (key, value)
    | _ -> None

let (|ParseLine|_|) (txt: string) : Line option =
    match txt with
    | ParseSection section -> Some <| Section section
    | ParseParameter parameter -> Some <| Parameter parameter
    | _ -> None

let parseLine (idx: int, txt: string) : Result<Line, string> =
    match txt with
    | ParseLine line -> Ok line
    | _ -> Error $"Cannot parse line {idx + 1}: {txt}."

let removeComment (idx: int, txt: string) : int * string =
    match txt with
    | ParseRegex CommentRegex [ txt; _; _ ] -> (idx, txt)
    | _ -> (idx, txt)

let isNotWhiteSpace (_: int, txt: string) : bool =
    match txt with
    | ParseRegex EmptySpaceRegex _ -> false
    | _ -> true

let parse (lines: string seq) : Result<Line list, string> =
    lines |> Seq.indexed |> Seq.map removeComment |> Seq.filter isNotWhiteSpace |> Seq.map parseLine |> Seq.takeUntil _.IsError |> List.ofSeq |> List.rev |> Result.combine
