namespace Fini

open System
open System.Collections.Generic

/// <summary>A problem with the content of a name, a line or a value.</summary>
/// <remarks>Each case holds the exact text that caused the problem.</remarks>
type ParserError =
    /// <summary>A section or parameter name, or a key, is not valid.</summary>
    | InvalidName of Name: string
    /// <summary>A line is not a section header, a parameter, a comment or blank.</summary>
    | InvalidLine of Line: string
    /// <summary>A value could not be converted to the requested type.</summary>
    | InvalidValue of Value: string

/// <summary>A section or parameter name.</summary>
/// <remarks>
/// A name may not be empty or contain whitespace or any of <c>= : ; # [ ]</c>. The one exception is
/// <see cref="P:Fini.Name.Empty"/>, the name of the root section. Names are compared case-insensitively
/// (<see cref="F:System.StringComparison.OrdinalIgnoreCase"/>) but keep their spelling. They are never trimmed or
/// corrected. The root name sorts before every other name.
/// </remarks>
[<Sealed>]
type Name =
    /// <summary>The empty name of the root section.</summary>
    static member Empty: Name

    /// <summary>Creates a name, returning an error if it is invalid.</summary>
    /// <param name="name">The name. An empty string gives the root name.</param>
    /// <returns><c>Ok name</c>, or <c>Error (InvalidName name)</c> if the name is invalid.</returns>
    static member TryGet: name: string -> Result<Name, ParserError>

    /// <summary>Creates a name, throwing if it is invalid.</summary>
    /// <param name="name">The name. An empty string gives the root name.</param>
    /// <returns>The name.</returns>
    /// <exception cref="T:System.ArgumentException">The name is invalid.</exception>
    static member Get: name: string -> Name

    /// <summary>Converts a string to a name, as <see cref="M:Fini.Name.Get(System.String)"/> does.</summary>
    /// <param name="name">The name.</param>
    /// <exception cref="T:System.ArgumentException">The name is invalid.</exception>
    static member op_Implicit: name: string -> Name

    /// <summary>Converts a name to its text.</summary>
    /// <param name="name">The name.</param>
    static member op_Implicit: name: Name -> string

    /// <summary>Returns the name as it was spelled, or an empty string for the root name.</summary>
    override ToString: unit -> string

    interface IComparable

/// <summary>Functions for creating section and parameter names.</summary>
[<RequireQualifiedAccess>]
module Name =
    /// <summary>The empty name of the root section.</summary>
    val empty: Name

    /// <summary>Creates a name, returning an error if it is invalid.</summary>
    /// <param name="name">The name. An empty string gives the root name.</param>
    /// <returns><c>Ok name</c>, or <c>Error (InvalidName name)</c> if the name is invalid.</returns>
    val tryGet: name: string -> Result<Name, ParserError>

    /// <summary>Creates a name, throwing if it is invalid.</summary>
    /// <param name="name">The name. An empty string gives the root name.</param>
    /// <returns>The name.</returns>
    /// <exception cref="T:System.ArgumentException">The name is invalid.</exception>
    val get: name: string -> Name

