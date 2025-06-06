namespace Fini

open Microsoft.FSharp.Collections
open System.Text.RegularExpressions

type internal Line =
    | Section of string
    | Parameter of string * string
    | Comment of string
    | Empty

module internal Line =
    let SectionRegex: Regex = Regex(@"^\s*\[\s*([^\]\s]+)\s*\]\s*$", RegexOptions.Compiled)

    let ParameterRegex: Regex = Regex(@"^\s*(\S+?)\s*=\s*(.*?)\s*$", RegexOptions.Compiled)

    let CommentRegex: Regex = Regex(@"^\s*#\s*(.*)", RegexOptions.Compiled)

    let EmptySpaceRegex: Regex = Regex(@"^\s*$", RegexOptions.Compiled)

    let (|ParseRegex|_|) (regex: Regex) (input: string) : string list voption =
        match regex.Match(input) with
        | m when m.Success -> List.tail [ for g in m.Groups -> g.Value ] |> ValueSome
        | _ -> ValueNone

    let (|ParseSection|_|) (text: string) : string voption =
        match text with
        | ParseRegex SectionRegex [ name ] -> name |> ValueSome
        | _ -> ValueNone

    let (|ParseParameter|_|) (text: string) : (string * string) voption =
        match text with
        | ParseRegex ParameterRegex [ key; value ] -> (key, value) |> ValueSome
        | _ -> ValueNone

    let (|ParseComment|_|) (text: string) : string voption =
        match text with
        | ParseRegex CommentRegex [ comment ] -> comment |> ValueSome
        | _ -> ValueNone

    let (|ParseEmptySpace|_|) (text: string) : string voption =
        match text with
        | ParseRegex EmptySpaceRegex _ -> "" |> ValueSome
        | _ -> ValueNone

    let (|ParseLine|_|) (text: string) : Line voption =
        match text with
        | ParseSection section -> section |> Section |> ValueSome
        | ParseParameter parameter -> parameter |> Parameter |> ValueSome
        | ParseComment comment -> comment |> Comment |> ValueSome
        | ParseEmptySpace _ -> Empty |> ValueSome
        | _ -> ValueNone

    let parseLine (text: string) : Result<Line, string> =
        match text with
        | ParseLine line -> Ok line
        | _ -> Error $"Cannot parse line: {text}."

    let inline fromResult (result: Result<string, string>) : Result<Line, string> = Result.bind parseLine result
