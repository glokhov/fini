namespace Fini

open System
open System.Collections
open System.Collections.Generic
open System.Diagnostics.CodeAnalysis
open System.Runtime.InteropServices
open System.Text.RegularExpressions

[<AutoOpen>]
module private Enumerator =
    let inline getEnumerator (s: seq<'T>) =
        let s = nullArgCheck "s" s
        s.GetEnumerator()

    let inline moveNext (e: IEnumerator<'T>) =
        let e = nullArgCheck "e" e
        e.MoveNext()

    let inline current (e: IEnumerator<'T>) =
        let e = nullArgCheck "e" e
        e.Current

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

type private Comment = { Text: string }

type private Section = { Name: string }

type private Parameter = { Key: string; Value: string }

type private Line =
    | Whitespace
    | Comment of Comment: Comment
    | Section of Section: Section
    | Parameter of Parameter: Parameter

[<CustomComparison; CustomEquality>]
type private Key =
    private
        { Path: string }

    static let comparer = StringComparer.OrdinalIgnoreCase

    interface IComparable with
        member m.CompareTo(obj) =
            match obj with
            | :? Key as other -> comparer.Compare(m.Path, other.Path)
            | _ -> invalidArg "obj" "Object is not a Key."

    override m.Equals(obj) =
        match obj with
        | :? Key as other -> comparer.Equals(m.Path, other.Path)
        | _ -> false

    override m.GetHashCode() = comparer.GetHashCode(m.Path)

[<RequireQualifiedAccess>]
module private Key =
    let inline private ensureSeparator key = if contains ':' key then key else ":" + key

    let inline create key = { Path = ensureSeparator key }

    let inline split key =
        let separator = indexOf ':' key.Path
        let section = key.Path[.. separator - 1]
        let param = key.Path[separator..]
        (section, param)

[<AutoOpen>]
module private KeyValuePair =
    let inline toStringKey (pair: KeyValuePair<Key, string>) = KeyValuePair<string, string>(pair.Key.Path, pair.Value)

[<AutoOpen>]
module private Parser =
    let private whitespaceRegex = Regex(@"^\s*$", RegexOptions.Compiled)

    let private commentRegex = Regex(@"^\s*[;#]\s*(.*?)\s*$", RegexOptions.Compiled)

    let private sectionRegex = Regex(@"^\s*\[\s*([^=:;#\s\[\]]+)\s*\]\s*$", RegexOptions.Compiled)

    let private parameterRegex = Regex(@"^\s*([^=:;#\s\[\]]+)\s*=\s*(.*?)\s*$", RegexOptions.Compiled)

    let private sectionKeyRegex = Regex(@"^([^=:;#\s\[\]]*):([^=:;#\s\[\]]+)$", RegexOptions.Compiled)

    let private parameterKeyRegex = Regex(@"^([^=:;#\s\[\]]+)$", RegexOptions.Compiled)

    let private (|ParseRegex|_|) (regex: Regex) input =
        match regex.Match(input) with
        | m when m.Success -> List.tail [ for g in m.Groups -> g.Value ] |> ValueSome
        | _ -> ValueNone

    let private (|ParseWhitespace|_|) text =
        match text with
        | ParseRegex whitespaceRegex _ -> ValueSome Whitespace
        | _ -> ValueNone

    let private (|ParseComment|_|) text =
        match text with
        | ParseRegex commentRegex [ text ] -> Comment { Text = text } |> ValueSome
        | _ -> ValueNone

    let private (|ParseSection|_|) text =
        match text with
        | ParseRegex sectionRegex [ name ] -> Section { Name = name } |> ValueSome
        | _ -> ValueNone

    let private (|ParseParameter|_|) text =
        match text with
        | ParseRegex parameterRegex [ key; value ] -> Parameter { Key = key; Value = value } |> ValueSome
        | _ -> ValueNone

    let private (|ParseSectionKey|_|) text =
        match text with
        | ParseRegex sectionKeyRegex [ section; parameter ] -> ValueSome { Path = section + ":" + parameter }
        | _ -> ValueNone

    let private (|ParseParameterKey|_|) text =
        match text with
        | ParseRegex parameterKeyRegex [ parameter ] -> ValueSome { Path = ":" + parameter }
        | _ -> ValueNone

    let tryParseLine text =
        match text with
        | ParseWhitespace whitespace -> Ok whitespace
        | ParseComment comment -> Ok comment
        | ParseSection section -> Ok section
        | ParseParameter parameter -> Ok parameter
        | _ -> Error $"Cannot parse line: %s{text}."

    let tryParseKey text =
        match text with
        | ParseSectionKey key -> Ok key
        | ParseParameterKey key -> Ok key
        | _ -> Error $"Invalid key: %s{text}."

[<StructuralComparison; StructuralEquality>]
type Ini =
    private
        { Map: Map<Key, string> }

    static let empty = { Map = Map.empty }

    static member Empty = empty

    static member TryCreate(lines) = empty.TryAppend lines

    member m.TryAppend(lines) =
        use enumerator = lines |> getEnumerator

        let rec loop map section =
            if moveNext enumerator |> not then
                Ok { Map = map }
            else
                match current enumerator |> tryParseLine with
                | Error err -> Error err
                | Ok line ->
                    match line with
                    | Whitespace -> loop map section
                    | Comment { Text = _ } -> loop map section
                    | Section { Name = name } -> loop map name
                    | Parameter { Key = key; Value = value } -> loop (Map.add { Path = section + ":" + key } value map) section

        loop m.Map ""

    member m.IsEmpty = m.Map.IsEmpty

    member m.TryAdd(key, value) =
        match tryParseKey key with
        | Ok key -> Ok { Map = Map.add key value m.Map }
        | Error err -> Error err

    member internal m.TryFindInternal(key) = m.Map.TryFind(Key.create key)

    member m.TryFind(key, [<Out; MaybeNullWhen(false)>] value: byref<string>) =
        match m.TryFindInternal key with
        | Some found ->
            value <- found
            true
        | None ->
            value <- Unchecked.defaultof<_>
            false

    member internal m.TryFindNestedInternal(key) =
        let key = Key.create key
        let split = Key.split key
        let section = fst split
        let param = snd split

        let rec loop section =
            match Map.tryFind (section + param |> Key.create) m.Map with
            | Some value -> Some value
            | None ->
                match section with
                | "" -> None
                | _ ->
                    match lastIndexOf '.' section with
                    | -1 -> loop ""
                    | index -> loop section[.. index - 1]

        loop section

    member m.TryFindNested(key, [<Out; MaybeNullWhen(false)>] value: byref<string>) =
        match m.TryFindNestedInternal key with
        | Some found ->
            value <- found
            true
        | None ->
            value <- Unchecked.defaultof<_>
            false

    member m.Find(key) = Map.find (Key.create key) m.Map

    member m.FindNested(key) =
        match m.TryFindNestedInternal key with
        | Some value -> value
        | None -> raise (KeyNotFoundException $"Key not found: %s{key}.")

    member m.Remove(key) = { Map = Map.remove (Key.create key) m.Map }

    member m.Count = m.Map.Count

    member m.ContainsKey key = Map.containsKey (Key.create key) m.Map

    member m.Keys = m.Map.Keys |> Seq.map _.Path

    member m.Values = m.Map.Values :> string seq

    member private m.Pairs = m.Map |> Seq.map toStringKey

    interface IEnumerable<KeyValuePair<string, string>> with
        member m.GetEnumerator() = m.Pairs.GetEnumerator()

    interface IEnumerable with
        member m.GetEnumerator() = (m.Pairs :> IEnumerable).GetEnumerator()

[<RequireQualifiedAccess>]
module Ini =
    let empty = Ini.Empty

    let tryCreate lines = Ini.TryCreate lines

    let tryAppend lines (ini: Ini) = ini.TryAppend lines

    // toSeq/toList/toArray

    // ofSeq/ofList/ofArray

    let isEmpty (ini: Ini) = ini.IsEmpty

    let tryAdd key value (ini: Ini) = ini.TryAdd(key, value)

    let tryFind key (ini: Ini) = ini.TryFindInternal key

    let tryFindNested key (ini: Ini) = ini.TryFindNestedInternal key

    let find key (ini: Ini) = ini.Find key

    let findNested key (ini: Ini) = ini.FindNested key

    let remove key (ini: Ini) = ini.Remove key

    let count (ini: Ini) = ini.Count

    let containsKey key (ini: Ini) = ini.ContainsKey key

    let keys (ini: Ini) = ini.Keys

    let values (ini: Ini) = ini.Values
