module internal Parser

open Microsoft.FSharp.Collections
open System.Text.RegularExpressions
open Types

let SectionRegex: Regex = Regex(@"^(\s*\[\s*)([^\]\s]+)(\s*\]\s*)$", RegexOptions.Compiled)

let ParameterRegex: Regex = Regex(@"^(\s*)(\S+?)(\s*=\s*)(.*?)(\s*)$", RegexOptions.Compiled)

let CommentRegex: Regex = Regex(@"^(\s*#\s*)(.*)", RegexOptions.Compiled)

let EmptySpaceRegex: Regex = Regex(@"^\s*$", RegexOptions.Compiled)

let (|ParseRegex|_|) (regex: Regex) (input: string) : string list option =
    match regex.Match(input) with
    | m when m.Success -> List.tail [ for g in m.Groups -> g.Value ] |> Some
    | _ -> None

let (|ParseSection|_|) (text: string) : string option =
    match text with
    | ParseRegex SectionRegex [ _; name; _ ] -> name |> Some
    | _ -> None

let (|ParseParameter|_|) (text: string) : (string * string) option =
    match text with
    | ParseRegex ParameterRegex [ _; key; _; value; _ ] -> (key, value) |> Some
    | _ -> None

let (|ParseComment|_|) (text: string) : string option =
    match text with
    | ParseRegex CommentRegex [ _; comment ] -> comment |> Some
    | _ -> None

let (|ParseEmptySpace|_|) (text: string) : string option =
    match text with
    | ParseRegex EmptySpaceRegex [ space ] -> space |> Some
    | _ -> None

let (|ParseLine|_|) (text: string) : Line option =
    match text with
    | ParseSection section -> section |> Section |> Some
    | ParseParameter parameter -> parameter |> Parameter |> Some
    | ParseComment _ -> Empty |> Some
    | ParseEmptySpace _ -> Empty |> Some
    | _ -> None

let parseLine (index: int, text: string) : Result<Line, string> =
    match text with
    | ParseLine line -> Ok line
    | _ -> Error $"Cannot parse line {index + 1}: {text}."

let fromLine (index: int, result: Result<string, string>) : Result<Line, string> =
    result |> Result.bind (fun line -> parseLine (index, line))
