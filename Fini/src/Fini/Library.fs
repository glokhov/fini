namespace Fini

open System
open System.Collections
open System.Collections.Generic
open System.Diagnostics.CodeAnalysis
open System.IO
open System.Runtime.InteropServices
open System.Text.RegularExpressions
open FInvoke

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

[<AutoOpen>]
module private File =
    let inline readLines path =
        Result.invoke File.ReadLines (nullArgCheck "path" path) |> Result.mapError _.Message

    let inline writeLines path lines =
        let writeLines: string -> string seq -> Result<unit, exn> = Result.invoke2 File.WriteAllLines
        writeLines (nullArgCheck "path" path) lines |> Result.mapError _.Message

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
        { Section: string
          Parameter: string }

    static let comparer = StringComparer.OrdinalIgnoreCase

    member m.Path = m.Section + ":" + m.Parameter

    interface IComparable with
        member m.CompareTo(obj) =
            match obj with
            | :? Key as other ->
                match m.Section, other.Section with
                | "", "" -> comparer.Compare(m.Parameter, other.Parameter)
                | "", _ -> -1
                | _, "" -> 1
                | lhs, rhs ->
                    match comparer.Compare(lhs, rhs) with
                    | 0 -> comparer.Compare(m.Parameter, other.Parameter)
                    | result -> result
            | _ -> invalidArg "obj" "Object is not a Key."

    override m.Equals(obj) =
        match obj with
        | :? Key as other -> comparer.Equals(m.Section, other.Section) && comparer.Equals(m.Parameter, other.Parameter)
        | _ -> false

    override m.GetHashCode() =
        HashCode.Combine(comparer.GetHashCode(m.Section), comparer.GetHashCode(m.Parameter))

[<RequireQualifiedAccess>]
module private Key =
    let inline create key =
        match indexOf ':' key with
        | -1 -> { Section = ""; Parameter = key }
        | index ->
            { Section = key[.. index - 1]
              Parameter = key[index + 1 ..] }

[<AutoOpen>]
module private KeyValuePair =
    let inline convert (pair: KeyValuePair<Key, _>) = KeyValuePair<string, _>(pair.Key.Path, pair.Value)

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
        | ParseRegex sectionKeyRegex [ section; parameter ] -> ValueSome { Section = section; Parameter = parameter }
        | _ -> ValueNone

    let private (|ParseParameterKey|_|) text =
        match text with
        | ParseRegex parameterKeyRegex [ parameter ] -> ValueSome { Section = ""; Parameter = parameter }
        | _ -> ValueNone

    let tryParseLine text =
        match text with
        | ParseWhitespace whitespace -> Ok whitespace
        | ParseComment comment -> Ok comment
        | ParseSection section -> Ok section
        | ParseParameter parameter -> Ok parameter
        | _ -> Error $"Cannot parse line: %s{text}."

    let tryParseKey key =
        match key with
        | ParseSectionKey key -> Ok key
        | ParseParameterKey key -> Ok key
        | _ -> Error $"Invalid key: %s{key}."

    let tryParseKeyValue (pair: KeyValuePair<string, string>) =
        match tryParseKey pair.Key with
        | Ok key -> Ok(key, pair.Value)
        | Error err -> Error err

[<StructuralComparison; StructuralEquality>]
type Ini =
    private
        { Map: Map<Key, string> }

    static let empty = { Map = Map.empty }

    static member Empty = empty

    static member OfSeq(pairs) =
        use enumerator = pairs |> getEnumerator

        let rec loop map =
            if moveNext enumerator |> not then
                Ok { Map = map }
            else
                match current enumerator |> tryParseKeyValue with
                | Error err -> Error err
                | Ok(key, value) -> loop (Map.add key value map)

        loop empty.Map

    static member FromFile(path) =
        match readLines path with
        | Ok lines -> empty.AppendLines lines
        | Error err -> Error err

    static member FromLines(lines) = empty.AppendLines lines

    member m.AppendFile(path) =
        match readLines path with
        | Ok lines -> m.AppendLines lines
        | Error err -> Error err

    member m.AppendLines(lines) =
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
                    | Parameter { Key = key; Value = value } -> loop (Map.add { Section = section; Parameter = key } value map) section

        loop m.Map ""

    member m.IsEmpty = m.Map.IsEmpty

    member m.Add(key, value) =
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

        let rec loop section =
            match Map.tryFind { Section = section; Parameter = key.Parameter } m.Map with
            | Some value -> Some value
            | None ->
                match section with
                | "" -> None
                | _ ->
                    match lastIndexOf '.' section with
                    | -1 -> loop ""
                    | index -> loop section[.. index - 1]

        loop key.Section

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
        | None -> raise (KeyNotFoundException())

    member m.Remove(key) = { Map = Map.remove (Key.create key) m.Map }

    member m.Count = m.Map.Count

    member m.ContainsKey key = Map.containsKey (Key.create key) m.Map

    member m.Keys = m.Map.Keys |> Seq.map _.Path

    member m.Values = m.Map.Values :> string seq

    member m.KeyValuePairs = m.Map |> Seq.map convert

    member m.ToLines() =
        seq {
            let mutable written = false
            let mutable section = ""

            for pair in m.Map do
                let name = pair.Key.Section

                if name <> "" && not (String.Equals(name, section, StringComparison.OrdinalIgnoreCase)) then
                    section <- name

                    if written then
                        yield ""

                    yield $"[{name}]"

                yield
                    if pair.Value = "" then
                        $"{pair.Key.Parameter} ="
                    else
                        $"{pair.Key.Parameter} = {pair.Value}"

                written <- true
        }

    member m.ToFile(path) = m.ToLines() |> writeLines path

    interface IEnumerable<KeyValuePair<string, string>> with
        member m.GetEnumerator() = m.KeyValuePairs.GetEnumerator()

    interface IEnumerable with
        member m.GetEnumerator() = (m.KeyValuePairs :> IEnumerable).GetEnumerator()

    override m.ToString() = m.ToLines() |> String.concat "\n"

[<RequireQualifiedAccess>]
module Ini =
    let empty = Ini.Empty

    let ofSeq pairs = pairs |> Ini.OfSeq

    let fromFile path = Ini.FromFile path

    let fromLines lines = Ini.FromLines lines

    let appendFile path (ini: Ini) = ini.AppendFile path

    let appendLines lines (ini: Ini) = ini.AppendLines lines

    let isEmpty (ini: Ini) = ini.IsEmpty

    let add key value (ini: Ini) = ini.Add(key, value)

    let tryFind key (ini: Ini) = ini.TryFindInternal key

    let tryFindNested key (ini: Ini) = ini.TryFindNestedInternal key

    let find key (ini: Ini) = ini.Find key

    let findNested key (ini: Ini) = ini.FindNested key

    let remove key (ini: Ini) = ini.Remove key

    let count (ini: Ini) = ini.Count

    let containsKey key (ini: Ini) = ini.ContainsKey key

    let keys (ini: Ini) = ini.Keys

    let values (ini: Ini) = ini.Values

    let toSeq (ini: Ini) = ini.KeyValuePairs

    let toLines (ini: Ini) = ini.ToLines()

    let toFile path (ini: Ini) = ini.ToFile path

    let toString (ini: Ini) = ini.ToString()
