namespace Fini

open System.IO
open Fini

module internal Map =

    // parameters

    let private sec (prm: (string * string) * string) : string = prm |> fst |> fst
    let private key (prm: (string * string) * string) : string = prm |> fst |> snd
    let private vlu (prm: (string * string) * string) : string = prm |> snd
    let private kvl (prm: (string * string) * string) : string * string = key prm, vlu prm

    let parameters (map: Map<string * string, string>) : (string * string * string) seq =
        map |> Seq.map (fun prm -> fst prm.Key, snd prm.Key, prm.Value)

    let sections (map: Map<string * string, string>) : string seq =
        map |> parameters |> Seq.map (fun (s, _ ,_) -> s) |> Seq.distinct

    let keys (section: string) (map: Map<string * string, string>) : string seq =
        map |> parameters |> Seq.filter (fun (s, _ ,_) -> s = section) |> Seq.map (fun (_, k ,_) -> k)

    let values (section: string) (map: Map<string * string, string>) : string seq =
        map |> parameters |> Seq.filter (fun (s, _ ,_) -> s = section) |> Seq.map (fun (_, _ ,v) -> v)

    let section(section: string) (map: Map<string * string, string>) : Map<string, string> =
        Seq.zip (map |> keys section) (map |> values section) |> Seq.toList |> Map.ofList

    // read

    let rec private append (lines: Line list) (section: string) (map: Map<string * string, string>) : Result<Map<string * string, string>, string> =
        match lines with
        | [] -> map |> Ok
        | Empty :: tail -> append tail section map
        | Comment _ :: tail -> append tail section map
        | Section section :: tail -> append tail section map
        | Parameter (key, value) :: tail -> append tail section (map.Add((section, key), value))

    let appendFromReader (reader: TextReader) (map: Map<string * string, string>) : Result<Map<string * string, string>, string> =
        reader
        |> IO.TextReader.readLines
        |> Seq.indexed
        |> Seq.map Line.fromResult
        |> Result.collect
        |> Result.bind (fun lines -> append lines "" map)

    let appendFromFile (path: string) (map: Map<string * string, string>) : Result<Map<string * string, string>, string> =
        path
        |> IO.File.openText
        |> Result.bind (fun reader ->
            use reader = reader
            appendFromReader reader map)

    // write

    let private formatSection (p: (string * string) * string) : string = p |> sec |> (fun section -> $"[{section}]")
    let private formatParameter (p: (string * string) * string) : string = p |> kvl |> (fun parameter -> $"{fst parameter}={snd parameter}")

    let private toLines (map: Map<string * string, string>) : string seq =
        let rec loop (map: ((string * string) * string) list) (section: string) (acc: string list) =
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

    let toWriter (writer: TextWriter) (map: Map<string * string, string>) : Result<unit, string> =
        map
        |> toLines
        |> Seq.map (IO.TextWriter.writeLine writer)
        |> Result.collectUnit
        |> Result.bind (fun _ -> IO.TextWriter.flush writer)

    let toFile (path: string) (map: Map<string * string, string>) : Result<unit, string> =
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

    let findNested (section: string) (key: string) (map: Map<string * string, string>) : string option =
        let rec loop (subsections: string list) (key: string) (map: Map<string * string, string>) =
            match subsections with
            | [] -> None
            | head :: tail ->
                match Map.tryFind (head, key) map with
                | Some value -> Some value
                | None -> loop tail key map
        loop (subsections section) key map
