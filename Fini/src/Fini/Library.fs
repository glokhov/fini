namespace Fini

open System
open System.Collections
open System.Collections.Generic
open System.Diagnostics.CodeAnalysis
open System.IO
open System.Runtime.InteropServices
open System.Text.RegularExpressions

[<AutoOpen>]
module internal Guards =
    let inline requireNonNull paramName (value: 'T) =
        ArgumentNullException.ThrowIfNull(value, paramName)
        value

    let inline requireNonNullSeq paramName (value: 'T seq) =
        ArgumentNullException.ThrowIfNull(value, paramName)
        value

    let inline requireNonNullPair paramName (pair: KeyValuePair<'K, 'V>) =
        ArgumentNullException.ThrowIfNull(pair.Key, $"{paramName}.{nameof pair.Key}")
        ArgumentNullException.ThrowIfNull(pair.Value, $"{paramName}.{nameof pair.Value}")
        pair

    let inline requireNonNullTuple paramName (tuple: 'K * 'V) =
        ArgumentNullException.ThrowIfNull(fst tuple, $"{paramName}.{nameof fst}")
        ArgumentNullException.ThrowIfNull(snd tuple, $"{paramName}.{nameof snd}")
        tuple

[<AutoOpen>]
module internal Enumerator =
    let inline getEnumerator (s: 'T seq) = s.GetEnumerator()

    let inline moveNext (e: IEnumerator<'T>) = e.MoveNext()

    let inline current (e: IEnumerator<'T>) = e.Current

[<AutoOpen>]
module internal String =
    let inline indexOf (c: char) (s: string) = s.IndexOf c

    let inline lastIndexOf (c: char) (s: string) = s.LastIndexOf c

[<AutoOpen>]
module internal File =
    let inline readLines path = File.ReadLines path

    let inline writeLines path (lines: string seq) = File.WriteAllLines(path, lines)

[<AutoOpen>]
module internal OrdinalIgnoreCase =
    let comparer = StringComparer.OrdinalIgnoreCase

type internal Comment = string

[<CustomComparison; CustomEquality>]
type internal Section =
    { Name: string }

    member m.CompareTo(other) =
        match m.Name, other.Name with
        | "", "" -> 0
        | "", _ -> -1
        | _, "" -> 1
        | _ -> comparer.Compare(m.Name, other.Name)

    interface IComparable with
        member m.CompareTo(obj) =
            match obj with
            | :? Section as other -> m.CompareTo other
            | _ -> invalidArg "obj" "Object is not a Name."

    override m.Equals(obj) =
        match obj with
        | :? Section as other -> comparer.Equals(m.Name, other.Name)
        | _ -> false

    override m.GetHashCode() = comparer.GetHashCode(m.Name)

type internal Parameter = { Key: string; Value: string }

type internal Line =
    | Whitespace
    | Comment of Comment: Comment
    | Section of Section: Section
    | Parameter of Parameter: Parameter

[<CustomComparison; CustomEquality>]
type internal Key =
    private
        { Section: Section
          Param: string }

    member m.Path = m.Section.Name + ":" + m.Param

    member m.CompareTo(other) =
        match m.Section.CompareTo(other.Section) with
        | 0 -> comparer.Compare(m.Param, other.Param)
        | result -> result

    interface IComparable with
        member m.CompareTo(obj) =
            match obj with
            | :? Key as other -> m.CompareTo other
            | _ -> invalidArg "obj" "Object is not a Key."

    override m.Equals(obj) =
        match obj with
        | :? Key as other -> comparer.Equals(m.Section, other.Section) && comparer.Equals(m.Param, other.Param)
        | _ -> false

    override m.GetHashCode() =
        HashCode.Combine(comparer.GetHashCode(m.Section), comparer.GetHashCode(m.Param))

[<RequireQualifiedAccess>]
module internal Key =
    let inline create key =
        match key |> requireNonNull (nameof key) |> indexOf ':' with
        | -1 -> { Section = { Name = "" }; Param = key }
        | index ->
            { Section = { Name = key[.. index - 1] }
              Param = key[index + 1 ..] }

    let inline path (key: Key) = key.Path

[<RequireQualifiedAccess>]
module internal Pair =
    let inline stringKey (pair: KeyValuePair<Key, _>) = KeyValuePair<string, _>(pair.Key.Path, pair.Value)

    let inline toTuple (pair: KeyValuePair<Key, _>) = pair.Key, pair.Value

    let inline sectionNameEquals (pair: KeyValuePair<Key, _>) section = comparer.Equals(pair.Key.Section.Name, section)

[<RequireQualifiedAccess>]
module internal Tuple =
    let inline stringKey (pair: KeyValuePair<Key, _>) = pair.Key.Path, pair.Value

[<AutoOpen>]
module internal Parser =
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
        | ParseRegex commentRegex [ text ] -> Comment text |> ValueSome
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
        | ParseRegex sectionKeyRegex [ section; parameter ] -> ValueSome { Section = { Name = section }; Param = parameter }
        | _ -> ValueNone

    let private (|ParseParameterKey|_|) text =
        match text with
        | ParseRegex parameterKeyRegex [ parameter ] -> ValueSome { Section = { Name = "" }; Param = parameter }
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
        match pair.Key |> tryParseKey with
        | Ok key -> Ok(key, pair.Value)
        | Error err -> Error err

    let tryParseTuple tuple =
        match fst tuple |> tryParseKey with
        | Ok key -> Ok(key, snd tuple)
        | Error err -> Error err

[<StructuralComparison; StructuralEquality>]
type Ini =
    private
        { Map: Map<Key, string> }

    static let empty = { Map = Map.empty }

    static member Empty = empty

    static member internal Create(pairs, requireNonNull, tryParse) =
        use enumerator = pairs |> requireNonNullSeq (nameof pairs) |> getEnumerator

        let rec loop map =
            if moveNext enumerator |> not then
                Ok { Map = map }
            else
                match current enumerator |> requireNonNull (nameof pairs) |> tryParse with
                | Error err -> Error err
                | Ok(key, value) -> loop (Map.add key value map)

        loop empty.Map

    static member Create(pairs) = Ini.Create(pairs, requireNonNullPair, tryParseKeyValue)

    static member FromFile(path) = path |> readLines |> empty.AppendLines

    static member FromLines(lines) = lines |> empty.AppendLines

    member m.AppendFile(path) = path |> readLines |> m.AppendLines

    member m.AppendLines(lines) =
        use enumerator = lines |> requireNonNullSeq (nameof lines) |> getEnumerator

        let rec loop map section =
            if moveNext enumerator |> not then
                Ok { Map = map }
            else
                match current enumerator |> requireNonNull (nameof lines) |> tryParseLine with
                | Error err -> Error err
                | Ok line ->
                    match line with
                    | Whitespace -> loop map section
                    | Comment _ -> loop map section
                    | Section { Name = name } -> loop map name
                    | Parameter { Key = key; Value = value } ->
                        loop (Map.add { Section = { Name = section }; Param = key } value map) section

        loop m.Map ""

    member m.IsEmpty = m.Map.IsEmpty

    member m.Add(key, value) =
        match key |> requireNonNull (nameof key) |> tryParseKey with
        | Ok key -> Ok { Map = Map.add key (value |> requireNonNull (nameof value)) m.Map }
        | Error err -> Error err

    member internal m.TryFindInternal(key) = m.Map.TryFind(key |> Key.create)

    member m.TryFind(key, [<Out; MaybeNullWhen(false)>] value: byref<string>) =
        match key |> m.TryFindInternal with
        | Some found ->
            value <- found
            true
        | None ->
            value <- Unchecked.defaultof<_>
            false

    member internal m.TryFindNestedInternal(key) =
        let key = key |> Key.create

        let rec loop section =
            match Map.tryFind { Section = section; Param = key.Param } m.Map with
            | Some value -> Some value
            | None ->
                match section.Name with
                | "" -> None
                | _ ->
                    match lastIndexOf '.' section.Name with
                    | -1 -> loop { Name = "" }
                    | index -> loop { Name = section.Name[.. index - 1] }

        loop key.Section

    member m.TryFindNested(key, [<Out; MaybeNullWhen(false)>] value: byref<string>) =
        match key |> m.TryFindNestedInternal with
        | Some found ->
            value <- found
            true
        | None ->
            value <- Unchecked.defaultof<_>
            false

    member m.Find(key) = Map.find (key |> Key.create) m.Map

    member m.FindNested(key) =
        match key |> m.TryFindNestedInternal with
        | Some value -> value
        | None -> raise (KeyNotFoundException())

    member m.Remove(key) = { Map = Map.remove (key |> Key.create) m.Map }

    member m.Count = m.Map.Count

    member m.ContainsKey key = Map.containsKey (key |> Key.create) m.Map

    member m.Keys = m.Map.Keys |> Seq.map _.Path

    member m.Values = m.Map.Values :> string seq

    member m.KeyValuePairs = m.Map |> Seq.map Pair.stringKey

    member m.Sections = m.Map |> Seq.map _.Key.Section |> Seq.distinct |> Seq.map _.Name

    member m.Section name =
        { Map =
            m.Map
            |> Seq.filter (fun pair -> Pair.sectionNameEquals pair name)
            |> Seq.map Pair.toTuple
            |> Map.ofSeq }

    member m.ToLines() =
        seq {
            let mutable written = false
            let mutable section = ""

            for pair in m.Map do
                let name = pair.Key.Section.Name

                if name <> "" && comparer.Equals(name, section) |> not then
                    section <- name

                    if written then
                        yield ""

                    yield $"[{name}]"

                yield
                    if pair.Value = "" then
                        $"{pair.Key.Param} ="
                    else
                        $"{pair.Key.Param} = {pair.Value}"

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

    let ofSeq pairs = Ini.Create(pairs, requireNonNullTuple, tryParseTuple)

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

    let sections (ini: Ini) = ini.Sections

    let section name (ini: Ini) = ini.Section name

    let toSeq (ini: Ini) = ini.Map |> Seq.map Tuple.stringKey

    let toLines (ini: Ini) = ini.ToLines()

    let toFile path (ini: Ini) = ini.ToFile path

    let toString (ini: Ini) = ini.ToString()
