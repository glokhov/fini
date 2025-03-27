module internal IniMap

open System.IO
open Types
open TakeUntil

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
    |> Seq.takeUntil _.IsError
    |> Seq.toList
    |> Result.combine
    |> Result.bind (fun lines -> append lines "" map)

let appendFromFile (path: string) (map: Map<MKey, Value>) : Result<Map<MKey, Value>, string> =
    path
    |> IO.File.openText
    |> Result.bind (fun reader ->
        use reader = reader
        appendFromReader reader map)

// writer

let private getLines (map: Map<MKey, Value>) : string seq =
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
    loop (map |> Map.toList) "[]" List.empty |> List.rev |> Seq.ofList

let toWriter (writer: TextWriter) (map: Map<MKey, Value>) : Result<unit, string> =
    map
    |> getLines
    |> Seq.map (IO.TextWriter.writeLine writer)
    |> Seq.takeUntil _.IsError
    |> Seq.toList
    |> Result.combineUnit
    |> Result.bind (fun _ -> IO.TextWriter.flush writer)

let toFile (path: string) (map: Map<MKey, Value>) : Result<unit, string> =
    path
    |> IO.File.createText
    |> Result.bind (fun writer ->
        use writer = writer
        toWriter writer map)
