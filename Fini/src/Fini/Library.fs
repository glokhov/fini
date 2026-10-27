namespace Fini

open System
open System.Collections
open System.Collections.Generic
open System.Globalization
open System.IO
open System.Text.RegularExpressions

[<AutoOpen>]
module Operators =
    let inline (>>=) m f = Result.bind f m

    let inline (<!>) m f = Result.map f m

    let inline (<?>) m f = Option.map f m

[<AutoOpen>]
module FuncConvert =
    let inline fromFunc (f: Func<'T, 'U>) = FuncConvert.FromFunc f

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

    let inline lastIndexOf (c: char) (s: string) = s.LastIndexOf c

    let inline split (c: char) (s: string) = s.Split c

[<AutoOpen>]
module File =
    let inline readLines path = File.ReadLines path

    let inline writeLines path (lines: string seq) = File.WriteAllLines(path, lines)

[<AutoOpen>]
module Parser =
    let whitespaceLineRegex = Regex(@"^\s*\z", RegexOptions.Compiled)

    let commentLineRegex = Regex(@"^\s*[;#]\s*(.*?)\s*\z", RegexOptions.Compiled)

    let sectionLineRegex = Regex(@"^\s*\[\s*([^=:;#\s\[\]]+)\s*\]\s*\z", RegexOptions.Compiled)

    let parameterLineRegex = Regex(@"^\s*([^=:;#\s\[\]]+)\s*=\s*(.*?)\s*\z", RegexOptions.Compiled)

    let nameRegex = Regex(@"^([^=:;#\s\[\]]+)\z", RegexOptions.Compiled)

    let inline (|ParseRegex|_|) (regex: Regex) input =
        match regex.Match(input) with
        | m when m.Success -> List.tail [ for g in m.Groups -> g.Value ] |> ValueSome
        | _ -> ValueNone

type Section = { Name: string }

type ParameterLine = { Key: string; Value: string }

type Line =
    | Whitespace
    | Comment
    | Section of Section: Section
    | Parameter of Parameter: ParameterLine

type ParserError =
    | InvalidName of Name: string
    | InvalidLine of Line: string
    | InvalidValue of Value: string

[<CustomComparison; CustomEquality>]
type Name =
    private
        { Name: string }

    static let comparer = StringComparer.OrdinalIgnoreCase

    static let empty = { Name = Empty }

    static member Empty = empty

    static member TryGet name =
        match name with
        | Empty -> empty |> Ok
        | ParseRegex nameRegex [ name ] -> { Name = name } |> Ok
        | _ -> InvalidName name |> Error

    static member Get name =
        match name with
        | Empty -> empty
        | ParseRegex nameRegex [ name ] -> { Name = name }
        | _ -> invalidArg (nameof name) name

    static member op_Implicit name = Name.Get name

    static member op_Implicit(name: Name) = name.ToString()

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
    let empty = Name.Empty

    let tryGet name = Name.TryGet name

    let get name = Name.Get name

[<CustomComparison; CustomEquality>]
type Key =
    private
        { section: Name
          parameter: Name }

    static let create section parameter = { section = section; parameter = parameter }

    static member TryGet(sectionName, parameterName) =
        if parameterName = Empty then
            InvalidName parameterName |> Error
        else
            Name.TryGet sectionName
            >>= (fun section -> Name.TryGet parameterName >>= (fun parameter -> create section parameter |> Ok))

    static member Get(sectionName, parameterName) =
        if parameterName = Empty then
            invalidArg (nameof parameterName) parameterName
        else
            create (Name.Get sectionName) (Name.Get parameterName)

    static member TryParse key =
        match split ':' key with
        | [| parameterName |] -> Key.TryGet(Name.Empty, parameterName)
        | [| sectionName; parameterName |] -> Key.TryGet(sectionName, parameterName)
        | _ -> InvalidName key |> Error

    static member Parse key =
        match split ':' key with
        | [| parameterName |] -> Key.Get(Name.Empty, parameterName)
        | [| sectionName; parameterName |] -> Key.Get(sectionName, parameterName)
        | _ -> invalidArg (nameof key) key

    static member op_Implicit key = Key.Parse key

    static member op_Implicit(key: Key) = key.ToString()

    member k.Section = k.section

    member k.Parameter = k.parameter

    member k.CompareTo other =
        match k.section.CompareTo other.section with
        | 0 -> k.parameter.CompareTo other.parameter
        | result -> result

    interface IComparable with
        member k.CompareTo obj =
            match obj with
            | :? Key as other -> k.CompareTo other
            | _ -> invalidArg (nameof obj) "Object is not a Key."

    override k.Equals obj =
        match obj with
        | :? Key as other -> k.section.Equals other.section && k.parameter.Equals other.parameter
        | _ -> false

    override k.GetHashCode() =
        let first = k.section.GetHashCode()
        let second = k.parameter.GetHashCode()
        ((first <<< 5) + first) ^^^ second

    override k.ToString() = k.section.Name + ":" + k.parameter.Name

