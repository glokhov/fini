module internal IniMap

open System.IO
open Types

// reader

let rec private append (lines: Line list) (section: Section) (map: Map<MKey, Value>) : Result<Map<MKey, Value>, string> =
    match lines with
    | [] -> map |> Ok
    | Empty :: tail -> append tail section map
    | Comment _ :: tail -> append tail section map
    | Section section :: tail -> append tail section map
    | Parameter parameter :: tail -> append tail section (map.Add((section, fst parameter), (snd parameter)))

let appendFromReader (reader: TextReader) (map: Map<MKey, Value>) : Result<Map<MKey, Value>, string> =
    reader
    |> IO.TextReader.readLines
    |> Seq.indexed
    |> Seq.map Parser.fromLine
    |> Result.collect
    |> Result.bind (fun lines -> append lines "" map)

let appendFromFile (path: string) (map: Map<MKey, Value>) : Result<Map<MKey, Value>, string> =
    path
    |> IO.File.openText
    |> Result.bind (fun reader ->
        use reader = reader
        appendFromReader reader map)

// writer

let private toLines (map: Map<MKey, Value>) : string seq =
    let rec loop (map: MParameter list) (section: string) (acc: string list) =
        match map with
        | [] -> acc
        | head :: tail ->
            let sec = formatSection head
            let par = formatParameter head
            if sec = section then
                loop tail section (par :: acc)
            else
                loop tail sec (par :: sec :: acc)
    loop (Map.toList map) "[]" List.empty |> Seq.rev

let toWriter (writer: TextWriter) (map: Map<MKey, Value>) : Result<unit, string> =
    map
    |> toLines
    |> Seq.map (IO.TextWriter.writeLine writer)
    |> Result.collectUnit
    |> Result.bind (fun _ -> IO.TextWriter.flush writer)

let toFile (path: string) (map: Map<MKey, Value>) : Result<unit, string> =
    path
    |> IO.File.createText
    |> Result.bind (fun writer ->
        use writer = writer
        toWriter writer map)

// find

let rec private subsections (section: string) : string list =
    let rec loop (section: string) (acc: string list) =
        let index = section.LastIndexOf '.'
        if index < 0 then
            List.rev acc
        else
            let sub = section[.. index - 1]
            loop sub (sub :: acc)
    loop section (List.singleton section)

let findNested (section: string) (key: string) (map: Map<MKey, Value>) : string option =
    let rec loop (subsections: string list) (key: string) (map: Map<MKey, Value>) =
        match subsections with
        | [] -> None
        | head :: tail ->
            match Map.tryFind (head, key) map with
            | Some value -> Some value
            | None -> loop tail key map
    loop (subsections section) key map