/// <summary>The key of a value: a section name and a parameter name, written <c>section:parameter</c>.</summary>
/// <remarks>
/// The parameter name can't be empty. An empty section name means the root section, so <c>x</c> and <c>:x</c> are
/// the same key, and both are written <c>:x</c>. Keys are compared case-insensitively but keep their spelling.
/// They are ordered by section name, then by parameter name.
/// </remarks>
[<Sealed>]
type Key =
    /// <summary>Creates a key from a section name and a parameter name, returning an error if either is invalid.</summary>
    /// <param name="sectionName">The section name. An empty string means the root section.</param>
    /// <param name="parameterName">The parameter name. It can't be empty.</param>
    /// <returns><c>Ok key</c>, or <c>Error (InvalidName name)</c> with the invalid name.</returns>
    static member TryGet: sectionName: string * parameterName: string -> Result<Key, ParserError>

    /// <summary>Creates a key from a section name and a parameter name, throwing if either is invalid.</summary>
    /// <param name="sectionName">The section name. An empty string means the root section.</param>
    /// <param name="parameterName">The parameter name. It can't be empty.</param>
    /// <returns>The key.</returns>
    /// <exception cref="T:System.ArgumentException">A name is invalid.</exception>
    static member Get: sectionName: string * parameterName: string -> Key

    /// <summary>Parses a key written <c>section:parameter</c> or <c>parameter</c>, returning an error if it is invalid.</summary>
    /// <param name="key">The key. Without a colon, the key is in the root section.</param>
    /// <returns>
    /// <c>Ok key</c>, or <c>Error (InvalidName name)</c> with the invalid name, or with the whole key if it has
    /// more than one colon.
    /// </returns>
    static member TryParse: key: string -> Result<Key, ParserError>

    /// <summary>Parses a key written <c>section:parameter</c> or <c>parameter</c>, throwing if it is invalid.</summary>
    /// <param name="key">The key. Without a colon, the key is in the root section.</param>
    /// <returns>The key.</returns>
    /// <exception cref="T:System.ArgumentException">The key is invalid.</exception>
    static member Parse: key: string -> Key

    /// <summary>Converts a string to a key, as <see cref="M:Fini.Key.Parse(System.String)"/> does.</summary>
    /// <param name="key">The key.</param>
    /// <exception cref="T:System.ArgumentException">The key is invalid.</exception>
    static member op_Implicit: key: string -> Key

    /// <summary>Converts a key to its text, <c>section:parameter</c>.</summary>
    /// <param name="key">The key.</param>
    static member op_Implicit: key: Key -> string

    /// <summary>Returns the key as <c>section:parameter</c>, or <c>:parameter</c> in the root section.</summary>
    override ToString: unit -> string

    interface IComparable

/// <summary>Functions for creating keys.</summary>
[<RequireQualifiedAccess>]
module Key =
    /// <summary>Creates a key from a section name and a parameter name, returning an error if either is invalid.</summary>
    /// <param name="sectionName">The section name. An empty string means the root section.</param>
    /// <param name="parameterName">The parameter name. It can't be empty.</param>
    /// <returns><c>Ok key</c>, or <c>Error (InvalidName name)</c> with the invalid name.</returns>
    val tryGet: sectionName: string -> parameterName: string -> Result<Key, ParserError>

    /// <summary>Creates a key from a section name and a parameter name, throwing if either is invalid.</summary>
    /// <param name="sectionName">The section name. An empty string means the root section.</param>
    /// <param name="parameterName">The parameter name. It can't be empty.</param>
    /// <returns>The key.</returns>
    /// <exception cref="T:System.ArgumentException">A name is invalid.</exception>
    val get: sectionName: string -> parameterName: string -> Key

    /// <summary>Parses a key written <c>section:parameter</c> or <c>parameter</c>, returning an error if it is invalid.</summary>
    /// <param name="key">The key. Without a colon, the key is in the root section.</param>
    /// <returns>
    /// <c>Ok key</c>, or <c>Error (InvalidName name)</c> with the invalid name, or with the whole key if it has
    /// more than one colon.
    /// </returns>
    val tryParse: key: string -> Result<Key, ParserError>

    /// <summary>Parses a key written <c>section:parameter</c> or <c>parameter</c>, throwing if it is invalid.</summary>
    /// <param name="key">The key. Without a colon, the key is in the root section.</param>
    /// <returns>The key.</returns>
    /// <exception cref="T:System.ArgumentException">The key is invalid.</exception>
    val parse: key: string -> Key

