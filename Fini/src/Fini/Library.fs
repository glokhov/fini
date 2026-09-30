namespace Fini

open System
open System.Runtime.InteropServices
open System.Text.RegularExpressions

[<AutoOpen>]
module private Parser =
    [<Struct>]
    type Section = { Name: string }

    [<Struct>]
    type Parameter = { Name: string; Value: string }

    [<Struct>]
    type Line =
        | Section of Section: Section
        | Parameter of Parameter: Parameter

    let sectionRegex = Regex(@"^\s*\[\s*([^\]\s]+)\s*\]\s*$", RegexOptions.Compiled)

    let parameterRegex = Regex(@"^\s*(\S+?)\s*=\s*(.*?)\s*$", RegexOptions.Compiled)

    let (|ParseRegex|_|) (regex: Regex) input =
        match regex.Match(input) with
        | m when m.Success -> List.tail [ for g in m.Groups -> g.Value ] |> ValueSome
        | _ -> ValueNone

    let (|ParseSection|_|) text =
        match text with
        | ParseRegex sectionRegex [ name ] -> { Name = name } |> ValueSome
        | _ -> ValueNone

    let (|ParseParameter|_|) text =
        match text with
        | ParseRegex parameterRegex [ key; value ] -> { Name = key; Value = value } |> ValueSome
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

type Ini = { Map: Map<Key, string> }

[<RequireQualifiedAccess>]
module Ini =

    let inline private firstIndexOf (c: char) (s: string) = s.IndexOf c

    let inline private lastIndexOf (c: char) (s: string) = s.LastIndexOf c

    let inline private trim (s: string) = s.Trim()

    let inline private lastIndexOfDot s = lastIndexOf '.' s

    let inline private isNotEmpty s = not (String.IsNullOrWhiteSpace s)

    let inline private ensureLeadingDot (s: string) = if s.StartsWith '.' then s else "." + s

    let private trimComment line =
        match firstIndexOf '#' line with
        | -1 -> line
        | index -> line[.. index - 1]

    let append lines ini =
        let append map lines =
            let rec loop map lines section =
                match lines with
                | [] -> map
                | head :: tail ->
                    match head with
                    | Section { Name = name } -> loop map tail { section with Path = "." + name }
                    | Parameter { Name = key; Value = value } ->
                        loop (Map.add { section with Path = section.Path + "." + key } value map) tail section

            loop map (Seq.toList lines) { Path = "" }

        let map =
            lines
            |> Seq.map trimComment
            |> Seq.map trim
            |> Seq.filter isNotEmpty
            |> Seq.map parseLine
            |> append ini.Map

        { ini with Map = map }

    let empty = { Map = Map.empty }

    let create lines = empty |> append lines

    let tryFind key ini = Map.tryFind { Path = ensureLeadingDot key } ini.Map

    let tryFindNested key ini =
        let key = ensureLeadingDot key
        let separator = lastIndexOfDot key
        let section = key[.. separator - 1]
        let param = key[separator..]

        let rec loop section =
            match Map.tryFind { Path = section + param } ini.Map with
            | Some value -> Some value
            | None ->
                match section with
                | "" -> None
                | _ -> loop section[.. (lastIndexOfDot section) - 1]

        loop section

type Ini with
    static member Empty: Ini = Ini.empty

    static member Create(lines) : Ini = Ini.create lines

    member this.Append(lines) : Ini = Ini.append lines this

    member this.TryFind(key, [<Out>] value: byref<string>) : bool =
        match Ini.tryFind key this with
        | Some found ->
            value <- found
            true
        | None ->
            value <- null
            false

    member this.TryFindNested(key, [<Out>] value: byref<string>) : bool =
        match Ini.tryFindNested key this with
        | Some found ->
            value <- found
            true
        | None ->
            value <- null
            false
