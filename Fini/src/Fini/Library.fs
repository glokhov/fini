module Fini.Library

open System
open System.IO
open System.Text.RegularExpressions
open CSharp
open Microsoft.FSharp.Collections

let readAllLines: string -> Result<string array, Exception> = invoke File.ReadAllLines

type Line =
    | Empty
    | Section of string
    | Param of (string * string)
    | Err of string

type Parameters = Map<string, string>
type Sections = Map<string, Parameters>

type Ini(sections: Sections) =
    new() = Ini(Map.empty)

let (|ParseRegex|_|) (regex: string) (input: string) : string list option =
    let m = Regex(regex).Match(input)
    if m.Success then
        Some(List.tail [ for x in m.Groups -> x.Value ])
    else
        None

let parseParameter (text: string) : Line option =
    match text with
    | ParseRegex @"(^\s*)(.*?)(\s*=\s*)(.*?)(\s*$)" [ _; key; _; value; _ ] -> Param(key, value) |> Some
    | _ -> None

let parseSection (text: string) : Line option =
    match text with
    | ParseRegex @"(^\s*\[\s*)([^\]\s]+)(\s*\]\s*$)" [ _; name; _ ] -> Section(name) |> Some
    | _ -> None

let removeComment (text: string) : string =
    match text with
    | ParseRegex @"(^.*?)(#)(.*$)" [ head; _; _ ] -> head
    | _ -> text

let isWhiteSpace (text: string) : bool =
    text |> Seq.toList |> Seq.forall Char.IsWhiteSpace

let parseLine (line: int, text: string) : Line =
    let noCommentText = removeComment text
    match parseParameter noCommentText with
    | Some parameter -> parameter
    | None ->
        match parseSection noCommentText with
        | Some section -> section
        | None ->
            match isWhiteSpace noCommentText with
            | true -> Empty
            | _ -> Err($"Cannot parse line {line + 1}: {text}.")

let addParameter (parameter: string * string) (sectionName: string) (sections: Sections) : Sections =
    let parameters = sections[sectionName].Add parameter
    let section = (sectionName, parameters)
    let sections = sections.Add section
    sections

let addSection (sectionName: string) (sections: Sections) : Sections =
    let section = (sectionName, Map.empty)
    let sections = sections.Add section
    sections

let rec parseLinesLoop (lines: Line list) (sectionName: string) (sections: Sections) : Result<Ini, string> =
    match lines with
    | [] -> Ini(sections) |> Ok
    | Empty :: tail -> parseLinesLoop tail sectionName sections
    | Section section :: tail -> parseLinesLoop tail section (addSection section sections)
    | Param param :: tail -> parseLinesLoop tail sectionName (addParameter param sectionName sections)
    | Err error :: _ -> Error error

let parseLines (lines: Line list) : Result<Ini, string> =
    parseLinesLoop lines "" <| Sections [ ("", Map.empty) ]

let parse (configFile: string) : Result<Ini, string> =
    match readAllLines configFile with
    | Ok lines -> lines |> List.ofArray |> List.indexed |> List.map parseLine |> parseLines
    | Error err -> Error err.Message
