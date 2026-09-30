namespace Fini

open System
open System.Runtime.InteropServices
open System.Text.RegularExpressions

[<AutoOpen>]
module private Parser =

    type Line =
        | Section of string
        | Parameter of string * string

    let sectionRegex = Regex(@"^\s*\[\s*([^\]\s]+)\s*\]\s*$", RegexOptions.Compiled)

    let parameterRegex = Regex(@"^\s*(\S+?)\s*=\s*(.*?)\s*$", RegexOptions.Compiled)

    let (|ParseRegex|_|) (regex: Regex) input =
        match regex.Match(input) with
        | m when m.Success -> List.tail [ for g in m.Groups -> g.Value ] |> ValueSome
        | _ -> ValueNone

    let (|ParseSection|_|) text =
        match text with
        | ParseRegex sectionRegex [ name ] -> name |> ValueSome
        | _ -> ValueNone

    let (|ParseParameter|_|) text =
        match text with
        | ParseRegex parameterRegex [ key; value ] -> (key, value) |> ValueSome
        | _ -> ValueNone

    let (|ParseLine|_|) text =
        match text with
        | ParseSection section -> section |> Section |> ValueSome
        | ParseParameter parameter -> parameter |> Parameter |> ValueSome
        | _ -> ValueNone

    let parseLine text =
        match text with
        | ParseLine line -> line
        | _ -> failwith $"Cannot parse line: %s{text}."

[<CustomComparison; CustomEquality>]
type Key =
    { Path: string }

    interface IComparable with
        member this.CompareTo(other) =
            match other with
            | :? Key as other -> StringComparer.OrdinalIgnoreCase.Compare(this.Path, other.Path)
            | _ -> StringComparer.OrdinalIgnoreCase.Compare(this.Path, null)

    override this.Equals(other) =
        match other with
        | :? Key as other -> StringComparer.OrdinalIgnoreCase.Equals(this.Path, other.Path)
        | _ -> false

    override this.GetHashCode() = StringComparer.OrdinalIgnoreCase.GetHashCode(this.Path)

type Ini = { Table: Map<Key, string> }

[<RequireQualifiedAccess>]
module Ini =

    let inline private firstIndexOf (c: char) (s: string) = s.IndexOf c

    let inline private lastIndexOf (c: char) (s: string) = s.LastIndexOf c

    let inline private trim (s: string) = s.Trim()

    let inline private isNotEmpty s = not (String.IsNullOrWhiteSpace s)

    let inline private ensureDot (s: string) = if s.StartsWith '.' then s else "." + s

    let private trimComment line =
        match firstIndexOf '#' line with
        | -1 -> line
        | index -> line[.. index - 1]

    let append lines ini =
        let rec collect table section lines =
            match lines with
            | [] -> table
            | head :: tail ->
                match head with
                | Section key -> collect table { section with Path = "." + key } tail
                | Parameter(key, value) -> collect (Map.add { section with Path = section.Path + "." + key } value table) section tail

        let table =
            lines
            |> Seq.map trimComment
            |> Seq.map trim
            |> Seq.filter isNotEmpty
            |> Seq.map parseLine
            |> Seq.toList
            |> collect ini.Table { Path = "" }

        { ini with Table = table }

    let empty = { Table = Map.empty }

    let create lines = empty |> append lines

    let tryFind key ini = Map.tryFind { Path = ensureDot key } ini.Table

    let tryFindNested key ini =
        let key = ensureDot key
        let lastDot = lastIndexOf '.' key
        let param = key[lastDot + 1 ..]

        let rec loop (section: string) =
            match Map.tryFind { Path = section + "." + param } ini.Table with
            | Some value -> Some value
            | None -> if section = "" then None else loop section[.. (lastIndexOf '.' section) - 1]

        loop key[.. lastDot - 1]

type Ini with

    static member Empty : Ini = Ini.empty

    static member Create(lines) : Ini = Ini.create lines

    member this.Append(lines) : Ini = Ini.append lines this

    member this.TryFind(key: string, [<Out>] value: byref<string>) : bool =
        match Ini.tryFind key this with
        | Some found ->
            value <- found
            true
        | None ->
            value <- null
            false

    member this.TryFindNested(key: string, [<Out>] value: byref<string>) : bool =
        match Ini.tryFindNested key this with
        | Some found ->
            value <- found
            true
        | None ->
            value <- null
            false
