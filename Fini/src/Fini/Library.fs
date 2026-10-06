namespace Fini

open System
open System.Runtime.InteropServices
open System.Text.RegularExpressions

[<AutoOpen>]
module private String =
    let inline isNotEmpty s = not (String.IsNullOrEmpty s)

    let inline trim (s: string) =
        let s = nullArgCheck "s" s
        s.Trim()

    let inline indexOf (c: char) (s: string) =
        let s = nullArgCheck "s" s
        (nullArgCheck "s" s).IndexOf c

    let inline lastIndexOf (c: char) (s: string) =
        let s = nullArgCheck "s" s
        s.LastIndexOf c

    let inline firstIndexOfColon s =
        let s = nullArgCheck "s" s
        indexOf ':' s

    let inline lastIndexOfDot s =
        let s = nullArgCheck "s" s
        lastIndexOf '.' s

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

    let private sectionRegex = Regex(@"^\s*\[\s*([^\]\s:]+)\s*\]\s*$", RegexOptions.Compiled)

    let private parameterRegex = Regex(@"^\s*([^\s:=]+?)\s*=\s*(.*?)\s*$", RegexOptions.Compiled)

    let private (|ParseRegex|_|) (regex: Regex) input =
        match regex.Match(input) with
        | m when m.Success -> List.tail [ for g in m.Groups -> g.Value ] |> ValueSome
        | _ -> ValueNone

    let private (|ParseSection|_|) text =
        match text with
        | ParseRegex sectionRegex [ name ] -> { Name = name } |> ValueSome
        | _ -> ValueNone

    let private (|ParseParameter|_|) text =
        match text with
        | ParseRegex parameterRegex [ key; value ] -> { Name = key; Value = value } |> ValueSome
        | _ -> ValueNone

    let private (|ParseLine|_|) text =
        match text with
        | ParseSection section -> section |> Section |> ValueSome
        | ParseParameter parameter -> parameter |> Parameter |> ValueSome
        | _ -> ValueNone

    let parseLine text =
        match text with
        | ParseLine line -> Ok line
        | _ -> Error $"Cannot parse line: %s{text}."

[<RequireQualifiedAccess>]
module private Result =
    let traverse (lines: Result<Line, string> seq) =
        let folder state next =
            match state, next with
            | Ok tail, Ok head -> Ok(head :: tail)
            | Error err, _ -> Error err
            | _, Error err -> Error err

        Seq.fold folder (Ok []) lines |> Result.map List.rev

[<CustomComparison; CustomEquality>]
type Key =
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
        | _ -> invalidArg "obj" "Object is not a Key."

    override this.GetHashCode() = StringComparer.OrdinalIgnoreCase.GetHashCode(this.Path)

type Ini = private { Map: Map<Key, string> }

[<RequireQualifiedAccess>]
module Ini =
    let inline ensureSeparator (s: string) =
        match nullArgCheck "s" s with
        | s when s.Contains ':' -> s
        | s -> ":" + s

    let inline takeBefore c s =
        match indexOf c s with
        | -1 -> s
        | index -> s[.. index - 1]

    let empty = { Map = Map.empty }

    let isEmpty ini = Map.isEmpty ini.Map

    let containsKey ini key = Map.containsKey { Path = ensureSeparator key } ini.Map

    let count ini = Map.count ini.Map

    let keys ini = Map.keys ini.Map |> Seq.map _.Path

    let values ini = Map.values ini.Map :> string seq

    let tryAppend lines ini =
        let append map lines =
            let rec loop map lines section =
                match lines with
                | [] -> map
                | head :: tail ->
                    match head with
                    | Section { Name = name } -> loop map tail { section with Path = name }
                    | Parameter { Name = key; Value = value } ->
                        loop (Map.add { section with Path = section.Path + ":" + key } value map) tail section

            loop map (Seq.toList lines) { Path = "" }

        let lines =
            nullArgCheck "lines" lines
            |> Seq.map (takeBefore '#')
            |> Seq.map trim
            |> Seq.filter isNotEmpty
            |> Seq.map parseLine
            |> Result.traverse

        match lines with
        | Ok map -> Ok { Map = append ini.Map map }
        | Error err -> Error err

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

    member this.ContainsKey(key: string) : bool = Ini.containsKey this key

    member this.Count: int = Ini.count this

    member this.Keys: string seq = Ini.keys this

    member this.Values: string seq = Ini.values this

    static member TryCreate(lines: string seq) : Result<Ini, string> = Ini.tryCreate lines

    member this.TryAppend(lines: string seq) : Result<Ini, string> = Ini.tryAppend lines this

    member this.TryFind(key: string, [<Out>] value: byref<string>) : bool =
        match Ini.tryFind key this with
        | Some found ->
            value <- found
            true
        | None ->
            value <- Unchecked.defaultof<_>
            false

    member this.TryFindNested(key: string, [<Out>] value: byref<string>) : bool =
        match Ini.tryFindNested key this with
        | Some found ->
            value <- found
            true
        | None ->
            value <- Unchecked.defaultof<_>
            false
