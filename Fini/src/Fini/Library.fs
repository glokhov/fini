namespace Fini

open System
open System.Collections
open System.Collections.Generic
open System.Globalization
open System.IO
open System.Text.RegularExpressions

[<AutoOpen>]
module Operators =
    let inline (<!>) arg func = Result.map func arg

    let inline (<?>) arg func = Option.map func arg

[<AutoOpen>]
module KeyValue =
    let inline key (kvp: KeyValuePair<_, _>) = kvp.Key

    let inline value (kvp: KeyValuePair<_, _>) = kvp.Value

[<AutoOpen>]
module Enumerator =
    let inline getEnumerator (s: 'T seq) = s.GetEnumerator()

    let inline moveNext (e: IEnumerator<'T>) = e.MoveNext()

    let inline current (e: IEnumerator<'T>) = e.Current

[<AutoOpen>]
module String =
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
module File =
    let inline readLines path = File.ReadLines path

    let inline writeLines path (lines: string seq) = File.WriteAllLines(path, lines)

[<AutoOpen>]
module Guards =
    let inline throwIfNull argumentName argument =
        if box argument |> isNull then
            nullArg argumentName

    let inline requireNonNull argumentName argument =
        throwIfNull argumentName argument
        argument

    let inline requireNonNullTuple argumentName argument =
        throwIfNull $"{argumentName}.{nameof fst}" (fst argument)
        throwIfNull $"{argumentName}.{nameof snd}" (snd argument)
        argument

[<CustomComparison; CustomEquality>]
type Section =
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

    override m.Equals obj =
        match obj with
        | :? Section as other -> comparer.Equals(m.Name, other.Name)
        | _ -> false

    override m.GetHashCode() = comparer.GetHashCode m.Name

type Parameter = { Key: string; Value: string }

type Line =
    | Whitespace
    | Comment
    | Section of Section: Section
    | Parameter of Parameter: Parameter

[<CustomComparison; CustomEquality>]
type Key =
    { Section: Section
      Parameter: string }

    member m.CompareTo other =
        match m.Section.CompareTo other.Section with
        | 0 -> comparer.Compare(m.Parameter, other.Parameter)
        | result -> result

    interface IComparable with
        member m.CompareTo obj =
            match obj with
            | :? Key as other -> m.CompareTo other
            | _ -> invalidArg (nameof obj) "Object is not a Key."

    override m.Equals obj =
        match obj with
        | :? Key as other -> m.Section.Equals other.Section && comparer.Equals(m.Parameter, other.Parameter)
        | _ -> false

    override m.GetHashCode() =
        let first = m.Section.GetHashCode()
        let second = comparer.GetHashCode m.Parameter
        ((first <<< 5) + first) ^^^ second

[<RequireQualifiedAccess>]
module Key =
    let inline create key =
        match requireNonNull (nameof key) key |> indexOf Colon with
        | -1 -> { Section = { Name = Empty }; Parameter = key }
        | index ->
            { Section = { Name = key[.. index - 1] }
              Parameter = key[index + 1 ..] }

    let inline path key = key.Section.Name + Separator + key.Parameter

type ParserError =
    | InvalidKey of Key: string
    | InvalidLine of Line: string
#if NET
    | InvalidValue of Value: string
#endif

[<AutoOpen>]
module Parser =
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
        | ParseRegex commentRegex [ _ ] -> ValueSome Comment
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

    let tryParseTuple kvp =
        match fst kvp |> tryParseKey with
        | Ok key -> Ok(key, snd kvp)
        | Error err -> Error err

#if NET
    let tryParseValue<'T when 'T :> IParsable<'T>> value =
        match 'T.TryParse(value, CultureInfo.InvariantCulture) with
        | true, result -> Ok result
        | false, _ -> InvalidValue value |> Error
#endif

[<RequireQualifiedAccess>]
module Map =
    let append pairs map =
        use enumerator = requireNonNull (nameof pairs) pairs |> getEnumerator

        let rec loop map =
            if moveNext enumerator |> not then
                Ok map
            else
                match current enumerator |> requireNonNullTuple (nameof pairs) |> tryParseTuple with
                | Error err -> Error err
                | Ok(key, value) -> Map.add key value map |> loop

        loop map

    let appendLines lines map =
        use enumerator = requireNonNull (nameof lines) lines |> getEnumerator

        let rec loop section map =
            if moveNext enumerator |> not then
                Ok map
            else
                match current enumerator |> requireNonNull (nameof lines) |> tryParseLine with
                | Error err -> Error err
                | Ok line ->
                    match line with
                    | Whitespace -> loop section map
                    | Comment -> loop section map
                    | Section { Name = name } -> loop name map
                    | Parameter { Key = key; Value = value } ->
                        Map.add { Section = { Name = section }; Parameter = key } value map |> loop section

        loop Empty map

    let add key value map =
        match requireNonNull (nameof key) key |> tryParseKey with
        | Ok key -> Map.add key (requireNonNull (nameof value) value) map |> Ok
        | Error err -> Error err

    let tryFind key map = Map.tryFind (Key.create key) map

#if NET
    let tryFindValue key map = tryFind key map <?> tryParseValue
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

#if NET
    let tryFindNestedValue key map = tryFindNested key map <?> tryParseValue
#endif

    let find key map = Map.find (Key.create key) map

    let findNested key map =
        match tryFindNested key map with
        | Some value -> value
        | None -> KeyNotFoundException() |> raise

    let remove key map = Map.remove (Key.create key) map

    let containsKey key map = Map.containsKey (Key.create key) map

    let keys map = Map.keys map |> Seq.map Key.path

    let values map = Map.values map :> string seq

    let sections map =
        map |> Seq.map key |> Seq.map _.Section |> Seq.distinct |> Seq.map _.Name

    let sectionWith sign name map =
        map |> Map.filter (fun key _ -> comparer.Equals(key.Section.Name, name) |> sign)

    let section = sectionWith id

    let removeSection = sectionWith not

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

    let toFile path map = toLines map |> writeLines path

    let toString map = toLines map |> String.concat Environment.NewLine

[<StructuralComparison; StructuralEquality>]
type Ini =
    private
        { Map: Map<Key, string> }

    member m.KeyValuePairs = m.Map |> Seq.map (fun kvp -> KeyValuePair(key kvp |> Key.path, kvp.Value))

    interface IEnumerable<KeyValuePair<string, string>> with
        member m.GetEnumerator() = m.KeyValuePairs.GetEnumerator()

    interface IEnumerable with
        member m.GetEnumerator() = (m.KeyValuePairs :> IEnumerable).GetEnumerator()

    override m.ToString() = Map.toString m.Map

[<RequireQualifiedAccess>]
module Ini =
    let inline create map = { Map = map }

    let empty = { Map = Map.empty }

    let ofSeq pairs = Map.append pairs Map.empty <!> create

    let fromLines lines = Map.appendLines lines Map.empty <!> create

    let fromFile path = Map.appendLines (readLines path) Map.empty <!> create

    let append pairs ini = Map.append pairs ini.Map <!> create

    let appendLines lines ini = Map.appendLines lines ini.Map <!> create

    let appendFile path ini = Map.appendLines (readLines path) ini.Map <!> create

    let isEmpty ini = Map.isEmpty ini.Map

    let add key value ini = Map.add key value ini.Map <!> create

    let tryFind key ini = Map.tryFind key ini.Map

#if NET
    let tryFindValue key ini = Map.tryFindValue key ini.Map
#endif

    let tryFindNested key ini = Map.tryFindNested key ini.Map

#if NET
    let tryFindNestedValue key ini = Map.tryFindNestedValue key ini.Map
#endif

    let remove key ini = Map.remove key ini.Map |> create

    let count ini = Map.count ini.Map

    let containsKey key ini = Map.containsKey key ini.Map

    let keys ini = Map.keys ini.Map

    let values ini = Map.values ini.Map

    let sections ini = Map.sections ini.Map

    let section name ini = Map.section name ini.Map |> create

    let removeSection name ini = Map.removeSection name ini.Map |> create

    let toSeq ini = ini.Map |> Seq.map (fun kvp -> Key.path kvp.Key, kvp.Value)

    let toLines ini = Map.toLines ini.Map

    let toFile path ini = Map.toFile path ini.Map

    let toString ini = Map.toString ini.Map

[<RequireQualifiedAccess>]
module KVP =
    let inline deconstruct pair = (key pair, value pair)

type Ini with
    static member Empty = Ini.empty

    static member Create pairs = pairs |> Seq.map KVP.deconstruct |> Ini.ofSeq

    static member FromLines lines = Ini.fromLines lines

    static member FromFile path = Ini.fromFile path

    member m.Append pairs = Ini.append (pairs |> Seq.map KVP.deconstruct) m

    member m.AppendLines lines = Ini.appendLines lines m

    member m.AppendFile path = Ini.appendFile path m

    member m.IsEmpty = Ini.isEmpty m

    member m.Add(key, value) = Ini.add key value m

    member m.TryFind key = Ini.tryFind key m

#if NET
    member m.TryFindValue key = Ini.tryFindValue key m
#endif

    member m.TryFindNested key = Ini.tryFindNested key m

#if NET
    member m.TryFindNestedValue key = Ini.tryFindNestedValue key m
#endif

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
