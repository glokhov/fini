namespace Fini

open System
open System.Collections
open System.Collections.Generic
open System.Diagnostics.CodeAnalysis
open System.IO
open System.Runtime.InteropServices
open System.Text.RegularExpressions

[<AutoOpen>]
module private Guards =
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

[<AutoOpen>]
module private Enumerator =
    let inline getEnumerator (s: 'T seq) = s.GetEnumerator()

    let inline moveNext (e: IEnumerator<'T>) = e.MoveNext()

    let inline current (e: IEnumerator<'T>) = e.Current

[<AutoOpen>]
module private String =
    let inline indexOf (c: char) (s: string) = s.IndexOf c

    let inline lastIndexOf (c: char) (s: string) = s.LastIndexOf c

[<AutoOpen>]
module private File =
    let inline readLines path = File.ReadLines path

    let inline writeLines path (lines: string seq) = File.WriteAllLines(path, lines)

type private Comment = string

[<CustomComparison; CustomEquality>]
type private Section =
    { Name: string }

    static let comparer = StringComparer.OrdinalIgnoreCase

    static member Comparer = comparer

    static member Compare(x: Section, y: Section) =
        match x.Name, y.Name with
        | "", "" -> 0
        | "", _ -> -1
        | _, "" -> 1
        | _ -> comparer.Compare(x.Name, y.Name)

    interface IComparable with
        member m.CompareTo(obj) =
            match obj with
            | :? Section as other -> Section.Compare(m, other)
            | _ -> invalidArg "obj" "Object is not a Name."

    override m.Equals(obj) =
        match obj with
        | :? Section as other -> comparer.Equals(m.Name, other.Name)
        | _ -> false

    override m.GetHashCode() = comparer.GetHashCode(m.Name)

type private Parameter = { Key: string; Value: string }

type private Line =
    | Whitespace
    | Comment of Comment: Comment
    | Section of Section: Section
    | Parameter of Parameter: Parameter

[<CustomComparison; CustomEquality>]
type private Key =
    private
        { Section: Section
          Parameter: string }

    static let comparer = StringComparer.OrdinalIgnoreCase

    member m.Path = m.Section.Name + ":" + m.Parameter

    interface IComparable with
        member m.CompareTo(obj) =
            match obj with
            | :? Key as other ->
                match Section.Compare(m.Section, other.Section) with
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
        match key |> requireNonNull (nameof key) |> indexOf ':' with
        | -1 -> { Section = { Name = "" }; Parameter = key }
        | index ->
            { Section = { Name = key[.. index - 1] }
              Parameter = key[index + 1 ..] }

[<AutoOpen>]
module private KeyValuePair =
    let inline stringKey (pair: KeyValuePair<Key, _>) = KeyValuePair<string, _>(pair.Key.Path, pair.Value)

    let inline toTuple (pair: KeyValuePair<Key, _>) = pair.Key, pair.Value

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
        | ParseRegex parameterKeyRegex [ parameter ] -> ValueSome { Section = { Name = "" }; Parameter = parameter }
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

/// <summary>
/// An immutable INI document: a flattened map from <c>section:parameter</c> keys to values.
/// </summary>
///
/// <remarks>
/// <para>
/// Every operation that changes a document returns a new one; the original is never modified. A key with
/// no colon refers to the root section, so <c>x</c> and <c>:x</c> are the same key. Both halves of a key
/// are compared with <see cref="P:System.StringComparer.OrdinalIgnoreCase"/>; values are kept and compared
/// as written.
/// </para>
/// <para>
/// Keys and values enumerate in key order: the root section first, then sections ordinal-ignore-case with
/// a parent ahead of its dotted children, then parameters within a section.
/// </para>
/// <para>
/// Two documents are equal when they hold the same keys and the same values. Section and parameter name
/// case, declaration order, comments and blank lines are not taken into account.
/// </para>
/// </remarks>
[<StructuralComparison; StructuralEquality>]
type Ini =
    private
        { Map: Map<Key, string> }

    static let empty = { Map = Map.empty }

    /// <summary>The empty document.</summary>
    static member Empty = empty

    /// <summary>Builds a document from a sequence of key-value pairs.</summary>
    ///
    /// <param name="pairs">The pairs to add, each keyed <c>section:parameter</c>. A later pair overwrites an
    /// earlier one with the same key.</param>
    ///
    /// <returns><c>Ok</c> of the document, or <c>Error "Invalid key: &lt;key&gt;."</c> for the first key the
    /// parser would reject.</returns>
    ///
    /// <exception cref="T:System.ArgumentNullException">
    /// <paramref name="pairs"/>, or the key or the value of any pair in it, is null.
    /// </exception>
    static member OfSeq(pairs) =
        use enumerator = pairs |> requireNonNullSeq (nameof pairs) |> getEnumerator

        let rec loop map =
            if moveNext enumerator |> not then
                Ok { Map = map }
            else
                match current enumerator |> requireNonNullPair (nameof pairs) |> tryParseKeyValue with
                | Error err -> Error err
                | Ok(key, value) -> loop (Map.add key value map)

        loop empty.Map

    /// <summary>Parses the lines of a file.</summary>
    ///
    /// <param name="path">The path of the file to read.</param>
    ///
    /// <returns><c>Ok</c> of the document, or <c>Error "Cannot parse line: &lt;line&gt;."</c> for the first
    /// unparsable line.</returns>
    ///
    /// <remarks>Reads lazily and stops at the first unparsable line, closing the file handle on the way
    /// out.</remarks>
    ///
    /// <exception cref="T:System.ArgumentNullException"><paramref name="path"/> is null.</exception>
    /// <exception cref="T:System.IO.IOException">The file cannot be read; the exceptions of
    /// <see cref="M:System.IO.File.ReadLines(System.String)"/> propagate unchanged.</exception>
    static member FromFile(path) = path |> readLines |> empty.AppendLines

    /// <summary>Parses a sequence of lines.</summary>
    ///
    /// <param name="lines">The lines to parse. Parameters before the first <c>[section]</c> header belong to
    /// the root section; a later parameter overwrites an earlier one with the same key.</param>
    ///
    /// <returns><c>Ok</c> of the document, or <c>Error "Cannot parse line: &lt;line&gt;."</c> for the first
    /// unparsable line.</returns>
    ///
    /// <remarks>Reads the sequence lazily and stops at the first unparsable line.</remarks>
    ///
    /// <exception cref="T:System.ArgumentNullException">
    /// <paramref name="lines"/>, or any line in it, is null.
    /// </exception>
    static member FromLines(lines) =
        lines |> requireNonNullSeq (nameof lines) |> empty.AppendLines

    /// <summary>Layers the lines of a file over this document.</summary>
    ///
    /// <param name="path">The path of the file to read.</param>
    ///
    /// <returns><c>Ok</c> of a new document, or <c>Error "Cannot parse line: &lt;line&gt;."</c> for the first
    /// unparsable line.</returns>
    ///
    /// <remarks>The file wins on a shared key; keys only present in this document survive. The file is
    /// parsed from the root section, by exactly the rules <see cref="M:Fini.Ini.FromFile(System.String)"/>
    /// uses.</remarks>
    ///
    /// <exception cref="T:System.ArgumentNullException"><paramref name="path"/> is null.</exception>
    /// <exception cref="T:System.IO.IOException">The file cannot be read; the exceptions of
    /// <see cref="M:System.IO.File.ReadLines(System.String)"/> propagate unchanged.</exception>
    member m.AppendFile(path) = path |> readLines |> m.AppendLines

    /// <summary>Layers a sequence of lines over this document.</summary>
    ///
    /// <param name="lines">The lines to parse.</param>
    ///
    /// <returns><c>Ok</c> of a new document, or <c>Error "Cannot parse line: &lt;line&gt;."</c> for the first
    /// unparsable line.</returns>
    ///
    /// <remarks>The appended lines win on a shared key; keys only present in this document survive. Parsing
    /// starts at the root section, so a line's key depends on the appended lines alone and never on the
    /// sections already present: appending <c>port = 9090</c> lands at <c>:port</c>.</remarks>
    ///
    /// <exception cref="T:System.ArgumentNullException">
    /// <paramref name="lines"/>, or any line in it, is null.
    /// </exception>
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
                        loop (Map.add { Section = { Name = section }; Parameter = key } value map) section

        loop m.Map ""

    /// <summary>True if the document holds no parameters.</summary>
    member m.IsEmpty = m.Map.IsEmpty

    /// <summary>Adds a parameter, overwriting any parameter already under the same key.</summary>
    ///
    /// <param name="key">The key to write, as <c>section:parameter</c> or <c>parameter</c> for the root
    /// section. It is not trimmed or repaired.</param>
    /// <param name="value">The value to write. A blank value is stored verbatim.</param>
    ///
    /// <returns><c>Ok</c> of a new document, or <c>Error "Invalid key: &lt;key&gt;."</c> if the parser would
    /// reject <paramref name="key"/>.</returns>
    ///
    /// <exception cref="T:System.ArgumentNullException">
    /// <paramref name="key"/> or <paramref name="value"/> is null.
    /// </exception>
    member m.Add(key, value) =
        match key |> requireNonNull (nameof key) |> tryParseKey with
        | Ok key -> Ok { Map = Map.add key (value |> requireNonNull (nameof value)) m.Map }
        | Error err -> Error err

    member internal m.TryFindInternal(key) = m.Map.TryFind(key |> Key.create)

    /// <summary>Looks up an exact key.</summary>
    ///
    /// <param name="key">The key to find, as <c>section:parameter</c> or <c>parameter</c> for the root
    /// section.</param>
    /// <param name="value">When this method returns, the value found, or null if there is none.</param>
    ///
    /// <returns>True if the key is present.</returns>
    ///
    /// <remarks>Does not walk the section hierarchy; see
    /// <see cref="M:Fini.Ini.TryFindNested(System.String,System.String@)"/>.</remarks>
    ///
    /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is null.</exception>
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
            match Map.tryFind { Section = section; Parameter = key.Parameter } m.Map with
            | Some value -> Some value
            | None ->
                match section.Name with
                | "" -> None
                | _ ->
                    match lastIndexOf '.' section.Name with
                    | -1 -> loop { Name = "" }
                    | index -> loop { Name = section.Name[.. index - 1] }

        loop key.Section

    /// <summary>Looks up a key, walking up the section hierarchy.</summary>
    ///
    /// <param name="key">The key to find, as <c>section:parameter</c> or <c>parameter</c> for the root
    /// section.</param>
    /// <param name="value">When this method returns, the value found, or null if there is none.</param>
    ///
    /// <returns>True if the parameter is present in the given section or in one of its ancestors.</returns>
    ///
    /// <remarks>A dotted section name is read as a hierarchy: the given section is tried first, then each
    /// ancestor in turn, then the root. So <c>server.dev:host</c> falls back to <c>server:host</c> and then
    /// to <c>:host</c>.</remarks>
    ///
    /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is null.</exception>
    member m.TryFindNested(key, [<Out; MaybeNullWhen(false)>] value: byref<string>) =
        match key |> m.TryFindNestedInternal with
        | Some found ->
            value <- found
            true
        | None ->
            value <- Unchecked.defaultof<_>
            false

    /// <summary>Looks up an exact key, raising if it is absent.</summary>
    ///
    /// <param name="key">The key to find, as <c>section:parameter</c> or <c>parameter</c> for the root
    /// section.</param>
    ///
    /// <returns>The value found.</returns>
    ///
    /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is null.</exception>
    /// <exception cref="T:System.Collections.Generic.KeyNotFoundException">The key is absent.</exception>
    member m.Find(key) = Map.find (key |> Key.create) m.Map

    /// <summary>Looks up a key walking up the section hierarchy, raising if it is absent.</summary>
    ///
    /// <param name="key">The key to find, as <c>section:parameter</c> or <c>parameter</c> for the root
    /// section.</param>
    ///
    /// <returns>The value found in the given section or in the nearest ancestor that holds the
    /// parameter.</returns>
    ///
    /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is null.</exception>
    /// <exception cref="T:System.Collections.Generic.KeyNotFoundException">Neither the section nor any of its
    /// ancestors holds the parameter.</exception>
    member m.FindNested(key) =
        match key |> m.TryFindNestedInternal with
        | Some value -> value
        | None -> raise (KeyNotFoundException())

    /// <summary>Removes one key.</summary>
    ///
    /// <param name="key">The key to remove, as <c>section:parameter</c> or <c>parameter</c> for the root
    /// section.</param>
    ///
    /// <returns>A new document without the key, or a document equal to this one if the key is absent.</returns>
    ///
    /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is null.</exception>
    member m.Remove(key) = { Map = Map.remove (key |> Key.create) m.Map }

    /// <summary>The number of parameters in the document.</summary>
    member m.Count = m.Map.Count

    /// <summary>True if the document holds a parameter under the exact key.</summary>
    ///
    /// <param name="key">The key to test, as <c>section:parameter</c> or <c>parameter</c> for the root
    /// section.</param>
    ///
    /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is null.</exception>
    member m.ContainsKey key = Map.containsKey (key |> Key.create) m.Map

    /// <summary>The keys of the document, as <c>section:parameter</c>, in key order.</summary>
    member m.Keys = m.Map.Keys |> Seq.map _.Path

    /// <summary>The values of the document, aligned with <see cref="P:Fini.Ini.Keys"/>.</summary>
    member m.Values = m.Map.Values :> string seq

    /// <summary>The key-value pairs of the document, in key order.</summary>
    member m.KeyValuePairs = m.Map |> Seq.map stringKey

    /// <summary>The distinct section names of the document, in key order, with the root section as the empty
    /// string.</summary>
    ///
    /// <remarks>Names differing only in case count as one, and a dotted child section appears under its own
    /// full name.</remarks>
    member m.Sections = m.Map |> Seq.map _.Key.Section |> Seq.distinct |> Seq.map _.Name

    /// <summary>One section as a standalone document.</summary>
    ///
    /// <param name="name">The section name, matched case-insensitively, or the empty string for the root
    /// section.</param>
    ///
    /// <returns>A document holding the parameters of that section, keyed as in this document, or the empty
    /// document if the section holds none.</returns>
    ///
    /// <remarks>Dotted child sections are excluded: <c>Section "alpha"</c> does not include
    /// <c>alpha.beta</c>.</remarks>
    member m.Section name =
        let predicate (pair: KeyValuePair<Key, string>) = Section.Comparer.Equals(pair.Key.Section.Name, name)
        { Map = m.Map |> Seq.filter predicate |> Seq.map toTuple |> Map.ofSeq }

    /// <summary>Renders the document as lines.</summary>
    ///
    /// <returns>The root parameters first with no header, then one <c>[section]</c> block per section
    /// separated by a blank line.</returns>
    ///
    /// <remarks>The output is normalised, not a copy of the input: comments, blank lines, original ordering
    /// and duplicate entries are not preserved. Values are, so rendering and parsing round-trip. An empty
    /// value renders as <c>x =</c>.</remarks>
    member m.ToLines() =
        seq {
            let mutable written = false
            let mutable section = ""

            for pair in m.Map do
                let name = pair.Key.Section.Name

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

    /// <summary>Renders the document to a file, overwriting it if it exists.</summary>
    ///
    /// <param name="path">The path of the file to write.</param>
    ///
    /// <exception cref="T:System.ArgumentNullException"><paramref name="path"/> is null.</exception>
    /// <exception cref="T:System.IO.IOException">The file cannot be written; the exceptions of
    /// <see cref="M:System.IO.File.WriteAllLines(System.String,System.Collections.Generic.IEnumerable{System.String})"/>
    /// propagate unchanged.</exception>
    member m.ToFile(path) = m.ToLines() |> writeLines path

    interface IEnumerable<KeyValuePair<string, string>> with
        member m.GetEnumerator() = m.KeyValuePairs.GetEnumerator()

    interface IEnumerable with
        member m.GetEnumerator() = (m.KeyValuePairs :> IEnumerable).GetEnumerator()

    /// <summary>Renders the document, joining the lines with <c>\n</c>.</summary>
    override m.ToString() = m.ToLines() |> String.concat "\n"

/// <summary>Functional operations on <see cref="T:Fini.Ini"/>.</summary>
///
/// <remarks>The document is always the last parameter, so the functions compose with <c>|&gt;</c>.</remarks>
[<RequireQualifiedAccess>]
module Ini =
    /// <summary>The empty document.</summary>
    let empty = Ini.Empty

    /// <summary>Builds a document from a sequence of key-value pairs.</summary>
    ///
    /// <param name="pairs">The pairs to add, each keyed <c>section:parameter</c>. A later pair overwrites an
    /// earlier one with the same key.</param>
    ///
    /// <returns><c>Ok</c> of the document, or <c>Error "Invalid key: &lt;key&gt;."</c> for the first key the
    /// parser would reject.</returns>
    ///
    /// <exception cref="T:System.ArgumentNullException">
    /// <paramref name="pairs"/>, or the key or the value of any pair in it, is null.
    /// </exception>
    let ofSeq pairs = pairs |> Ini.OfSeq

    /// <summary>Parses the lines of a file.</summary>
    ///
    /// <param name="path">The path of the file to read.</param>
    ///
    /// <returns><c>Ok</c> of the document, or <c>Error "Cannot parse line: &lt;line&gt;."</c> for the first
    /// unparsable line.</returns>
    ///
    /// <remarks>Reads lazily and stops at the first unparsable line, closing the file handle on the way
    /// out.</remarks>
    ///
    /// <exception cref="T:System.ArgumentNullException"><paramref name="path"/> is null.</exception>
    /// <exception cref="T:System.IO.IOException">The file cannot be read; the exceptions of
    /// <see cref="M:System.IO.File.ReadLines(System.String)"/> propagate unchanged.</exception>
    let fromFile path = Ini.FromFile path

    /// <summary>Parses a sequence of lines.</summary>
    ///
    /// <param name="lines">The lines to parse. Parameters before the first <c>[section]</c> header belong to
    /// the root section; a later parameter overwrites an earlier one with the same key.</param>
    ///
    /// <returns><c>Ok</c> of the document, or <c>Error "Cannot parse line: &lt;line&gt;."</c> for the first
    /// unparsable line.</returns>
    ///
    /// <remarks>Reads the sequence lazily and stops at the first unparsable line.</remarks>
    ///
    /// <exception cref="T:System.ArgumentNullException">
    /// <paramref name="lines"/>, or any line in it, is null.
    /// </exception>
    let fromLines lines = Ini.FromLines lines

    /// <summary>Layers the lines of a file over a document.</summary>
    ///
    /// <param name="path">The path of the file to read.</param>
    /// <param name="ini">The document to layer the file over.</param>
    ///
    /// <returns><c>Ok</c> of a new document, or <c>Error "Cannot parse line: &lt;line&gt;."</c> for the first
    /// unparsable line.</returns>
    ///
    /// <remarks>The file wins on a shared key; keys only present in <paramref name="ini"/> survive. The file
    /// is parsed from the root section, by exactly the rules <c>fromFile</c> uses.</remarks>
    ///
    /// <exception cref="T:System.ArgumentNullException"><paramref name="path"/> is null.</exception>
    /// <exception cref="T:System.IO.IOException">The file cannot be read; the exceptions of
    /// <see cref="M:System.IO.File.ReadLines(System.String)"/> propagate unchanged.</exception>
    let appendFile path (ini: Ini) = ini.AppendFile path

    /// <summary>Layers a sequence of lines over a document.</summary>
    ///
    /// <param name="lines">The lines to parse.</param>
    /// <param name="ini">The document to layer the lines over.</param>
    ///
    /// <returns><c>Ok</c> of a new document, or <c>Error "Cannot parse line: &lt;line&gt;."</c> for the first
    /// unparsable line.</returns>
    ///
    /// <remarks>The appended lines win on a shared key; keys only present in <paramref name="ini"/> survive.
    /// Parsing starts at the root section, so a line's key depends on <paramref name="lines"/> alone and
    /// never on the sections already present: appending <c>port = 9090</c> lands at <c>:port</c>. Hence
    /// <c>Ini.empty |&gt; Ini.appendLines lines</c> is <c>Ini.fromLines lines</c>.</remarks>
    ///
    /// <exception cref="T:System.ArgumentNullException">
    /// <paramref name="lines"/>, or any line in it, is null.
    /// </exception>
    let appendLines lines (ini: Ini) = ini.AppendLines lines

    /// <summary>True if the document holds no parameters.</summary>
    ///
    /// <param name="ini">The document to test.</param>
    let isEmpty (ini: Ini) = ini.IsEmpty

    /// <summary>Adds a parameter, overwriting any parameter already under the same key.</summary>
    ///
    /// <param name="key">The key to write, as <c>section:parameter</c> or <c>parameter</c> for the root
    /// section. It is not trimmed or repaired.</param>
    /// <param name="value">The value to write. A blank value is stored verbatim.</param>
    /// <param name="ini">The document to add to.</param>
    ///
    /// <returns><c>Ok</c> of a new document, or <c>Error "Invalid key: &lt;key&gt;."</c> if the parser would
    /// reject <paramref name="key"/>.</returns>
    ///
    /// <exception cref="T:System.ArgumentNullException">
    /// <paramref name="key"/> or <paramref name="value"/> is null.
    /// </exception>
    let add key value (ini: Ini) = ini.Add(key, value)

    /// <summary>Looks up an exact key.</summary>
    ///
    /// <param name="key">The key to find, as <c>section:parameter</c> or <c>parameter</c> for the root
    /// section.</param>
    /// <param name="ini">The document to search.</param>
    ///
    /// <returns><c>Some</c> of the value, or <c>None</c> if the key is absent.</returns>
    ///
    /// <remarks>Does not walk the section hierarchy; see <c>tryFindNested</c>.</remarks>
    ///
    /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is null.</exception>
    let tryFind key (ini: Ini) = ini.TryFindInternal key

    /// <summary>Looks up a key, walking up the section hierarchy.</summary>
    ///
    /// <param name="key">The key to find, as <c>section:parameter</c> or <c>parameter</c> for the root
    /// section.</param>
    /// <param name="ini">The document to search.</param>
    ///
    /// <returns><c>Some</c> of the value found in the given section or in the nearest ancestor that holds the
    /// parameter, or <c>None</c> if none does.</returns>
    ///
    /// <remarks>A dotted section name is read as a hierarchy: the given section is tried first, then each
    /// ancestor in turn, then the root. So <c>server.dev:host</c> falls back to <c>server:host</c> and then
    /// to <c>:host</c>.</remarks>
    ///
    /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is null.</exception>
    let tryFindNested key (ini: Ini) = ini.TryFindNestedInternal key

    /// <summary>Looks up an exact key, raising if it is absent.</summary>
    ///
    /// <param name="key">The key to find, as <c>section:parameter</c> or <c>parameter</c> for the root
    /// section.</param>
    /// <param name="ini">The document to search.</param>
    ///
    /// <returns>The value found.</returns>
    ///
    /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is null.</exception>
    /// <exception cref="T:System.Collections.Generic.KeyNotFoundException">The key is absent.</exception>
    let find key (ini: Ini) = ini.Find key

    /// <summary>Looks up a key walking up the section hierarchy, raising if it is absent.</summary>
    ///
    /// <param name="key">The key to find, as <c>section:parameter</c> or <c>parameter</c> for the root
    /// section.</param>
    /// <param name="ini">The document to search.</param>
    ///
    /// <returns>The value found in the given section or in the nearest ancestor that holds the
    /// parameter.</returns>
    ///
    /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is null.</exception>
    /// <exception cref="T:System.Collections.Generic.KeyNotFoundException">Neither the section nor any of its
    /// ancestors holds the parameter.</exception>
    let findNested key (ini: Ini) = ini.FindNested key

    /// <summary>Removes one key.</summary>
    ///
    /// <param name="key">The key to remove, as <c>section:parameter</c> or <c>parameter</c> for the root
    /// section.</param>
    /// <param name="ini">The document to remove from.</param>
    ///
    /// <returns>A new document without the key, or a document equal to <paramref name="ini"/> if the key is
    /// absent.</returns>
    ///
    /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is null.</exception>
    let remove key (ini: Ini) = ini.Remove key

    /// <summary>The number of parameters in the document.</summary>
    ///
    /// <param name="ini">The document to count.</param>
    let count (ini: Ini) = ini.Count

    /// <summary>True if the document holds a parameter under the exact key.</summary>
    ///
    /// <param name="key">The key to test, as <c>section:parameter</c> or <c>parameter</c> for the root
    /// section.</param>
    /// <param name="ini">The document to search.</param>
    ///
    /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is null.</exception>
    let containsKey key (ini: Ini) = ini.ContainsKey key

    /// <summary>The keys of the document, as <c>section:parameter</c>, in key order.</summary>
    ///
    /// <param name="ini">The document to enumerate.</param>
    let keys (ini: Ini) = ini.Keys

    /// <summary>The values of the document, aligned with <c>keys</c>.</summary>
    ///
    /// <param name="ini">The document to enumerate.</param>
    let values (ini: Ini) = ini.Values

    /// <summary>The distinct section names of the document, in key order, with the root section as the empty
    /// string.</summary>
    ///
    /// <param name="ini">The document to enumerate.</param>
    ///
    /// <remarks>Names differing only in case count as one, and a dotted child section appears under its own
    /// full name.</remarks>
    let sections (ini: Ini) = ini.Sections

    /// <summary>One section as a standalone document.</summary>
    ///
    /// <param name="name">The section name, matched case-insensitively, or the empty string for the root
    /// section.</param>
    /// <param name="ini">The document to take the section from.</param>
    ///
    /// <returns>A document holding the parameters of that section, keyed as in <paramref name="ini"/>, or the
    /// empty document if the section holds none.</returns>
    ///
    /// <remarks>Dotted child sections are excluded: <c>section "alpha"</c> does not include
    /// <c>alpha.beta</c>.</remarks>
    let section name (ini: Ini) = ini.Section name

    /// <summary>The key-value pairs of the document, in key order.</summary>
    ///
    /// <param name="ini">The document to enumerate.</param>
    let toSeq (ini: Ini) = ini.KeyValuePairs

    /// <summary>Renders the document as lines.</summary>
    ///
    /// <param name="ini">The document to render.</param>
    ///
    /// <returns>The root parameters first with no header, then one <c>[section]</c> block per section
    /// separated by a blank line.</returns>
    ///
    /// <remarks>The output is normalised, not a copy of the input: comments, blank lines, original ordering
    /// and duplicate entries are not preserved. Values are, so rendering and parsing round-trip. An empty
    /// value renders as <c>x =</c>.</remarks>
    let toLines (ini: Ini) = ini.ToLines()

    /// <summary>Renders the document to a file, overwriting it if it exists.</summary>
    ///
    /// <param name="path">The path of the file to write.</param>
    /// <param name="ini">The document to render.</param>
    ///
    /// <exception cref="T:System.ArgumentNullException"><paramref name="path"/> is null.</exception>
    /// <exception cref="T:System.IO.IOException">The file cannot be written; the exceptions of
    /// <see cref="M:System.IO.File.WriteAllLines(System.String,System.Collections.Generic.IEnumerable{System.String})"/>
    /// propagate unchanged.</exception>
    let toFile path (ini: Ini) = ini.ToFile path

    /// <summary>Renders the document, joining the lines with <c>\n</c>.</summary>
    ///
    /// <param name="ini">The document to render.</param>
    let toString (ini: Ini) = ini.ToString()