/// <summary>An immutable INI document: a flat, sorted map from keys to values.</summary>
/// <remarks>
/// Every operation that changes a document returns a new one. Two documents are equal if they have the same keys
/// and values; letter case in names, the order of entries, comments and blank lines don't matter. Enumerating a
/// document gives its key/value pairs in key order.
/// </remarks>
[<Sealed>]
type Ini =
    /// <summary>A document with no values.</summary>
    static member Empty: Ini

    /// <summary>Creates a document from key/value pairs. If a key appears twice, the later value wins.</summary>
    /// <param name="pairs">The pairs.</param>
    /// <returns>The new document.</returns>
    static member Create: pairs: KeyValuePair<Key, string> seq -> Ini

    /// <summary>Parses lines into a document.</summary>
    /// <param name="lines">The lines. They are read one at a time, and reading stops at the first invalid line.</param>
    /// <returns><c>Ok ini</c>, or <c>Error (InvalidLine line)</c> with the first invalid line.</returns>
    static member FromLines: lines: string seq -> Result<Ini, ParserError>

    /// <summary>Reads a file into a document.</summary>
    /// <param name="path">The path of the file.</param>
    /// <returns><c>Ok ini</c>, or <c>Error (InvalidLine line)</c> with the first invalid line.</returns>
    /// <exception cref="T:System.IO.IOException">The file can't be read.</exception>
    static member FromFile: path: string -> Result<Ini, ParserError>

    /// <summary>Adds key/value pairs on top of this document. If a key already exists, the new value wins.</summary>
    /// <param name="pairs">The pairs. A document can be passed to merge it into this one.</param>
    /// <returns>The new document.</returns>
    member Append: pairs: KeyValuePair<Key, string> seq -> Ini

    /// <summary>
    /// Parses lines on top of this document. If a key already exists, the new value wins. The lines start in the
    /// root section.
    /// </summary>
    /// <param name="lines">The lines. Reading stops at the first invalid line.</param>
    /// <returns><c>Ok ini</c>, or <c>Error (InvalidLine line)</c> with the first invalid line.</returns>
    member AppendLines: lines: string seq -> Result<Ini, ParserError>

    /// <summary>
    /// Reads a file on top of this document. If a key already exists, the new value wins. The file starts in the
    /// root section.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <returns><c>Ok ini</c>, or <c>Error (InvalidLine line)</c> with the first invalid line.</returns>
    /// <exception cref="T:System.IO.IOException">The file can't be read.</exception>
    member AppendFile: path: string -> Result<Ini, ParserError>

    /// <summary>Whether the document has no values.</summary>
    member IsEmpty: bool

    /// <summary>
    /// Adds a value, replacing an existing one. The key takes the spelling of <paramref name="key"/>.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value, stored exactly as given.</param>
    /// <returns>The new document.</returns>
    member Add: key: Key * value: string -> Ini

    /// <summary>Finds the value of a key.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The value, or <c>None</c> (<c>null</c> in C#) if the key is missing.</returns>
    member TryFind: key: Key -> string option

#if NET
    /// <summary>Finds the value of a key and parses it with <see cref="T:System.IParsable`1"/> and the invariant culture.</summary>
    /// <param name="key">The key.</param>
    /// <returns>
    /// <c>Some (Ok value)</c>, <c>Some (Error (InvalidValue text))</c> if the value can't be parsed, or <c>None</c>
    /// (<c>null</c> in C#) if the key is missing.
    /// </returns>
    member TryFindAs<'T when 'T :> IParsable<'T>> : key: Key -> Result<'T, ParserError> option
#endif

    /// <summary>Finds the value of a key and converts it with a parser.</summary>
    /// <param name="parser">The parser. Any exception it throws counts as a parse failure. It isn't called for a missing key.</param>
    /// <param name="key">The key.</param>
    /// <returns>
    /// <c>Some (Ok value)</c>, <c>Some (Error (InvalidValue text))</c> if the parser throws, or <c>None</c>
    /// (<c>null</c> in C#) if the key is missing.
    /// </returns>
    member TryFindWith: parser: Func<string, 'T> * key: Key -> Result<'T, ParserError> option

    /// <summary>
    /// Finds the value of a key in its section or the nearest parent section: <c>a.b:x</c> looks in <c>a.b</c>,
    /// then <c>a</c>, then the root.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>The first value found, or <c>None</c> (<c>null</c> in C#) if no section has the key.</returns>
    member TryFindNested: key: Key -> string option

#if NET
    /// <summary>
    /// Finds the value of a key in its section or the nearest parent section, and parses it with
    /// <see cref="T:System.IParsable`1"/> and the invariant culture.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>
    /// <c>Some (Ok value)</c>, <c>Some (Error (InvalidValue text))</c> if the value can't be parsed, or <c>None</c>
    /// (<c>null</c> in C#) if no section has the key.
    /// </returns>
    member TryFindNestedAs<'T when 'T :> IParsable<'T>> : key: Key -> Result<'T, ParserError> option
