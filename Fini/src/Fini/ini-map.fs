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

let rec private subsections (section: string option) (acc: string list) : string list =
    match section with
    | None -> List.rev acc
    | Some sec ->
        let ind = sec.LastIndexOf '.'
        if ind < 0 then
            subsections None acc
            else
                let sub = sec[..ind-1]
                subsections (Some sub) (sub :: acc)

let rec private findInSubsections (section: string list) (key: string) (map: Map<MKey, Value>) : string option =
    match section with
    | [] -> None
    | head :: tail ->
        match map |> Map.tryFind(head, key) with
        | Some value -> Some value
        | None -> map |> findInSubsections tail key

let findNested (section: string) (key: string) (map: Map<MKey, Value>) : string option =
    map |> findInSubsections (subsections (Some section) (List.singleton section)) key
