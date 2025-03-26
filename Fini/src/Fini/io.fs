module internal IO

open System
open System.IO
open FInvoke.Result

let readLines: path: string -> Result<string seq, Exception> = invoke File.ReadLines

let writeAllText: path: string -> contents: string -> Result<unit, Exception> = invoke2 File.WriteAllText

module File =

    let openText (path: string) : Result<StreamReader, string> =
        invoke File.OpenText path |> Result.mapError _.Message

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
