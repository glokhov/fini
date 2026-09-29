namespace Fini.v3

open System
open System.Collections.Generic

[<AutoOpen>]
module private String =
    let inline firstIndexOf (c: char) (s: string) = s.IndexOf c
    let inline lastIndexOf (c: char) (s: string) = s.LastIndexOf c
    let inline isNotEmpty s = not (String.IsNullOrWhiteSpace s)

[<AutoOpen>]
module private Nested =
    let rec nested acc key =
        match lastIndexOf '.' key with
        | -1 -> List.rev acc
        | index ->
            let parent = key[.. index - 1]
            nested (parent :: acc) parent

type Key(key, comparer: StringComparer) =
    let nested = lazy (nested [ key ] key)

    new(key) = Key(key, StringComparer.Ordinal)

    member _.Key = key
    member _.Keys = nested.Value

    interface IComparable with
        member _.CompareTo(other) =
            match other with
            | :? Key as other -> comparer.Compare(key, other.Key)
            | _ -> comparer.Compare(key, null)

    interface IComparer<string> with
        member _.Compare(x, y) = comparer.Compare(x, y)

    override _.Equals(other) =
        match other with
        | :? Key as other -> comparer.Equals(key, other.Key)
        | _ -> false

    override _.GetHashCode() = comparer.GetHashCode(key)

[<AutoOpen>]
module private Parser =
    let inline trimComment line =
        match firstIndexOf '#' line with
        | -1 -> line
        | index -> line[.. index - 1]

    let inline parseLineWithComparer comparer line =
        match firstIndexOf '=' line with
        | -1 -> invalidArg (nameof line) $"Expected a key-value pair, but no '=' was found: {line}"
        | index ->
            let key = Key(line[.. index - 1].Trim(), comparer)
            let value = line[index + 1 ..].Trim()
            key, value

module Ini =
    let parseLinesWithComparer comparer lines =
        lines
        |> Seq.map trimComment
        |> Seq.filter isNotEmpty
        |> Seq.map (parseLineWithComparer comparer)
        |> Map.ofSeq

    let parseLines lines = parseLinesWithComparer StringComparer.Ordinal lines
