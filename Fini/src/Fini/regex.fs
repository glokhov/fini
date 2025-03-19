module internal Regex

open System.Text.RegularExpressions

let ``match`` (pattern: string) (input: string) : Match option =
    match Regex.Match(input, pattern) with
    | m when m.Success -> Some m
    | _ -> None

let (|ParseRegex|_|) (pattern: string) (input: string) : string list option =
    match ``match`` pattern input with
    | Some m -> Some(List.tail [ for g in m.Groups -> g.Value ])
    | None -> None
