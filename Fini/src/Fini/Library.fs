namespace Fini

open System
open System.Diagnostics.CodeAnalysis
open System.Runtime.InteropServices
open System.Text.RegularExpressions

[<AutoOpen>]
module private String =
    let inline indexOf (c: char) (s: string) =
        let s = nullArgCheck "s" s
        s.IndexOf c

    let inline lastIndexOf (c: char) (s: string) =
        let s = nullArgCheck "s" s
        s.LastIndexOf c

    let inline contains (c: char) (s: string) =
        let s = nullArgCheck "s" s
        s.Contains c

[<AutoOpen>]
module private Parser =
    [<Struct>]
    type Comment = { Text: string }

    [<Struct>]
    type Section = { Name: string }

    [<Struct>]
    type Parameter = { Key: string; Value: string }

    [<Struct>]
    type Line =
        | Whitespace
        | Comment of Comment: Comment
        | Section of Section: Section
        | Parameter of Parameter: Parameter

    let private whitespaceRegex = Regex(@"^\s*$", RegexOptions.Compiled)

    let private commentRegex = Regex(@"^\s*[;#]\s*(.*?)\s*$", RegexOptions.Compiled)

    let private sectionRegex = Regex(@"^\s*\[\s*([^=:;#\s\[\]]+)\s*\]\s*$", RegexOptions.Compiled)

    let private parameterRegex = Regex(@"^\s*([^=:;#\s\[\]]+)\s*=\s*(.*?)\s*$", RegexOptions.Compiled)

    let private (|ParseRegex|_|) (regex: Regex) input =
        match regex.Match(input) with
        | m when m.Success -> List.tail [ for g in m.Groups -> g.Value ] |> ValueSome
        | _ -> ValueNone

    let private (|ParseWhitespace|_|) text =
        match text with
        | ParseRegex whitespaceRegex _ -> Whitespace |> ValueSome
        | _ -> ValueNone

    let private (|ParseComment|_|) text =
        match text with
        | ParseRegex commentRegex [ text ] -> { Text = text } |> ValueSome
        | _ -> ValueNone

    let private (|ParseSection|_|) text =
        match text with
        | ParseRegex sectionRegex [ name ] -> { Name = name } |> ValueSome
        | _ -> ValueNone

    let private (|ParseParameter|_|) text =
        match text with
        | ParseRegex parameterRegex [ key; value ] -> { Key = key; Value = value } |> ValueSome
        | _ -> ValueNone

    let private (|ParseLine|_|) text =
        match text with
        | ParseWhitespace whitespace -> whitespace |> ValueSome
        | ParseComment comment -> comment |> Comment |> ValueSome
        | ParseSection section -> section |> Section |> ValueSome
        | ParseParameter parameter -> parameter |> Parameter |> ValueSome
        | _ -> ValueNone

    let parseLine text =
        match text with
        | ParseLine line -> Ok line
        | _ -> Error $"Cannot parse line: %s{text}."

[<CustomComparison; CustomEquality>]
type private Key =
    private
        { Path: string }

    interface IComparable with
        member this.CompareTo(obj) =
            match obj with
            | :? Key as other -> StringComparer.OrdinalIgnoreCase.Compare(this.Path, other.Path)
            | _ -> invalidArg "obj" "Object is not a Key."

    override this.Equals(obj) =
        match obj with
        | :? Key as other -> StringComparer.OrdinalIgnoreCase.Equals(this.Path, other.Path)
        | _ -> false

    override this.GetHashCode() = StringComparer.OrdinalIgnoreCase.GetHashCode(this.Path)

type Ini = private { Map: Map<Key, string> }

[<RequireQualifiedAccess>]
module Ini =
    let inline private ensureSeparator s = if contains ':' s then s else ":" + s

    let inline private firstIndexOfColon s = indexOf ':' s

    let inline private lastIndexOfDot s = lastIndexOf '.' s

    let empty = { Map = Map.empty }

    let isEmpty ini = Map.isEmpty ini.Map

    let containsKey key ini = Map.containsKey { Path = ensureSeparator key } ini.Map

    let count ini = Map.count ini.Map

    let keys ini = Map.keys ini.Map |> Seq.map _.Path

    let values ini = Map.values ini.Map :> string seq

    let tryAppend (lines: string seq) ini =
        use enumerator = (nullArgCheck "lines" lines).GetEnumerator()

        let rec loop map section =
            if not (enumerator.MoveNext()) then
                Ok { Map = map }
            else
                match parseLine enumerator.Current with
                | Error err -> Error err
                | Ok line ->
                    match line with
                    | Whitespace -> loop map section
                    | Comment { Text = _ } -> loop map section
                    | Section { Name = name } -> loop map { Path = name }
                    | Parameter { Key = key; Value = value } -> loop (Map.add { Path = section.Path + ":" + key } value map) section

        loop ini.Map { Path = "" }

    let tryCreate lines = tryAppend lines empty

    let tryFind key ini = Map.tryFind { Path = ensureSeparator key } ini.Map

    let tryFindNested key ini =
        let key = ensureSeparator key
        let separator = firstIndexOfColon key
        let section = key[.. separator - 1]
        let param = key[separator..]

        let rec loop section =
            match Map.tryFind { Path = section + param } ini.Map with
            | Some value -> Some value
            | None ->
                match section with
                | "" -> None
                | _ ->
                    match lastIndexOfDot section with
                    | -1 -> loop ""
                    | index -> loop section[.. index - 1]

        loop section

type Ini with
    static member Empty: Ini = Ini.empty

    member this.IsEmpty: bool = Ini.isEmpty this

    member this.ContainsKey(key: string) : bool = Ini.containsKey key this

    member this.Count: int = Ini.count this

    member this.Keys: string seq = Ini.keys this

    member this.Values: string seq = Ini.values this

    static member TryCreate(lines: string seq) : Result<Ini, string> = Ini.tryCreate lines

    member this.TryAppend(lines: string seq) : Result<Ini, string> = Ini.tryAppend lines this

    member this.TryFind(key: string, [<Out; MaybeNullWhen(false)>] value: byref<string>) : bool =
        match Ini.tryFind key this with
        | Some found ->
            value <- found
            true
        | None ->
            value <- Unchecked.defaultof<_>
            false

    member this.TryFindNested(key: string, [<Out; MaybeNullWhen(false)>] value: byref<string>) : bool =
        match Ini.tryFindNested key this with
        | Some found ->
            value <- found
            true
        | None ->
            value <- Unchecked.defaultof<_>
            false