#endif

    /// <summary>
    /// Finds the value of a key in its section or the nearest parent section, and converts it with a parser.
    /// </summary>
    /// <param name="parser">The parser. Any exception it throws counts as a parse failure. It isn't called for a missing key.</param>
    /// <param name="key">The key.</param>
    /// <returns>
    /// <c>Some (Ok value)</c>, <c>Some (Error (InvalidValue text))</c> if the parser throws, or <c>None</c>
    /// (<c>null</c> in C#) if no section has the key.
    /// </returns>
    member TryFindNestedWith: parser: Func<string, 'T> * key: Key -> Result<'T, ParserError> option

    /// <summary>Gets the value of a key.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The value.</returns>
    /// <exception cref="T:System.Collections.Generic.KeyNotFoundException">The key is missing.</exception>
    member Find: key: Key -> string

#if NET
    /// <summary>Gets the value of a key and parses it with <see cref="T:System.IParsable`1"/> and the invariant culture.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The parsed value.</returns>
    /// <exception cref="T:System.Collections.Generic.KeyNotFoundException">The key is missing.</exception>
    /// <exception cref="T:System.FormatException">The value can't be parsed.</exception>
    member FindAs<'T when 'T :> IParsable<'T>> : key: Key -> 'T
#endif

    /// <summary>Gets the value of a key and converts it with a parser.</summary>
    /// <param name="parser">The parser. Its exceptions are not caught.</param>
    /// <param name="key">The key.</param>
    /// <returns>The converted value.</returns>
    /// <exception cref="T:System.Collections.Generic.KeyNotFoundException">The key is missing.</exception>
    member FindWith: parser: Func<string, 'T> * key: Key -> 'T

    /// <summary>Gets the value of a key from its section or the nearest parent section.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The first value found.</returns>
    /// <exception cref="T:System.Collections.Generic.KeyNotFoundException">No section has the key.</exception>
    member FindNested: key: Key -> string

#if NET
    /// <summary>
    /// Gets the value of a key from its section or the nearest parent section, and parses it with
    /// <see cref="T:System.IParsable`1"/> and the invariant culture.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>The parsed value.</returns>
    /// <exception cref="T:System.Collections.Generic.KeyNotFoundException">No section has the key.</exception>
    /// <exception cref="T:System.FormatException">The value can't be parsed.</exception>
    member FindNestedAs<'T when 'T :> IParsable<'T>> : key: Key -> 'T
#endif

    /// <summary>
    /// Gets the value of a key from its section or the nearest parent section, and converts it with a parser.
    /// </summary>
    /// <param name="parser">The parser. Its exceptions are not caught.</param>
    /// <param name="key">The key.</param>
    /// <returns>The converted value.</returns>
    /// <exception cref="T:System.Collections.Generic.KeyNotFoundException">No section has the key.</exception>
    member FindNestedWith: parser: Func<string, 'T> * key: Key -> 'T

    /// <summary>Removes a key. Removing a missing key does nothing.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The new document.</returns>
    member Remove: key: Key -> Ini

    /// <summary>The number of values.</summary>
    member Count: int

    /// <summary>Whether the document has a value for exactly this key, without looking in parent sections.</summary>
    /// <param name="key">The key.</param>
    member ContainsKey: key: Key -> bool

    /// <summary>The keys, in key order.</summary>
    member Keys: Key seq

    /// <summary>The values, in key order.</summary>
    member Values: string seq

    /// <summary>The key/value pairs, in key order.</summary>
    member KeyValuePairs: KeyValuePair<Key, string> seq

    /// <summary>The section names, in order, with the root name first if there are root parameters.</summary>
    member Sections: Name seq

    /// <summary>Returns one section as a new document. Child sections are not included.</summary>
    /// <param name="name">The section name. <see cref="P:Fini.Name.Empty"/> gives the root parameters.</param>
    /// <returns>A document with the values of the section, empty if the section is missing.</returns>
    member Section: name: Name -> Ini

    /// <summary>Removes one section. Child sections are kept.</summary>
    /// <param name="name">The section name. <see cref="P:Fini.Name.Empty"/> removes the root parameters.</param>
    /// <returns>The new document.</returns>
    member RemoveSection: name: Name -> Ini

    /// <summary>
    /// Writes the document as lines: root parameters first, then a <c>[section]</c> block for each section, with
    /// a blank line between blocks. Sections and parameters are sorted.
    /// </summary>
    /// <returns>The lines. Reading them back gives an equal document.</returns>
    member ToLines: unit -> string seq

    /// <summary>Writes the document to a file, replacing it, in the layout of <see cref="M:Fini.Ini.ToLines"/>.</summary>
    /// <param name="path">The path of the file.</param>
    /// <exception cref="T:System.IO.IOException">The file can't be written.</exception>
    member ToFile: path: string -> unit

    /// <summary>Returns the lines of <see cref="M:Fini.Ini.ToLines"/> joined with <see cref="P:System.Environment.NewLine"/>.</summary>
    override ToString: unit -> string

    interface IEnumerable<KeyValuePair<Key, string>>

    interface IEquatable<Ini>

    interface IComparable<Ini>

    interface IComparable

