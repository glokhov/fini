module internal Parser

open Microsoft.FSharp.Collections
open System.Text.RegularExpressions
open TakeUntil
open Types

type Line =
    | Section of Section
    | Parameter of Parameter

let SectionRegex: Regex = Regex(@"^(\s*\[\s*)([^\]\s]+)(\s*\]\s*)$", RegexOptions.Compiled)

let ParameterRegex: Regex = Regex(@"^(\s*)(\S+?)(\s*=\s*)(.*?)(\s*)$", RegexOptions.Compiled)

let CommentRegex: Regex = Regex(@"(.*?)(\s*#\s*)(.*)", RegexOptions.Compiled)

let EmptySpaceRegex: Regex = Regex(@"^\s*$", RegexOptions.Compiled)

let (|ParseRegex|_|) (regex: Regex) (input: string) : string list option =
    match regex.Match(input) with
    | m when m.Success -> Some(List.tail [ for g in m.Groups -> g.Value ])
    | _ -> None

let (|ParseSection|_|) (text: string) : string option =
    match text with
    | ParseRegex SectionRegex [ _; name; _ ] -> Some name
    | _ -> None

let (|ParseParameter|_|) (text: string) : (string * string) option =
    match text with
    | ParseRegex ParameterRegex [ _; key; _; value; _ ] -> Some <| (key, value)
    | _ -> None

let (|ParseLine|_|) (text: string) : Line option =
    match text with
    | ParseSection section -> Some <| Section section
    | ParseParameter parameter -> Some <| Parameter parameter
    | _ -> None

let parseLine (index: int, text: string) : Result<Line, string> =
    match text with
    | ParseLine line -> Ok line
    | _ -> Error $"Cannot parse line {index + 1}: {text}."

let removeComment (index: int, text: string) : int * string =
    match text with
    | ParseRegex CommentRegex [ text; _; _ ] -> (index, text)
    | _ -> (index, text)

let isNotWhiteSpace (_: int, text: string) : bool =
    match text with
    | ParseRegex EmptySpaceRegex _ -> false
    | _ -> true

let parse (lines: string seq) : Result<Line list, string> =
    lines
    |> Seq.indexed
    |> Seq.map removeComment
    |> Seq.filter isNotWhiteSpace
    |> Seq.map parseLine
    |> Seq.takeUntil _.IsError
    |> Seq.toList
    |> List.rev
    |> Result.combine
