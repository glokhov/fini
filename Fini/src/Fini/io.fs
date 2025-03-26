module internal IO

open System
open System.IO
open FInvoke.Result

module File =
    let openText (path: string) : Result<StreamReader, string> =
        invoke File.OpenText path |> Result.mapError _.Message

    let createText (path: string) : Result<StreamWriter, string> =
        invoke File.CreateText path |> Result.mapError _.Message

module TextReader =
    let readLines (reader: TextReader) : Result<string, string> seq =
        seq {
            let mutable cur = Unchecked.defaultof<_>
            let mutable ok = true

            let readLine () =
                match invoke reader.ReadLine () with
                | Ok null -> false
                | Ok line ->
                    cur <- Ok line
                    true
                | Error err ->
                    cur <- Error err.Message
                    true

            while ok && readLine () do
                yield cur

                if cur.IsError then
                    ok <- false
        }

module TextWriter =
    let flush (writer: TextWriter) : Result<unit, string> =
        invoke writer.Flush () |> Result.mapError _.Message

    let writeLine (writer: TextWriter) (value: string) : Result<unit, string> =
        let writeLine: string -> Result<unit, Exception> = invoke writer.WriteLine
        writeLine value |> Result.mapError _.Message
