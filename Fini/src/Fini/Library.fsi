namespace Fini

open System
open System.Collections
open System.Collections.Generic

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
[<Sealed>]
type Ini =
    /// <summary>The empty document.</summary>
    static member Empty: Ini

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
    static member Create: pairs: seq<KeyValuePair<string, string>> -> Result<Ini, string>

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
    static member FromFile: path: string -> Result<Ini, string>

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
    static member FromLines: lines: seq<string> -> Result<Ini, string>

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
    member AppendFile: path: string -> Result<Ini, string>

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
    member AppendLines: lines: seq<string> -> Result<Ini, string>

    /// <summary>True if the document holds no parameters.</summary>
    member IsEmpty: bool

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
    member Add: key: string * value: string -> Result<Ini, string>

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
    member TryFind: key: string * value: byref<string> -> bool

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
    member TryFindNested: key: string * value: byref<string> -> bool

    /// <summary>Looks up an exact key, raising if it is absent.</summary>
    ///
    /// <param name="key">The key to find, as <c>section:parameter</c> or <c>parameter</c> for the root
    /// section.</param>
    ///
    /// <returns>The value found.</returns>
    ///
    /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is null.</exception>
    /// <exception cref="T:System.Collections.Generic.KeyNotFoundException">The key is absent.</exception>
    member Find: key: string -> string

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
    member FindNested: key: string -> string

    /// <summary>Removes one key.</summary>
    ///
    /// <param name="key">The key to remove, as <c>section:parameter</c> or <c>parameter</c> for the root
    /// section.</param>
    ///
    /// <returns>A new document without the key, or a document equal to this one if the key is absent.</returns>
    ///
    /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is null.</exception>
    member Remove: key: string -> Ini

    /// <summary>The number of parameters in the document.</summary>
    member Count: int

    /// <summary>True if the document holds a parameter under the exact key.</summary>
    ///
    /// <param name="key">The key to test, as <c>section:parameter</c> or <c>parameter</c> for the root
    /// section.</param>
    ///
    /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is null.</exception>
    member ContainsKey: key: string -> bool

    /// <summary>The keys of the document, as <c>section:parameter</c>, in key order.</summary>
    member Keys: seq<string>

    /// <summary>The values of the document, aligned with <see cref="P:Fini.Ini.Keys"/>.</summary>
    member Values: seq<string>

    /// <summary>The key-value pairs of the document, in key order.</summary>
    member KeyValuePairs: seq<KeyValuePair<string, string>>

    /// <summary>The distinct section names of the document, in key order, with the root section as the empty
    /// string.</summary>
    ///
    /// <remarks>Names differing only in case count as one, and a dotted child section appears under its own
    /// full name.</remarks>
    member Sections: seq<string>

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
    member Section: name: string -> Ini

    /// <summary>Renders the document as lines.</summary>
    ///
    /// <returns>The root parameters first with no header, then one <c>[section]</c> block per section
    /// separated by a blank line.</returns>
    ///
    /// <remarks>The output is normalised, not a copy of the input: comments, blank lines, original ordering
    /// and duplicate entries are not preserved. Values are, so rendering and parsing round-trip. An empty
    /// value renders as <c>x =</c>.</remarks>
    member ToLines: unit -> seq<string>

    /// <summary>Renders the document to a file, overwriting it if it exists.</summary>
    ///
    /// <param name="path">The path of the file to write.</param>
    ///
    /// <exception cref="T:System.ArgumentNullException"><paramref name="path"/> is null.</exception>
    /// <exception cref="T:System.IO.IOException">The file cannot be written; the exceptions of
    /// <see cref="M:System.IO.File.WriteAllLines(System.String,System.Collections.Generic.IEnumerable{System.String})"/>
    /// propagate unchanged.</exception>
    member ToFile: path: string -> unit

    /// <summary>Renders the document, joining the lines with <c>\n</c>.</summary>
    override ToString: unit -> string

    interface IEnumerable<KeyValuePair<string, string>>
    interface IEnumerable
    interface IComparable
    interface IComparable<Ini>
    interface IEquatable<Ini>
    interface IStructuralComparable
    interface IStructuralEquatable

