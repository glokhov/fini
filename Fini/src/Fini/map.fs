namespace Fini

open System.Collections.Generic
open System.IO
open Fini

module internal Map =
    module private Private =
        let inline section (prm: (string * string) * string) : string = prm |> fst |> fst
        let inline key (prm: (string * string) * string) : string = prm |> fst |> snd
        let inline value (prm: (string * string) * string) : string = prm |> snd
        let inline keyValue (prm: (string * string) * string) : string * string = key prm, value prm

        let rec subsections (section: string) : string list =
            let rec loop (section: string) (acc: string list) =
                let index = section.LastIndexOf '.'
                if index < 0 then
                    List.rev acc
                else
                    let sub = section[.. index - 1]
                    loop sub (sub :: acc)
            loop section (List.singleton section)

        let rec append (lines: Line list) (section: string) (map: Map<string * string, string>) : Result<Map<string * string, string>, string> =
            match lines with
            | [] -> map |> Ok
            | Empty :: tail -> append tail section map
            | Comment _ :: tail -> append tail section map
            | Section section :: tail -> append tail section map
            | Parameter (key, value) :: tail -> append tail section (map.Add((section, key), value))

    module private Writer =
        let writeSection (sec: string) (writer: TextWriter) : Result<TextWriter, string> =
            writer
            |> IO.TextWriter.writeChar '['
            |> Result.bind (IO.TextWriter.writeString sec)
            |> Result.bind (IO.TextWriter.writeChar ']')
            |> Result.bind IO.TextWriter.writeEol

        let writeParameter (key: string) (value: string) (writer: TextWriter) : Result<TextWriter, string> =
            writer
            |> IO.TextWriter.writeString key
            |> Result.bind (IO.TextWriter.writeChar '=')
            |> Result.bind (IO.TextWriter.writeString value)
            |> Result.bind IO.TextWriter.writeEol

        let writeParameterPretty (key: string) (value: string) (keyLength: int) (writer: TextWriter) : Result<TextWriter, string> =
            writer
            |> IO.TextWriter.writeString(key.PadRight(keyLength))
            |> Result.bind (IO.TextWriter.writeString " = ")
            |> Result.bind (IO.TextWriter.writeString value)
            |> Result.bind IO.TextWriter.writeEol

        let writeLine (line: (string * string) * string) (section: string) (writer: TextWriter) : Result<string, string> =
            let current = Private.section line
            let key = Private.key line
            let value = Private.value line
            if(current <> section) then
                writer
                |> writeSection current
                |> Result.bind (writeParameter key value)
                |> Result.map (fun _ -> current)
            else
                writer
                |> writeParameter key value
                |> Result.map (fun _ -> current)

        let writeLinePretty (line: (string * string) * string) (section: string) (first: bool) (keyLength: string -> int) (writer: TextWriter) : Result<string, string> =
            let current = Private.section line
            let key = Private.key line
            let value = Private.value line
            let keyLength = keyLength current
            if(current <> section) then
                if first then Ok writer else IO.TextWriter.writeEol writer
                |> Result.bind (writeSection current)
                |> Result.bind IO.TextWriter.writeEol
                |> Result.bind (writeParameterPretty key value keyLength)
                |> Result.map (fun _ -> current)
            else
                writer
                |> writeParameterPretty key value keyLength
                |> Result.map (fun _ -> current)

        let toFile (path: string) (map: Map<string * string, string>) (toWriter: TextWriter -> Map<string * string, string> -> Result<unit, string>) : Result<unit, string> =
            path
            |> IO.File.createText
            |> Result.bind (fun writer ->
                use writer = writer
                toWriter writer map)

        let toString (map: Map<string * string, string>) (toWriter: TextWriter -> Map<string * string, string> -> Result<unit, string>) : string =
            use writer = new StringWriter()
            match toWriter writer map with
            | Ok _ -> writer.ToString().TrimEnd()
            | Error err -> err

    // of seq, list, array

    let ofList (l: (string * string * string) list) : Map<string * string, string> =
        List.fold (fun acc (s, k, v) -> Map.add (s, k) v acc) Map.empty l

    let ofArray (a: (string * string * string) array) : Map<string * string, string> =
        let mutable m = Map.empty
        for s, k, v in a do
            m <- Map.add (s, k) v m
        m

    let rec fromEnumerator (acc: Map<string * string, string>) (e: IEnumerator<string * string * string>) : Map<string * string, string> =
        if e.MoveNext() then
            let section, key, value = e.Current
            fromEnumerator (acc.Add((section, key), value)) e
        else
            acc

    let ofSeq (c: (string * string * string) seq) : Map<string * string, string> =
        match c with
        | :? ((string * string * string) list) as xs -> ofList xs
        | :? ((string * string * string) array) as xs -> ofArray xs
        | _ ->
            use e = c.GetEnumerator()
            fromEnumerator Map.empty e

    // to seq, list, array

    let toSeq (map: Map<string * string, string>) : (string * string * string) seq =
        map |> Seq.map (fun prm -> fst prm.Key, snd prm.Key, prm.Value)

    let toList (map: Map<string * string, string>) : (string * string * string) list =
        map |> toSeq |> Seq.toList

    let toArray (map: Map<string * string, string>) : (string * string * string) array =
        map |> toSeq |> Seq.toArray

    // sections, keys and values

    let sections (map: Map<string * string, string>) : string seq =
        map |> toSeq |> Seq.map (fun (s, _ ,_) -> s) |> Seq.distinct

    let keys (section: string) (map: Map<string * string, string>) : string seq =
        map |> toSeq |> Seq.filter (fun (s, _ ,_) -> s = section) |> Seq.map (fun (_, k ,_) -> k)

    let values (section: string) (map: Map<string * string, string>) : string seq =
        map |> toSeq |> Seq.filter (fun (s, _ ,_) -> s = section) |> Seq.map (fun (_, _ ,v) -> v)

    let section(section: string) (map: Map<string * string, string>) : Map<string, string> =
        Seq.zip (keys section map) (values section map) |> Map.ofSeq

    // read

    let appendFromReader (reader: TextReader) (map: Map<string * string, string>) : Result<Map<string * string, string>, string> =
        reader
        |> IO.TextReader.readLines
        |> Seq.map Line.fromResult
        |> Result.collect
        |> Result.bind (fun lines -> Private.append lines "" map)

    let appendFromFile (path: string) (map: Map<string * string, string>) : Result<Map<string * string, string>, string> =
        path
        |> IO.File.openText
        |> Result.bind (fun reader ->
            use reader = reader
            appendFromReader reader map)

    // write

    let toWriter (writer: TextWriter) (map: Map<string * string, string>) : Result<unit, string> =
        let rec loop (map: ((string * string) * string) list) (section: string) : Result<unit, string> =
            match map with
            | [] -> writer |> IO.TextWriter.flush
            | head :: tail -> writer |> Writer.writeLine head section |> Result.bind (loop tail) 
        loop (Map.toList map) ""

    let toWriterPretty (writer: TextWriter) (map: Map<string * string, string>) : Result<unit, string> =
        let maxKeyLength (section: string) : int =
            map
            |> keys section
            |> Seq.map _.Length
            |> Seq.max
        let rec loop (map: ((string * string) * string) list) (first: bool) (section: string) : Result<unit, string> =
            match map with
            | [] -> writer |> IO.TextWriter.flush
            | head :: tail -> writer |> Writer.writeLinePretty head section first maxKeyLength |> Result.bind (loop tail false) 
        loop (Map.toList map) true ""

    let inline toFile (path: string) (map: Map<string * string, string>) : Result<unit, string> = Writer.toFile path map toWriter
    let inline toFilePretty (path: string) (map: Map<string * string, string>) : Result<unit, string> = Writer.toFile path map toWriterPretty

    // to string

    let inline toString (map: Map<string * string, string>) : string = Writer.toString map toWriter
    let inline toStringPretty (map: Map<string * string, string>) : string = Writer.toString map toWriterPretty

    // find

    let findNested (section: string) (key: string) (map: Map<string * string, string>) : string option =
        let rec loop (subsections: string list) (key: string) (map: Map<string * string, string>) =
            match subsections with
            | [] -> None
            | head :: tail ->
                match Map.tryFind (head, key) map with
                | Some value -> Some value
                | None -> loop tail key map
        loop (Private.subsections section) key map
