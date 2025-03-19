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
    | Empty
    | Section of string
    | Parameter of (string * string)

let removeComment (input: string) : string =
    match input with
    | ParseRegex CommentRegex [ head; _; _ ] -> head
    | _ -> input

let (|ParseEmptySpace|_|) (input: string) : unit option =
    match input with
    | ParseRegex EmptySpaceRegex _ -> Some()
    | _ -> None

let (|ParseSection|_|) (input: string) : string option =
    match input with
    | ParseRegex SectionRegex [ _; name; _ ] -> Some name
    | _ -> None

let (|ParseParameter|_|) (input: string) : (string * string) option =
    match input with
    | ParseRegex ParameterRegex [ _; key; _; value; _ ] -> Some(key, value)
    | _ -> None

let (|ParseLine|_|) (input: string) : Line option =
    match removeComment input with
    | ParseEmptySpace _ -> Some Empty
    | ParseSection section -> Some(Section section)
    | ParseParameter parameter -> Some(Parameter parameter)
    | _ -> None

let parseLine (index: int, text: string) : Result<Line, string> =
    match text with
    | ParseLine line -> Ok line
    | _ -> Error $"Cannot parse line {index + 1}: {text}."

let parseLines (lines: string seq) : Result<Line list, string> =
    let lines =
        lines
        |> Seq.indexed
        |> Seq.map parseLine
        |> Seq.takeUntil _.IsError
        |> List.ofSeq
        |> List.rev

    let rec loop (lines: Result<Line, string> list) (acc: Line list) : Result<Line list, string> =
        match lines with
        | [] -> Ok acc
        | Error err :: _ -> Error err
        | Ok line :: tail -> loop tail (line :: acc)

    loop lines List.Empty