/// <summary>Functions for creating, reading and changing INI documents. The document is always the last argument.</summary>
[<RequireQualifiedAccess>]
module Ini =
    /// <summary>A document with no values.</summary>
    val empty: Ini

    /// <summary>Creates a document from key/value pairs. If a key appears twice, the later value wins.</summary>
    /// <param name="pairs">The pairs.</param>
    /// <returns>The new document.</returns>
    val ofSeq: pairs: (Key * string) seq -> Ini

    /// <summary>Parses lines into a document.</summary>
    /// <param name="lines">The lines. They are read one at a time, and reading stops at the first invalid line.</param>
    /// <returns><c>Ok ini</c>, or <c>Error (InvalidLine line)</c> with the first invalid line.</returns>
    val fromLines: lines: string seq -> Result<Ini, ParserError>

    /// <summary>Reads a file into a document.</summary>
    /// <param name="path">The path of the file.</param>
    /// <returns><c>Ok ini</c>, or <c>Error (InvalidLine line)</c> with the first invalid line.</returns>
    /// <exception cref="T:System.IO.IOException">The file can't be read.</exception>
    val fromFile: path: string -> Result<Ini, ParserError>

    /// <summary>Adds key/value pairs on top of a document. If a key already exists, the new value wins.</summary>
    /// <param name="pairs">The pairs, for example from <see cref="M:Fini.IniModule.toSeq(Fini.Ini)"/> of another document.</param>
    /// <param name="ini">The document.</param>
    /// <returns>The new document.</returns>
    val append: pairs: (Key * string) seq -> ini: Ini -> Ini

    /// <summary>
    /// Parses lines on top of a document. If a key already exists, the new value wins. The lines start in the
    /// root section.
    /// </summary>
    /// <param name="lines">The lines. Reading stops at the first invalid line.</param>
    /// <param name="ini">The document.</param>
    /// <returns><c>Ok ini</c>, or <c>Error (InvalidLine line)</c> with the first invalid line.</returns>
    val appendLines: lines: string seq -> ini: Ini -> Result<Ini, ParserError>

    /// <summary>
    /// Reads a file on top of a document. If a key already exists, the new value wins. The file starts in the
    /// root section.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <param name="ini">The document.</param>
    /// <returns><c>Ok ini</c>, or <c>Error (InvalidLine line)</c> with the first invalid line.</returns>
    /// <exception cref="T:System.IO.IOException">The file can't be read.</exception>
    val appendFile: path: string -> ini: Ini -> Result<Ini, ParserError>

    /// <summary>Whether a document has no values.</summary>
    /// <param name="ini">The document.</param>
    val isEmpty: ini: Ini -> bool

    /// <summary>Adds a value, replacing an existing one. The key takes the spelling of <paramref name="key"/>.</summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value, stored exactly as given.</param>
    /// <param name="ini">The document.</param>
    /// <returns>The new document.</returns>
    val add: key: Key -> value: string -> ini: Ini -> Ini

    /// <summary>Finds the value of a key.</summary>
    /// <param name="key">The key.</param>
    /// <param name="ini">The document.</param>
    /// <returns>The value, or <c>None</c> if the key is missing.</returns>
    val tryFind: key: Key -> ini: Ini -> string option

