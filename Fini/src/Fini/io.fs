namespace IO

open System
open System.IO
open FInvoke.Result

module internal File =
    let inline openText (path: string) : Result<StreamReader, string> =
        if File.Exists path then invoke File.OpenText path |> Result.mapError _.Message else Error $"File doesn't exist: {path}."

    let inline createText (path: string) : Result<StreamWriter, string> =
        invoke File.CreateText path |> Result.mapError _.Message

module internal TextReader =
    let inline private readLine (reader: TextReader) : Result<string, string> =
        invoke reader.ReadLine () |> Result.mapError _.Message

    let readLines (reader: TextReader) : Result<string, string> seq =
        seq {
            let mutable cur = Unchecked.defaultof<_>
            let mutable ok = true

            let readLine () =
                match readLine reader with
                | Ok null -> false
                | Ok line ->
                    cur <- Ok line
                    true
                | Error err ->
                    cur <- Error err
                    true

            while ok && readLine () do
                yield cur

                if cur.IsError then
                    ok <- false
        }

module internal TextWriter =
    let inline writeChar (value: char) (writer: TextWriter) : Result<TextWriter, string> =
        let write: char -> Result<unit, Exception> = invoke writer.Write
        write value |> Result.map (fun _ -> writer) |> Result.mapError _.Message

    let inline writeString (value: string) (writer: TextWriter) : Result<TextWriter, string> =
        let write: string -> Result<unit, Exception> = invoke writer.Write
        write value |> Result.map (fun _ -> writer) |> Result.mapError _.Message

    let inline writeEol (writer: TextWriter) : Result<TextWriter, string> =
        let write: unit -> Result<unit, Exception> = invoke writer.WriteLine
        write () |> Result.map (fun _ -> writer) |> Result.mapError _.Message

    let inline flush (writer: TextWriter) : Result<unit, string> =
        invoke writer.Flush () |> Result.mapError _.Message
