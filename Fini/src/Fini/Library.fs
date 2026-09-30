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

    let inline parseLine text =
        match text with
        | ParseLine line -> line
        | _ -> failwith $"Cannot parse line: %s{text}."

[<CustomComparison; CustomEquality>]
type Key =
    { Key: string
      Comparer: StringComparer }

    interface IComparable with
        member this.CompareTo(other) =
            match other with
            | :? Key as other -> this.Comparer.Compare(this.Key, other.Key)
            | _ -> this.Comparer.Compare(this.Key, null)

    override this.Equals(other) =
        match other with
        | :? Key as other -> this.Comparer.Equals(this.Key, other.Key)
        | _ -> false

    override this.GetHashCode() = this.Comparer.GetHashCode(this.Key)

type Ini = { Table: Map<Key, string>; Comparer: StringComparer }

[<RequireQualifiedAccess>]
module Ini =

    let inline private firstIndexOf (c: char) (s: string) = s.IndexOf c

    let inline private lastIndexOf (c: char) (s: string) = s.LastIndexOf c

    let inline private trim (s: string) = s.Trim()

    let inline private isNotEmpty s = not (String.IsNullOrWhiteSpace s)

    let inline private ensureDot (s: string) = if s.StartsWith '.' then s else "." + s

    let inline private trimComment line =
        match firstIndexOf '#' line with
        | -1 -> line
        | index -> line[.. index - 1]

    let inline emptyWithComparer comparer = { Table = Map.empty; Comparer = comparer }

    let empty = emptyWithComparer StringComparer.OrdinalIgnoreCase

    let append lines ini =
        let rec collect table section lines =
            match lines with
            | [] -> table
            | head :: tail ->
                match head with
                | Section key -> collect table { section with Key = "." + key } tail
                | Parameter(key, value) -> collect (Map.add { section with Key = section.Key + "." + key } value table) section tail

        let table =
            lines
            |> Seq.map trimComment
            |> Seq.map trim
            |> Seq.filter isNotEmpty
            |> Seq.map parseLine
            |> Seq.toList
            |> collect ini.Table { Key = ""; Comparer = ini.Comparer }

        { ini with Table = table }

    let createWithComparer comparer lines = emptyWithComparer comparer |> append lines

    let create lines = createWithComparer StringComparer.OrdinalIgnoreCase lines

    let inline createKey key ini = { Key = key; Comparer = ini.Comparer }

    let tryFindNested key ini =
        let key = ensureDot key

        match lastIndexOf '.' key with
        | -1 -> None
        | lastDot ->
            let param = key[lastDot + 1 ..]

            let rec loop (section: string) =
                match Map.tryFind (createKey (section + "." + param) ini) ini.Table with
                | Some value -> Some value
                | None ->
                    if section = "" then
                        None
                    else
                        match lastIndexOf '.' section with
                        | -1 -> loop ""
                        | index -> loop section[.. index - 1]

            loop key[.. lastDot - 1]

    let tryFind key ini = Map.tryFind (createKey (ensureDot key) ini) ini.Table

type Ini with

    static member Empty() : Ini = Ini.empty

    static member Empty(comparer: StringComparer) : Ini = Ini.emptyWithComparer comparer

    static member Create(lines: string seq) : Ini = Ini.create lines

    static member Create(lines: string seq, comparer: StringComparer) : Ini = Ini.createWithComparer comparer lines

    member this.Append(lines: string seq) : Ini = Ini.append lines this

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