#if NET
    /// <summary>Finds the value of a key and parses it with <see cref="T:System.IParsable`1"/> and the invariant culture.</summary>
    /// <param name="key">The key.</param>
    /// <param name="ini">The document.</param>
    /// <returns>
    /// <c>Some (Ok value)</c>, <c>Some (Error (InvalidValue text))</c> if the value can't be parsed, or <c>None</c>
    /// if the key is missing.
    /// </returns>
    val tryFindAs<'T when 'T :> IParsable<'T>> : key: Key -> ini: Ini -> Result<'T, ParserError> option
#endif

    /// <summary>Finds the value of a key and converts it with a parser.</summary>
    /// <param name="parser">The parser. Any exception it throws counts as a parse failure. It isn't called for a missing key.</param>
    /// <param name="key">The key.</param>
    /// <param name="ini">The document.</param>
    /// <returns>
    /// <c>Some (Ok value)</c>, <c>Some (Error (InvalidValue text))</c> if the parser throws, or <c>None</c> if the
    /// key is missing.
    /// </returns>
    val tryFindWith: parser: (string -> 'T) -> key: Key -> ini: Ini -> Result<'T, ParserError> option

    /// <summary>
    /// Finds the value of a key in its section or the nearest parent section: <c>a.b:x</c> looks in <c>a.b</c>,
    /// then <c>a</c>, then the root.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="ini">The document.</param>
    /// <returns>The first value found, or <c>None</c> if no section has the key.</returns>
    val tryFindNested: key: Key -> ini: Ini -> string option

#if NET
    /// <summary>
    /// Finds the value of a key in its section or the nearest parent section, and parses it with
    /// <see cref="T:System.IParsable`1"/> and the invariant culture.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="ini">The document.</param>
    /// <returns>
    /// <c>Some (Ok value)</c>, <c>Some (Error (InvalidValue text))</c> if the value can't be parsed, or <c>None</c>
    /// if no section has the key.
    /// </returns>
    val tryFindNestedAs<'T when 'T :> IParsable<'T>> : key: Key -> ini: Ini -> Result<'T, ParserError> option
#endif

    /// <summary>
    /// Finds the value of a key in its section or the nearest parent section, and converts it with a parser.
    /// </summary>
    /// <param name="parser">The parser. Any exception it throws counts as a parse failure. It isn't called for a missing key.</param>
    /// <param name="key">The key.</param>
    /// <param name="ini">The document.</param>
    /// <returns>
    /// <c>Some (Ok value)</c>, <c>Some (Error (InvalidValue text))</c> if the parser throws, or <c>None</c> if no
    /// section has the key.
    /// </returns>
    val tryFindNestedWith: parser: (string -> 'T) -> key: Key -> ini: Ini -> Result<'T, ParserError> option

    /// <summary>Gets the value of a key.</summary>
    /// <param name="key">The key.</param>
    /// <param name="ini">The document.</param>
    /// <returns>The value.</returns>
    /// <exception cref="T:System.Collections.Generic.KeyNotFoundException">The key is missing.</exception>
    val find: key: Key -> ini: Ini -> string

#if NET
    /// <summary>Gets the value of a key and parses it with <see cref="T:System.IParsable`1"/> and the invariant culture.</summary>
    /// <param name="key">The key.</param>
    /// <param name="ini">The document.</param>
    /// <returns>The parsed value.</returns>
    /// <exception cref="T:System.Collections.Generic.KeyNotFoundException">The key is missing.</exception>
    /// <exception cref="T:System.FormatException">The value can't be parsed.</exception>
    val findAs<'T when 'T :> IParsable<'T>> : key: Key -> ini: Ini -> 'T
#endif

    /// <summary>Gets the value of a key and converts it with a parser.</summary>
    /// <param name="parser">The parser. Its exceptions are not caught.</param>
    /// <param name="key">The key.</param>
    /// <param name="ini">The document.</param>
    /// <returns>The converted value.</returns>
    /// <exception cref="T:System.Collections.Generic.KeyNotFoundException">The key is missing.</exception>
    val findWith: parser: (string -> 'T) -> key: Key -> ini: Ini -> 'T

    /// <summary>Gets the value of a key from its section or the nearest parent section.</summary>
    /// <param name="key">The key.</param>
    /// <param name="ini">The document.</param>
    /// <returns>The first value found.</returns>
    /// <exception cref="T:System.Collections.Generic.KeyNotFoundException">No section has the key.</exception>
    val findNested: key: Key -> ini: Ini -> string

#if NET
    /// <summary>
    /// Gets the value of a key from its section or the nearest parent section, and parses it with
    /// <see cref="T:System.IParsable`1"/> and the invariant culture.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="ini">The document.</param>
    /// <returns>The parsed value.</returns>
    /// <exception cref="T:System.Collections.Generic.KeyNotFoundException">No section has the key.</exception>
    /// <exception cref="T:System.FormatException">The value can't be parsed.</exception>
    val findNestedAs<'T when 'T :> IParsable<'T>> : key: Key -> ini: Ini -> 'T
#endif

    /// <summary>
    /// Gets the value of a key from its section or the nearest parent section, and converts it with a parser.
    /// </summary>
    /// <param name="parser">The parser. Its exceptions are not caught.</param>
    /// <param name="key">The key.</param>
    /// <param name="ini">The document.</param>
    /// <returns>The converted value.</returns>
    /// <exception cref="T:System.Collections.Generic.KeyNotFoundException">No section has the key.</exception>
    val findNestedWith: parser: (string -> 'T) -> key: Key -> ini: Ini -> 'T

    /// <summary>Removes a key. Removing a missing key does nothing.</summary>
    /// <param name="key">The key.</param>
    /// <param name="ini">The document.</param>
    /// <returns>The new document.</returns>
    val remove: key: Key -> ini: Ini -> Ini

    /// <summary>The number of values in a document.</summary>
    /// <param name="ini">The document.</param>
    val count: ini: Ini -> int

    /// <summary>Whether a document has a value for exactly this key, without looking in parent sections.</summary>
    /// <param name="key">The key.</param>
    /// <param name="ini">The document.</param>
    val containsKey: key: Key -> ini: Ini -> bool

    /// <summary>The keys of a document, in key order.</summary>
    /// <param name="ini">The document.</param>
    val keys: ini: Ini -> Key seq

    /// <summary>The values of a document, in key order.</summary>
    /// <param name="ini">The document.</param>
    val values: ini: Ini -> string seq

    /// <summary>The section names of a document, in order, with the root name first if there are root parameters.</summary>
    /// <param name="ini">The document.</param>
    val sections: ini: Ini -> Name seq

    /// <summary>Returns one section as a new document. Child sections are not included.</summary>
    /// <param name="name">The section name. <see cref="P:Fini.Name.Empty"/> gives the root parameters.</param>
    /// <param name="ini">The document.</param>
    /// <returns>A document with the values of the section, empty if the section is missing.</returns>
    val section: name: Name -> ini: Ini -> Ini

    /// <summary>Removes one section. Child sections are kept.</summary>
    /// <param name="name">The section name. <see cref="P:Fini.Name.Empty"/> removes the root parameters.</param>
    /// <param name="ini">The document.</param>
    /// <returns>The new document.</returns>
    val removeSection: name: Name -> ini: Ini -> Ini

    /// <summary>The key/value pairs of a document, in key order.</summary>
    /// <param name="ini">The document.</param>
    val toSeq: ini: Ini -> (Key * string) seq

    /// <summary>
    /// Writes a document as lines: root parameters first, then a <c>[section]</c> block for each section, with a
    /// blank line between blocks. Sections and parameters are sorted.
    /// </summary>
    /// <param name="ini">The document.</param>
    /// <returns>The lines. Reading them back gives an equal document.</returns>
    val toLines: ini: Ini -> string seq

    /// <summary>Writes a document to a file, replacing it, in the layout of <c>toLines</c>.</summary>
    /// <param name="path">The path of the file.</param>
    /// <param name="ini">The document.</param>
    /// <exception cref="T:System.IO.IOException">The file can't be written.</exception>
    val toFile: path: string -> ini: Ini -> unit

    /// <summary>Returns the lines of <c>toLines</c> joined with <see cref="P:System.Environment.NewLine"/>.</summary>
    /// <param name="ini">The document.</param>
    val toString: ini: Ini -> string
