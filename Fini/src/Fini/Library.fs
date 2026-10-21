namespace Fini

open System
open System.Collections
open System.Collections.Generic
open System.Globalization
open System.IO
open System.Text.RegularExpressions

[<AutoOpen>]
module private Operators =
    let inline (<!>) arg func = Result.map func arg

    let inline (<?>) arg func = Option.map func arg

[<AutoOpen>]
module internal Guards =
    let inline private throwIfNull (paramName: string) (argument: 'T) =
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(argument, paramName)
#else
        if box argument |> isNull then
            ArgumentNullException paramName |> raise
#endif

    let inline requireNonNull paramName (argument: 'T) =
        throwIfNull paramName argument
        argument

    let inline requireNonNullSeq paramName (argument: 'T seq) =
        throwIfNull paramName argument
        argument

    let inline requireNonNullKeyValue paramName (argument: KeyValuePair<'K, 'V>) =
        throwIfNull $"{paramName}.{nameof argument.Key}" argument.Key
        throwIfNull $"{paramName}.{nameof argument.Value}" argument.Value
        argument

    let inline requireNonNullTuple paramName (argument: 'K * 'V) =
        throwIfNull $"{paramName}.{nameof fst}" (fst argument)
        throwIfNull $"{paramName}.{nameof snd}" (snd argument)
        argument

[<AutoOpen>]
module internal Hash =
    let inline combine (first: int) (second: int) =
#if NET10_0_OR_GREATER
        HashCode.Combine(first, second)
#else
        ((first <<< 5) + first) ^^^ second
#endif

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
        combine (m.Section.GetHashCode()) (comparer.GetHashCode m.Parameter)

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
#if NET10_0_OR_GREATER
    | InvalidValue of Value: string
#endif

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

#if NET10_0_OR_GREATER
    let tryParseValue<'T when 'T :> IParsable<'T>> value =
        match 'T.TryParse(value, CultureInfo.InvariantCulture) with
        | true, result -> Ok result
        | false, _ -> InvalidValue value |> Error
#endif

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
        match tryParseKey (requireNonNull (nameof key) key) with
        | Ok key -> Map.add key (requireNonNull (nameof value) value) map |> Ok
        | Error err -> Error err

    let tryFind key map = Map.tryFind (Key.create key) map

#if NET10_0_OR_GREATER
    let tryFindValue<'T when 'T :> IParsable<'T>> key map = tryFind key map <?> tryParseValue<'T>
#endif

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

#if NET10_0_OR_GREATER
    let tryFindNestedValue key map = tryFindNested key map <?> tryParseValue
#endif

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

    static let create map = { Map = map }

    static let empty = { Map = Map.empty }

    static member Empty = empty

    static member Create pairs =
        Map.create pairs requireNonNullKeyValue tryParseKeyValue <!> create

    static member OfSeq pairs =
        Map.create pairs requireNonNullTuple tryParseTuple <!> create

    static member FromFile path = Map.appendLines (readLines path) Map.empty <!> create

    static member FromLines lines = Map.appendLines lines Map.empty <!> create

    member m.AppendFile path = Map.appendLines (readLines path) m.Map <!> create

    member m.AppendLines lines = Map.appendLines lines m.Map <!> create

    member m.IsEmpty = Map.isEmpty m.Map

    member m.Add(key, value) = Map.add key value m.Map <!> create

    member m.TryFind key = Map.tryFind key m.Map

#if NET10_0_OR_GREATER
    member m.TryFindValue key = Map.tryFindValue key m.Map
#endif

    member m.TryFindNested key = Map.tryFindNested key m.Map

#if NET10_0_OR_GREATER
    member m.TryFindNestedValue key = Map.tryFindNestedValue key m.Map
#endif

    member m.Remove key = Map.remove key m.Map |> create

    member m.Count = Map.count m.Map

    member m.ContainsKey key = Map.containsKey key m.Map

    member m.Keys = Map.keys m.Map

    member m.Values = Map.values m.Map

    member m.Sections = Map.sections m.Map

    member m.Section name = Map.section name m.Map |> create

    member m.RemoveSection name = Map.removeSection name m.Map |> create

    member m.ToLines() = Map.toLines m.Map

    member m.ToFile path = Map.toFile path m.Map

    member m.KeyValuePairs = m.Map |> Seq.map KeyValuePair.toStringKey

    member m.Tuples = m.Map |> Seq.map Tuple.toStringKey

    interface IEnumerable<KeyValuePair<string, string>> with
        member m.GetEnumerator() = m.KeyValuePairs.GetEnumerator()

    interface IEnumerable with
        member m.GetEnumerator() = (m.KeyValuePairs :> IEnumerable).GetEnumerator()

    override m.ToString() = Map.toString m.Map

[<RequireQualifiedAccess>]
module Ini =
    let empty = Ini.Empty

    let ofSeq pairs = Ini.OfSeq pairs

    let appendLines lines (ini: Ini) = ini.AppendLines lines

    let appendFile path (ini: Ini) = ini.AppendFile path

    let fromLines lines = Ini.FromLines lines

    let fromFile path = Ini.FromFile path

    let isEmpty (ini: Ini) = ini.IsEmpty

    let add key value (ini: Ini) = ini.Add(key, value)

    let tryFind key (ini: Ini) = ini.TryFind key

#if NET10_0_OR_GREATER
    let tryFindValueAs key (ini: Ini) = ini.TryFindValue key
#endif

    let tryFindNested key (ini: Ini) = ini.TryFindNested key

#if NET10_0_OR_GREATER
    let tryFindNestedValueAs key (ini: Ini) = ini.TryFindNestedValue key
#endif

    let remove key (ini: Ini) = ini.Remove key

    let count (ini: Ini) = ini.Count

    let containsKey key (ini: Ini) = ini.ContainsKey key

    let keys (ini: Ini) = ini.Keys

    let values (ini: Ini) = ini.Values

    let sections (ini: Ini) = ini.Sections

    let section name (ini: Ini) = ini.Section name

    let removeSection name (ini: Ini) = ini.RemoveSection name

    let toSeq (ini: Ini) = ini.Tuples

    let toLines (ini: Ini) = ini.ToLines()

    let toFile path (ini: Ini) = ini.ToFile path

    let toString (ini: Ini) = ini.ToString()
