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
module FuncConvert =
    let inline fromFunc (parser: Func<'T, 'U>) = FuncConvert.FromFunc parser

[<AutoOpen>]
module KeyValuePair =
    let inline key (kvp: KeyValuePair<_, _>) = kvp.Key

    let inline value (kvp: KeyValuePair<_, _>) = kvp.Value

    let inline deconstruct pair = (key pair, value pair)

[<AutoOpen>]
module Enumerator =
    let inline getEnumerator (s: 'T seq) = s.GetEnumerator()

    let inline moveNext (e: IEnumerator<'T>) = e.MoveNext()

    let inline current (e: IEnumerator<'T>) = e.Current

[<AutoOpen>]
module String =
    [<Literal>]
    let Empty = ""

[<AutoOpen>]
module File =
    let inline readLines path = File.ReadLines path

    let inline writeLines path (lines: string seq) = File.WriteAllLines(path, lines)

[<AutoOpen>]
module Parser =
    let whitespaceLineRegex = Regex(@"^\s*$", RegexOptions.Compiled)

    let commentLineRegex = Regex(@"^\s*[;#]\s*(.*?)\s*$", RegexOptions.Compiled)

    let sectionLineRegex = Regex(@"^\s*\[\s*([^=:;#\s\[\]]+)\s*\]\s*$", RegexOptions.Compiled)

    let parameterLineRegex = Regex(@"^\s*([^=:;#\s\[\]]+)\s*=\s*(.*?)\s*$", RegexOptions.Compiled)

    let nameRegex = Regex(@"^([^=:;#\s\[\]]+)$", RegexOptions.Compiled)

    let keyRegex = Regex(@"^([^=:;#\s\[\]]*):([^=:;#\s\[\]]+)$", RegexOptions.Compiled)

    let inline (|ParseRegex|_|) (regex: Regex) input =
        match regex.Match(input) with
        | m when m.Success -> List.tail [ for g in m.Groups -> g.Value ] |> ValueSome
        | _ -> ValueNone

type Section = { Name: string }

type Parameter = { Key: string; Value: string }

type Line =
    | Whitespace
    | Comment
    | Section of Section: Section
    | Parameter of Parameter: Parameter

type ParserError =
    | InvalidName of Name: string
    | InvalidLine of Line: string
    | InvalidValue of Value: string

[<CustomComparison; CustomEquality>]
type Name =
    private
        { Name: string }

    static let comparer = StringComparer.OrdinalIgnoreCase

    member s.CompareTo other =
        match s.Name, other.Name with
        | Empty, Empty -> 0
        | Empty, _ -> -1
        | _, Empty -> 1
        | _ -> comparer.Compare(s.Name, other.Name)

    interface IComparable with
        member m.CompareTo obj =
            match obj with
            | :? Name as other -> m.CompareTo other
            | _ -> invalidArg (nameof obj) "Object is not a Section."

    override s.Equals obj =
        match obj with
        | :? Name as other -> comparer.Equals(s.Name, other.Name)
        | _ -> false

    override s.GetHashCode() = comparer.GetHashCode s.Name

    override s.ToString() = s.Name

[<RequireQualifiedAccess>]
module Name =
    let empty = { Name = Empty }

    let tryParse name =
        match name with
        | Empty -> empty |> Ok
        | ParseRegex nameRegex [ name ] -> { Name = name } |> Ok
        | _ -> InvalidName name |> Error

    let parse name =
        match name with
        | Empty -> empty
        | ParseRegex nameRegex [ name ] -> { Name = name }
        | _ -> invalidArg (nameof name) name

type Name with
    static member Empty = Name.empty

    static member TryParse name = Name.tryParse name

    static member Parse name = Name.parse name

    static member op_Implicit name = Name.parse name

    static member op_Implicit(section: Name) = section.ToString()

[<CustomComparison; CustomEquality>]
type Key =
    private
        { Section: Name
          Parameter: Name }

    member k.CompareTo other =
        match k.Section.CompareTo other.Section with
        | 0 -> k.Parameter.CompareTo other.Parameter
        | result -> result

    interface IComparable with
        member k.CompareTo obj =
            match obj with
            | :? Key as other -> k.CompareTo other
            | _ -> invalidArg (nameof obj) "Object is not a Key."

    override k.Equals obj =
        match obj with
        | :? Key as other -> k.Section.Equals other.Section && k.Parameter.Equals other.Parameter
        | _ -> false

    override k.GetHashCode() =
        let first = k.Section.GetHashCode()
        let second = k.Parameter.GetHashCode()
        ((first <<< 5) + first) ^^^ second

    override k.ToString() = k.Section.Name + ":" + k.Parameter.Name

[<RequireQualifiedAccess>]
module Key =
    let tryParse key =
        match key with
        | ParseRegex nameRegex [ parameter ] ->
            Ok
                { Section = { Name = Empty }
                  Parameter = { Name = parameter } }
        | ParseRegex keyRegex [ section; parameter ] ->
            Ok
                { Section = { Name = section }
                  Parameter = { Name = parameter } }
        | _ -> InvalidName key |> Error

    let parse key =
        match key with
        | ParseRegex nameRegex [ parameter ] ->
            { Section = { Name = Empty }
              Parameter = { Name = parameter } }
        | ParseRegex keyRegex [ section; parameter ] ->
            { Section = { Name = section }
              Parameter = { Name = parameter } }
        | _ -> invalidArg (nameof key) key

type Key with
    static member TryParse name = Key.tryParse name

    static member Parse name = Key.parse name

    static member op_Implicit name = Key.parse name

    static member op_Implicit(key: Key) = key.ToString()

[<RequireQualifiedAccess>]
module Map =
    let inline lastIndexOf (c: char) (s: string) = s.LastIndexOf c

    let inline (|ParseWhitespace|_|) text =
        match text with
        | ParseRegex whitespaceLineRegex _ -> ValueSome Whitespace
        | _ -> ValueNone

    let inline (|ParseComment|_|) text =
        match text with
        | ParseRegex commentLineRegex [ _ ] -> ValueSome Comment
        | _ -> ValueNone

    let inline (|ParseSection|_|) text =
        match text with
        | ParseRegex sectionLineRegex [ name ] -> Section { Name = name } |> ValueSome
        | _ -> ValueNone

    let inline (|ParseParameter|_|) text =
        match text with
        | ParseRegex parameterLineRegex [ key; value ] -> Parameter { Key = key; Value = value } |> ValueSome
        | _ -> ValueNone

    let tryParseLine line =
        match line with
        | ParseWhitespace whitespace -> Ok whitespace
        | ParseComment comment -> Ok comment
        | ParseSection section -> Ok section
        | ParseParameter parameter -> Ok parameter
        | _ -> InvalidLine line |> Error

#if NET
    let parseAs<'T when 'T :> IParsable<'T>> value = 'T.Parse(value, CultureInfo.InvariantCulture)

    let tryParseAs<'T when 'T :> IParsable<'T>> value =
        match 'T.TryParse(value, CultureInfo.InvariantCulture) with
        | true, result -> Ok result
        | false, _ -> value |> InvalidValue |> Error
#endif

    let parseWith<'T> (parser: string -> 'T) value = parser value

    let tryParseWith<'T> (parser: string -> 'T) value =
        try
            parser value |> Ok
        with _ ->
            value |> InvalidValue |> Error

    let append pairs map =
        use enumerator = pairs |> getEnumerator

        let rec loop map =
            if moveNext enumerator |> not then
                map
            else
                let key, value = current enumerator
                Map.add key value map |> loop

        loop map

    let appendLines lines map =
        use enumerator = lines |> getEnumerator

        let rec loop section map =
            if moveNext enumerator |> not then
                Ok map
            else
                match current enumerator |> tryParseLine with
                | Error err -> Error err
                | Ok line ->
                    match line with
                    | Whitespace -> loop section map
                    | Comment -> loop section map
                    | Section { Name = name } -> loop name map
                    | Parameter { Key = key; Value = value } ->
                        Map.add { Section = { Name = section }; Parameter = { Name = key } } value map |> loop section

        loop Empty map

    let add key value map = map |> Map.add key value

    let tryFind key map = map |> Map.tryFind key

#if NET
    let tryFindAs key map = map |> tryFind key <?> tryParseAs
#endif

    let tryFindWith parser key map = map |> tryFind key <?> tryParseWith parser

    let tryFindNested key map =
        let rec loop section =
            match Map.tryFind { Section = section; Parameter = key.Parameter } map with
            | Some value -> Some value
            | None ->
                match section.Name with
                | Empty -> None
                | _ ->
                    match lastIndexOf '.' section.Name with
                    | -1 -> loop { Name = Empty }
                    | index -> loop { Name = section.Name[.. index - 1] }

        loop key.Section

#if NET
    let tryFindNestedAs key map = map |> tryFindNested key <?> tryParseAs
#endif

    let tryFindNestedWith parser key map = map |> tryFindNested key <?> tryParseWith parser

    let find key map = map |> Map.find key

#if NET
    let findAs key map = map |> find key |> parseAs
#endif

    let findWith parser key map = map |> find key |> parseWith parser

    let findNested key map =
        match map |> tryFindNested key with
        | Some value -> value
        | None -> KeyNotFoundException() |> raise

#if NET
    let findNestedAs key map = map |> findNested key |> parseAs
#endif

    let findNestedWith parser key map = map |> findNested key |> parseWith parser

    let remove key map = map |> Map.remove key

    let containsKey key map = map |> Map.containsKey key

    let keys map = map |> Map.keys :> Key seq

    let values map = map |> Map.values :> string seq

    let sections map = map |> Seq.map key |> Seq.map _.Section |> Seq.distinct

    let sectionWith sign (section: Name) map =
        map |> Map.filter (fun key _ -> section |> key.Section.Equals |> sign)

    let section = sectionWith id

    let removeSection = sectionWith not

    let toLines (map: Map<Key, string>) =
        seq {
            let mutable written = false
            let mutable section = Name.empty

            for pair in map do
                let name = pair.Key.Section

                if name <> Name.empty && name.Equals(section) |> not then
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

type Ini =
    private
        { Map: Map<Key, string> }

    member m.KeyValuePairs = m.Map :> KeyValuePair<Key, string> seq

    interface IEnumerable<KeyValuePair<Key, string>> with
        member m.GetEnumerator() = m.KeyValuePairs.GetEnumerator()

    interface IEnumerable with
        member m.GetEnumerator() = (m.KeyValuePairs :> IEnumerable).GetEnumerator()

    override m.ToString() = Map.toString m.Map

[<RequireQualifiedAccess>]
module Ini =
    let inline create map = { Map = map }

    let empty = { Map = Map.empty }

    let ofSeq pairs = Map.empty |> Map.append pairs |> create

    let fromLines lines = Map.empty |> Map.appendLines lines <!> create

    let fromFile path = Map.empty |> Map.appendLines (readLines path) <!> create

    let append pairs ini = ini.Map |> Map.append pairs |> create

    let appendLines lines ini = ini.Map |> Map.appendLines lines <!> create

    let appendFile path ini = ini.Map |> Map.appendLines (readLines path) <!> create

    let isEmpty ini = ini.Map |> Map.isEmpty

    let add key value ini = ini.Map |> Map.add key value |> create

    let tryFind key ini = ini.Map |> Map.tryFind key

#if NET
    let tryFindAs key ini = ini.Map |> Map.tryFindAs key
#endif

    let tryFindWith parser key ini = ini.Map |> Map.tryFindWith parser key

    let tryFindNested key ini = ini.Map |> Map.tryFindNested key

#if NET
    let tryFindNestedAs key ini = ini.Map |> Map.tryFindNestedAs key
#endif

    let tryFindNestedWith parser key ini = ini.Map |> Map.tryFindNestedWith parser key

    let find key ini = ini.Map |> Map.find key

#if NET
    let findAs key ini = ini.Map |> Map.findAs key
#endif

    let findWith parser key ini = ini.Map |> Map.findWith parser key

    let findNested key ini = ini.Map |> Map.findNested key

#if NET
    let findNestedAs key ini = ini.Map |> Map.findNestedAs key
#endif

    let findNestedWith parser key ini = ini.Map |> Map.findNestedWith parser key

    let remove key ini = ini.Map |> Map.remove key |> create

    let count ini = ini.Map |> Map.count

    let containsKey key ini = ini.Map |> Map.containsKey key

    let keys ini = ini.Map |> Map.keys

    let values ini = ini.Map |> Map.values

    let sections ini = ini.Map |> Map.sections

    let section name ini = ini.Map |> Map.section name |> create

    let removeSection name ini = ini.Map |> Map.removeSection name |> create

    let toSeq ini = ini.Map |> Seq.map deconstruct

    let toLines ini = ini.Map |> Map.toLines

    let toFile path ini = ini.Map |> Map.toFile path

    let toString ini = ini.Map |> Map.toString

type Ini with
    static member Empty = Ini.empty

    static member Create pairs = pairs |> Seq.map deconstruct |> Ini.ofSeq

    static member FromLines lines = lines |> Ini.fromLines

    static member FromFile path = path |> Ini.fromFile

    member m.Append pairs = Ini.append (pairs |> Seq.map deconstruct) m

    member m.AppendLines lines = Ini.appendLines lines m

    member m.AppendFile path = Ini.appendFile path m

    member m.IsEmpty = Ini.isEmpty m

    member m.Add(key, value) = Ini.add key value m

    member m.TryFind key = Ini.tryFind key m

#if NET
    member m.TryFindAs key = Ini.tryFindAs key m
#endif

    member m.TryFindWith(parser, key) = Ini.tryFindWith (fromFunc parser) key m

    member m.TryFindNested key = Ini.tryFindNested key m

#if NET
    member m.TryFindNestedAs key = Ini.tryFindNestedAs key m
#endif

    member m.TryFindNestedWith(parser, key) = Ini.tryFindNestedWith (fromFunc parser) key m

    member m.Find key = Ini.find key m

#if NET
    member m.FindAs key = Ini.findAs key m
#endif

    member m.FindWith(parser, key) = Ini.findWith (fromFunc parser) key m

    member m.FindNested key = Ini.findNested key m

#if NET
    member m.FindNestedAs key = Ini.findNestedAs key m
#endif

    member m.FindNestedWith(parser, key) = Ini.findNestedWith (fromFunc parser) key m

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

[<AutoOpen>]
module Casting =
    let inline name name = Name.parse name

    let inline key key = Key.parse key