/// <summary>Functional operations on <see cref="T:Fini.Ini"/>.</summary>
///
/// <remarks>The document is always the last parameter, so the functions compose with <c>|&gt;</c>.</remarks>
[<RequireQualifiedAccess>]
module Ini =
    /// <summary>The empty document.</summary>
    val empty: Ini

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
    val ofSeq: pairs: seq<string * string> -> Result<Ini, string>

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
    val fromFile: path: string -> Result<Ini, string>

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
    val fromLines: lines: seq<string> -> Result<Ini, string>

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
    val appendFile: path: string -> ini: Ini -> Result<Ini, string>

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
    val appendLines: lines: seq<string> -> ini: Ini -> Result<Ini, string>

    /// <summary>True if the document holds no parameters.</summary>
    ///
    /// <param name="ini">The document to test.</param>
    val isEmpty: ini: Ini -> bool

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
    val add: key: string -> value: string -> ini: Ini -> Result<Ini, string>

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
    val tryFind: key: string -> ini: Ini -> string option

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
    val tryFindNested: key: string -> ini: Ini -> string option

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
    val find: key: string -> ini: Ini -> string

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
    val findNested: key: string -> ini: Ini -> string

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
    val remove: key: string -> ini: Ini -> Ini

    /// <summary>The number of parameters in the document.</summary>
    ///
    /// <param name="ini">The document to count.</param>
    val count: ini: Ini -> int

    /// <summary>True if the document holds a parameter under the exact key.</summary>
    ///
    /// <param name="key">The key to test, as <c>section:parameter</c> or <c>parameter</c> for the root
    /// section.</param>
    /// <param name="ini">The document to search.</param>
    ///
    /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is null.</exception>
    val containsKey: key: string -> ini: Ini -> bool

    /// <summary>The keys of the document, as <c>section:parameter</c>, in key order.</summary>
    ///
    /// <param name="ini">The document to enumerate.</param>
    val keys: ini: Ini -> seq<string>

    /// <summary>The values of the document, aligned with <c>keys</c>.</summary>
    ///
    /// <param name="ini">The document to enumerate.</param>
    val values: ini: Ini -> seq<string>

    /// <summary>The distinct section names of the document, in key order, with the root section as the empty
    /// string.</summary>
    ///
    /// <param name="ini">The document to enumerate.</param>
    ///
    /// <remarks>Names differing only in case count as one, and a dotted child section appears under its own
    /// full name.</remarks>
    val sections: ini: Ini -> seq<string>

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
    val section: name: string -> ini: Ini -> Ini

    /// <summary>The key-value pairs of the document, in key order.</summary>
    ///
    /// <param name="ini">The document to enumerate.</param>
    val toSeq: ini: Ini -> seq<string * string>

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
    val toLines: ini: Ini -> seq<string>

    /// <summary>Renders the document to a file, overwriting it if it exists.</summary>
    ///
    /// <param name="path">The path of the file to write.</param>
    /// <param name="ini">The document to render.</param>
    ///
    /// <exception cref="T:System.ArgumentNullException"><paramref name="path"/> is null.</exception>
    /// <exception cref="T:System.IO.IOException">The file cannot be written; the exceptions of
    /// <see cref="M:System.IO.File.WriteAllLines(System.String,System.Collections.Generic.IEnumerable{System.String})"/>
    /// propagate unchanged.</exception>
    val toFile: path: string -> ini: Ini -> unit

    /// <summary>Renders the document, joining the lines with <c>\n</c>.</summary>
    ///
    /// <param name="ini">The document to render.</param>
    val toString: ini: Ini -> string