[<RequireQualifiedAccess>]
module Key =
    let tryGet sectionName parameterName = Key.TryGet(sectionName, parameterName)

    let get sectionName parameterName = Key.Get(sectionName, parameterName)

    let tryParse key = Key.TryParse key

    let parse key = Key.Parse key

    let section key = key.section

    let parameter key = key.parameter

[<RequireQualifiedAccess>]
module Map =
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
                loop (Map.add key value map)

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
                        loop section (Map.add { section = { Name = section }; parameter = { Name = key } } value map)

        loop Empty map

    let add key value map = map |> Map.add key value

    let tryFind key map = map |> Map.tryFind key

#if NET
    let tryFindAs key map = map |> tryFind key <?> tryParseAs
#endif

    let tryFindWith parser key map = map |> tryFind key <?> tryParseWith parser

    let tryFindNested key map =
        let rec loop section =
            match Map.tryFind { key with section = section } map with
            | Some value -> Some value
            | None ->
                match section with
                | { Name = Empty } -> None
                | _ ->
                    match lastIndexOf '.' section.Name with
                    | -1 -> loop { Name = Empty }
                    | index -> loop { Name = section.Name[.. index - 1] }

        loop key.section

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

    let sections map = map |> Seq.map key |> Seq.map _.section |> Seq.distinct

    let sectionWith sign (section: Name) map =
        Map.filter (fun key _ -> key.section.Equals section |> sign) map

    let section = sectionWith id

    let removeSection = sectionWith not

    let toLines (map: Map<Key, string>) =
        seq {
            let mutable written = false
            let mutable section = Name.empty

            for pair in map do
                let name = pair.Key.section

                if name <> Name.empty && name.Equals(section) |> not then
                    section <- name

                    if written then
                        yield Empty

                    yield $"[{name}]"

                yield
                    if pair.Value = Empty then
                        $"{pair.Key.parameter} ="
                    else
                        $"{pair.Key.parameter} = {pair.Value}"

                written <- true
        }

    let toFile path map = toLines map |> writeLines path

    let toString map = toLines map |> String.concat Environment.NewLine

type Ini =
    private
        { Map: Map<Key, string> }

    member ini.KeyValuePairs = ini.Map :> KeyValuePair<Key, string> seq

    interface IEnumerable<KeyValuePair<Key, string>> with
        member ini.GetEnumerator() = ini.KeyValuePairs.GetEnumerator()

    interface IEnumerable with
        member ini.GetEnumerator() = (ini.KeyValuePairs :> IEnumerable).GetEnumerator()

    override ini.ToString() = ini.Map |> Map.toString

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

    static member Create pairs = Ini.ofSeq (Seq.map deconstruct pairs)

    static member FromLines lines = Ini.fromLines lines

    static member FromFile path = Ini.fromFile path

    member ini.Append pairs = Ini.append (Seq.map deconstruct pairs) ini

    member ini.AppendLines lines = Ini.appendLines lines ini

    member ini.AppendFile path = Ini.appendFile path ini

    member ini.IsEmpty = Ini.isEmpty ini

    member ini.Add(key, value) = Ini.add key value ini

    member ini.TryFind key = Ini.tryFind key ini

#if NET
    member ini.TryFindAs key = Ini.tryFindAs key ini
#endif

    member ini.TryFindWith(parser, key) = Ini.tryFindWith (fromFunc parser) key ini

    member ini.TryFindNested key = Ini.tryFindNested key ini

#if NET
    member ini.TryFindNestedAs key = Ini.tryFindNestedAs key ini
#endif

    member ini.TryFindNestedWith(parser, key) = Ini.tryFindNestedWith (fromFunc parser) key ini

    member ini.Find key = Ini.find key ini

#if NET
    member ini.FindAs key = Ini.findAs key ini
#endif

    member ini.FindWith(parser, key) = Ini.findWith (fromFunc parser) key ini

    member ini.FindNested key = Ini.findNested key ini

#if NET
    member ini.FindNestedAs key = Ini.findNestedAs key ini
#endif

    member ini.FindNestedWith(parser, key) = Ini.findNestedWith (fromFunc parser) key ini

    member ini.Remove key = Ini.remove key ini

    member ini.Count = Ini.count ini

    member ini.ContainsKey key = Ini.containsKey key ini

    member ini.Keys = Ini.keys ini

    member ini.Values = Ini.values ini

    member ini.Sections = Ini.sections ini

    member ini.Section name = Ini.section name ini

    member ini.RemoveSection name = Ini.removeSection name ini

    member ini.ToLines() = Ini.toLines ini

    member ini.ToFile path = Ini.toFile path ini
