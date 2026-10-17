namespace Fini

open System
open System.Collections
open System.Collections.Generic
open System.IO
open System.Text.RegularExpressions

[<AutoOpen>]
module internal Guards =
    let inline requireNonNull paramName (argument: 'T) =
        ArgumentNullException.ThrowIfNull(argument, paramName)
        argument

    let inline requireNonNullSeq paramName (argument: 'T seq) =
        ArgumentNullException.ThrowIfNull(argument, paramName)
        argument

    let inline requireNonNullKeyValue paramName (argument: KeyValuePair<'K, 'V>) =
        ArgumentNullException.ThrowIfNull(argument.Key, $"{paramName}.{nameof argument.Key}")
        ArgumentNullException.ThrowIfNull(argument.Value, $"{paramName}.{nameof argument.Value}")
        argument

    let inline requireNonNullTuple paramName (argument: 'K * 'V) =
        ArgumentNullException.ThrowIfNull(fst argument, $"{paramName}.{nameof fst}")
        ArgumentNullException.ThrowIfNull(snd argument, $"{paramName}.{nameof snd}")
        argument

[<AutoOpen>]
module internal Enumerator =
    let inline getEnumerator (s: 'T seq) = s.GetEnumerator()

    let inline moveNext (e: IEnumerator<'T>) = e.MoveNext()

    let inline current (e: IEnumerator<'T>) = e.Current

[<AutoOpen>]
module internal String =
    [<Literal>]
    let Empty = ""

    [<Literal>]
    let Separator = ":"

    [<Literal>]
    let Colon = ':'

    [<Literal>]
    let Dot = '.'

    let comparer = StringComparer.OrdinalIgnoreCase

    let inline indexOf (c: char) (s: string) = s.IndexOf c

    let inline lastIndexOf (c: char) (s: string) = s.LastIndexOf c

[<AutoOpen>]
module internal File =
    let inline readLines path = File.ReadLines path

    let inline writeLines path (lines: string seq) = File.WriteAllLines(path, lines)

type internal Comment = string

[<CustomComparison; CustomEquality>]
type internal Section =
    { Name: string }

    member m.CompareTo other =
        match m.Name, other.Name with
        | Empty, Empty -> 0
        | Empty, _ -> -1
        | _, Empty -> 1
        | _ -> comparer.Compare(m.Name, other.Name)

    interface IComparable with
        member m.CompareTo obj =
            match obj with
            | :? Section as other -> m.CompareTo other
            | _ -> invalidArg (nameof obj) "Object is not a Section."

    override m.Equals(obj) =
        match obj with
        | :? Section as other -> comparer.Equals(m.Name, other.Name)
        | _ -> false

    override m.GetHashCode() = comparer.GetHashCode m.Name

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
          Parameter: string }

    member m.Path = m.Section.Name + Separator + m.Parameter

    member m.CompareTo other =
        match m.Section.CompareTo other.Section with
        | 0 -> comparer.Compare(m.Parameter, other.Parameter)
        | result -> result

    interface IComparable with
        member m.CompareTo obj =
            match obj with
            | :? Key as other -> m.CompareTo other
            | _ -> invalidArg (nameof obj) "Object is not a Key."

    override m.Equals(obj) =
        match obj with
        | :? Key as other -> m.Section.Equals other.Section && comparer.Equals(m.Parameter, other.Parameter)
        | _ -> false

    override m.GetHashCode() =
        HashCode.Combine(m.Section.GetHashCode(), comparer.GetHashCode m.Parameter)

[<RequireQualifiedAccess>]
module internal Key =
    let inline create key =
        match requireNonNull (nameof key) key |> indexOf Colon with
        | -1 -> { Section = { Name = Empty }; Parameter = key }
        | index ->
            { Section = { Name = key[.. index - 1] }
              Parameter = key[index + 1 ..] }

type ParserError =
    | InvalidKey of Key: string
    | InvalidLine of Line: string

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
        | ParseRegex sectionKeyRegex [ section; parameter ] -> ValueSome { Section = { Name = section }; Parameter = parameter }
        | _ -> ValueNone

    let private (|ParseParameterKey|_|) text =
        match text with
        | ParseRegex parameterKeyRegex [ parameter ] -> ValueSome { Section = { Name = Empty }; Parameter = parameter }
        | _ -> ValueNone

    let tryParseLine text =
        match text with
        | ParseWhitespace whitespace -> Ok whitespace
        | ParseComment comment -> Ok comment
        | ParseSection section -> Ok section
        | ParseParameter parameter -> Ok parameter
        | _ -> InvalidLine text |> Error

    let tryParseKey key =
        match key with
        | ParseSectionKey key -> Ok key
        | ParseParameterKey key -> Ok key
        | _ -> InvalidKey key |> Error

    let tryParseKeyValue (pair: KeyValuePair<string, string>) =
        match tryParseKey pair.Key with
        | Ok key -> Ok(key, pair.Value)
        | Error err -> Error err

    let tryParseTuple tuple =
        match fst tuple |> tryParseKey with
        | Ok key -> Ok(key, snd tuple)
        | Error err -> Error err

[<RequireQualifiedAccess>]
module internal KeyValuePair =
    let inline toStringKey (kvp: KeyValuePair<Key, string>) = KeyValuePair(kvp.Key.Path, kvp.Value)

[<RequireQualifiedAccess>]
module internal Tuple =
    let inline toStringKey (kvp: KeyValuePair<Key, string>) = kvp.Key.Path, kvp.Value

[<RequireQualifiedAccess>]
module internal Map =
    let create pairs requireNonNullPair tryParsePair =
        use enumerator = requireNonNullSeq (nameof pairs) pairs |> getEnumerator

        let rec loop map =
            if moveNext enumerator |> not then
                Ok map
            else
                match current enumerator |> requireNonNullPair (nameof pairs) |> tryParsePair with
                | Error err -> Error err
                | Ok(key, value) -> loop (Map.add key value map)

        loop Map.empty

    let appendLines lines map =
        use enumerator = requireNonNullSeq (nameof lines) lines |> getEnumerator

        let rec loop map section =
            if moveNext enumerator |> not then
                Ok map
            else
                match current enumerator |> requireNonNull (nameof lines) |> tryParseLine with
                | Error err -> Error err
                | Ok line ->
                    match line with
                    | Whitespace -> loop map section
                    | Comment _ -> loop map section
                    | Section { Name = name } -> loop map name
                    | Parameter { Key = key; Value = value } ->
                        loop (Map.add { Section = { Name = section }; Parameter = key } value map) section

        loop map Empty

    let add key value map =
        let key = requireNonNull (nameof key) key
        let value = requireNonNull (nameof value) value

        match tryParseKey key with
        | Ok key -> Map.add key value map |> Ok
        | Error err -> Error err

    let tryFind key map = Map.tryFind (Key.create key) map

    let tryFindNested key map =
        let key = Key.create key

        let rec loop section =
            match Map.tryFind { Section = section; Parameter = key.Parameter } map with
            | Some value -> Some value
            | None ->
                match section.Name with
                | Empty -> None
                | _ ->
                    match lastIndexOf Dot section.Name with
                    | -1 -> loop { Name = Empty }
                    | index -> loop { Name = section.Name[.. index - 1] }

        loop key.Section

    let find key map = Map.find (Key.create key) map

    let findNested key map =
        match tryFindNested key map with
        | Some value -> value
        | None -> KeyNotFoundException() |> raise

    let toLines (map: Map<Key, string>) =
        seq {
            let mutable written = false
            let mutable section = Empty

            for pair in map do
                let name = pair.Key.Section.Name

                if name <> Empty && comparer.Equals(name, section) |> not then
                    section <- name

                    if written then
                        yield Empty

                    yield $"[{name}]"

                yield
                    if pair.Value = Empty then
                        $"{pair.Key.Parameter} ="
                    else
                        $"{pair.Key.Parameter} = {pair.Value}"

                written <- true
        }

    let remove key map = Map.remove (Key.create key) map

    let containsKey key map = Map.containsKey (Key.create key) map

    let keys (map: Map<Key, _>) = Map.keys map |> Seq.map _.Path

    let values map = Map.values map :> string seq

    let sections (map: Map<Key, _>) =
        map |> Seq.map _.Key.Section |> Seq.distinct |> Seq.map _.Name

    let private sectionPredicate sign name map =
        map |> Map.filter (fun key _ -> comparer.Equals(key.Section.Name, name) |> sign)

    let section = sectionPredicate id

    let removeSection = sectionPredicate not

    let toFile path map = toLines map |> writeLines path

    let toString map = toLines map |> String.concat Environment.NewLine

[<StructuralComparison; StructuralEquality>]
type Ini =
    private
        { Map: Map<Key, string> }

    member private m.KeyValuePairs = m.Map |> Seq.map KeyValuePair.toStringKey

    interface IEnumerable<KeyValuePair<string, string>> with
        member m.GetEnumerator() = m.KeyValuePairs.GetEnumerator()

    interface IEnumerable with
        member m.GetEnumerator() = (m.KeyValuePairs :> IEnumerable).GetEnumerator()

    override m.ToString() = Map.toString m.Map

[<RequireQualifiedAccess>]
module Ini =
    let private ctr map = { Map = map }

    let empty = { Map = Map.empty }

    let create pairs =
        Map.create pairs requireNonNullKeyValue tryParseKeyValue |> Result.map ctr

    let ofSeq pairs =
        Map.create pairs requireNonNullTuple tryParseTuple |> Result.map ctr

    let appendLines lines ini = Map.appendLines lines ini.Map |> Result.map ctr

    let appendFile path ini = appendLines (readLines path) ini

    let fromLines lines = appendLines lines empty

    let fromFile path = appendFile path empty

    let isEmpty ini = Map.isEmpty ini.Map

    let add key value ini = Map.add key value ini.Map |> Result.map ctr

    let tryFind key ini = Map.tryFind key ini.Map

    let tryFindNested key ini = Map.tryFindNested key ini.Map

    let find key ini = Map.find key ini.Map

    let findNested key ini = Map.findNested key ini.Map

    let remove key ini = Map.remove key ini.Map |> ctr

    let count ini = Map.count ini.Map

    let containsKey key ini = Map.containsKey key ini.Map

    let keys ini = Map.keys ini.Map

    let values ini = Map.values ini.Map

    let sections ini = Map.sections ini.Map

    let section name ini = Map.section name ini.Map |> ctr

    let removeSection name ini = Map.removeSection name ini.Map |> ctr

    let toSeq ini = ini.Map |> Seq.map Tuple.toStringKey

    let toLines ini = Map.toLines ini.Map

    let toFile path ini = Map.toFile path ini.Map

    let toString ini = Map.toString ini.Map

type Ini with
    static member Empty = Ini.empty

    static member Create pairs = Ini.create pairs

    static member FromFile path = Ini.fromFile path

    static member FromLines lines = Ini.fromLines lines

    member m.AppendFile path = Ini.appendFile path m

    member m.AppendLines lines = Ini.appendLines lines m

    member m.IsEmpty = Ini.isEmpty m

    member m.Add(key, value) = Ini.add key value m

    member m.TryFind key = Ini.tryFind key m

    member m.TryFindNested key = Ini.tryFindNested key m

    member m.Find key = Ini.find key m

    member m.FindNested key = Ini.findNested key m

    member m.Remove key = Ini.remove key m

    member m.Count = Ini.count m

    member m.ContainsKey key = Ini.containsKey key m

    member m.Keys = Ini.keys m

    member m.Values = Ini.values m

    member m.Sections = Ini.sections m

    member m.Section name = Ini.section name m

    member m.RemoveSection name = Ini.removeSection name m

    member m.ToLines() = Ini.toLines m

    member m.ToFile path = Ini.toFile path m
