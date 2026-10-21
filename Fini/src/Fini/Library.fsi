namespace Fini

open System
open System.Collections
open System.Collections.Generic

/// <summary>
/// The reason a document could not be built: a line the parser cannot read, or a key it rejects.
/// Each case carries the offending text verbatim — untrimmed, unquoted, with no position and no prose
/// around it.
/// </summary>
/// <remarks>
/// Two errors are equal when they hold the same case and the same text, and <c>ToString</c> names both,
/// as in <c>InvalidKey "a b"</c>. From C# the cases are nested types that carry their payload under the
/// field name — <c>Key</c> and <c>Line</c> — with <c>IsInvalidKey</c> / <c>IsInvalidLine</c> to test one
/// and <c>NewInvalidKey</c> / <c>NewInvalidLine</c> to build one.
/// </remarks>
type ParserError =
    /// <summary>
    /// A key that is not of the form <c>section:parameter</c>. Reported by <c>Add</c> and <c>Create</c> /
    /// <c>Ini.add</c> and <c>Ini.ofSeq</c>, which is what keeps the invariant that anything
    /// <c>ToLines</c> writes can be read back.
    /// </summary>
    /// <remarks>
    /// Both halves must avoid whitespace and the characters <c>= : ; # [ ]</c>, and the parameter half
    /// must be non-empty; the section half may be empty, meaning the root section. So <c>a b</c>,
    /// <c>alpha:</c> and <c>alpha:beta:x</c> are all invalid keys.
    /// </remarks>
    | InvalidKey of Key: string

    /// <summary>
    /// A line that is neither a section header, a parameter, a comment nor whitespace. Reported by
    /// <c>FromLines</c>, <c>FromFile</c>, <c>AppendLines</c> and <c>AppendFile</c> / <c>Ini.fromLines</c>,
    /// <c>Ini.fromFile</c>, <c>Ini.appendLines</c> and <c>Ini.appendFile</c>.
    /// </summary>
    | InvalidLine of Line: string

/// <summary>
/// An immutable INI document: a set of parameters, each keyed <c>section:parameter</c>.
/// </summary>
/// <remarks>
/// <para>
/// An <c>Ini</c> is a value. Every operation that would change a document returns a new one and leaves the
/// original untouched, and two documents are equal when they hold the same keys and the same values —
/// name case, declaration order, comments and blank lines are not taken into account, values and their
/// case are. Equal documents have equal hash codes, and documents are ordered too, by keys and then
/// values, in agreement with equality.
/// </para>
/// <para>
/// The model is flat: there is no nested object to walk, so lookup, enumeration and rendering all work on
/// the same sequence of pairs, in key order — the root section first, then sections
/// <c>OrdinalIgnoreCase</c> with a parent ahead of its children, then parameters within a section.
/// The pairs themselves are reached by enumerating the document — it is an
/// <c>IEnumerable&lt;KeyValuePair&lt;string, string&gt;&gt;</c>, keyed <c>section:parameter</c> with a root
/// parameter keyed <c>:name</c> — or, from F#, through <c>Ini.toSeq</c>.
/// </para>
/// <para>
/// Keys are compared case-insensitively (<c>OrdinalIgnoreCase</c>) in both halves; a key with no colon
/// refers to the root section, so <c>x</c> and <c>:x</c> are the same key. Keys are never trimmed or
/// repaired, so a key the parser would reject is simply absent on the way out: <c>TryFind</c> gives
/// <c>None</c>, <c>ContainsKey</c> gives <c>false</c>, <c>Remove</c> is a no-op and <c>Find</c> raises.
/// </para>
/// <para>
/// Content failures — an unparsable line, an invalid key — are returned as
/// <c>Error</c> of <c>Result&lt;Ini, ParserError&gt;</c>. A missing key is an answer rather than a failure:
/// <c>TryFind</c> and <c>TryFindNested</c> return an option, <c>Find</c> and <c>FindNested</c> are their
/// partial counterparts and raise. A <c>null</c> argument and a filesystem problem are raised.
/// </para>
/// </remarks>
[<Sealed>]
type Ini =
    /// <summary>The empty document.</summary>
    static member Empty: Ini

    /// <summary>Builds a document from key/value pairs.</summary>
    /// <param name="pairs">
    /// The pairs to add, each key of the form <c>section:parameter</c>. A later pair overwrites an earlier
    /// one with the same key. Enumerated lazily, and only up to the first invalid key.
    /// </param>
    /// <returns>
    /// <c>Ok</c> of the document, or <c>Error</c> of <c>InvalidKey</c> for the first key the parser
    /// rejects.
    /// </returns>
    /// <exception cref="T:System.ArgumentNullException">
    /// <paramref name="pairs"/>, or the key or the value of one of its pairs, is <c>null</c>.
    /// </exception>
    /// <remarks>
    /// Takes the pair shape the document enumerates as, so <c>Ini.Create(ini)</c> round-trips a
    /// document.
    /// </remarks>
    static member Create: pairs: seq<KeyValuePair<string, string>> -> Result<Ini, ParserError>

    /// <summary>Parses a file into a document.</summary>
    /// <param name="path">The path of the file to read.</param>
    /// <returns>
    /// <c>Ok</c> of the document, or <c>Error</c> of <c>InvalidLine</c> for the first line the parser
    /// cannot read.
    /// </returns>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="path"/> is <c>null</c>.</exception>
    /// <exception cref="T:System.IO.FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="T:System.IO.DirectoryNotFoundException">The path is invalid.</exception>
    /// <exception cref="T:System.IO.IOException">The file cannot be read.</exception>
    /// <remarks>
    /// A single lazy pass that stops at the first unparsable line — a 10,000-line file with a bad line at
    /// position 3 reads three lines — and the file handle is closed on the way out.
    /// </remarks>
    static member FromFile: path: string -> Result<Ini, ParserError>

    /// <summary>Parses lines into a document.</summary>
    /// <param name="lines">
    /// The lines to read: <c>[name]</c> opens a section, <c>name = value</c> is a parameter in the current
    /// section, <c>; text</c> and <c># text</c> are comments, blank and whitespace lines are ignored, and
    /// anything else is an error. Parameters before the first header belong to the root section.
    /// </param>
    /// <returns>
    /// <c>Ok</c> of the document, or <c>Error</c> of <c>InvalidLine</c> for the first line the parser
    /// cannot read.
    /// </returns>
    /// <exception cref="T:System.ArgumentNullException">
    /// <paramref name="lines"/>, or one of the lines, is <c>null</c>.
    /// </exception>
    /// <remarks>
    /// A value is everything after the first <c>=</c>, with surrounding whitespace trimmed, so it may
    /// contain <c>= : ; #</c> and may be empty. A later parameter overwrites an earlier one with the same
    /// key, and a section may be reopened later on.
    /// </remarks>
    static member FromLines: lines: seq<string> -> Result<Ini, ParserError>

    /// <summary>Layers a file over this document.</summary>
    /// <param name="path">The path of the file to read.</param>
    /// <returns>
    /// <c>Ok</c> of a new document in which the file's parameters win on a shared key and this document's
    /// own parameters survive, or <c>Error</c> of <c>InvalidLine</c> for the first line the parser cannot
    /// read.
    /// </returns>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="path"/> is <c>null</c>.</exception>
    /// <exception cref="T:System.IO.FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="T:System.IO.DirectoryNotFoundException">The path is invalid.</exception>
    /// <exception cref="T:System.IO.IOException">The file cannot be read.</exception>
    /// <remarks>
    /// Parsing starts at the root section, by design: a layer's lines are read by exactly the rules
    /// <c>FromLines</c> uses, so load order decides which value wins, never which key a line writes to.
    /// </remarks>
    member AppendFile: path: string -> Result<Ini, ParserError>

    /// <summary>Layers lines over this document.</summary>
    /// <param name="lines">The lines to read, by the rules <c>FromLines</c> uses.</param>
    /// <returns>
    /// <c>Ok</c> of a new document in which the given parameters win on a shared key and this document's
    /// own parameters survive, or <c>Error</c> of <c>InvalidLine</c> for the first line the parser cannot
    /// read.
    /// </returns>
    /// <exception cref="T:System.ArgumentNullException">
    /// <paramref name="lines"/>, or one of the lines, is <c>null</c>.
    /// </exception>
    /// <remarks>
    /// Parsing starts at the root section, by design, so <c>Ini.Empty.AppendLines(lines)</c> is
    /// <c>Ini.FromLines(lines)</c> and an appended layer opens its own sections when it means to write
    /// into them.
    /// </remarks>
    member AppendLines: lines: seq<string> -> Result<Ini, ParserError>

    /// <summary>Whether the document holds no parameters.</summary>
    member IsEmpty: bool

    /// <summary>Adds a parameter, or overwrites the one already under that key.</summary>
    /// <param name="key">The key, of the form <c>section:parameter</c>.</param>
    /// <param name="value">The value, kept as written.</param>
    /// <returns>
    /// <c>Ok</c> of a new document holding the parameter, or <c>Error</c> of <c>InvalidKey</c> when the
    /// parser rejects the key.
    /// </returns>
    /// <exception cref="T:System.ArgumentNullException">
    /// <paramref name="key"/> or <paramref name="value"/> is <c>null</c>.
    /// </exception>
    member Add: key: string * value: string -> Result<Ini, ParserError>

    /// <summary>Looks up one key, exactly.</summary>
    /// <param name="key">The key, of the form <c>section:parameter</c>.</param>
    /// <returns><c>Some</c> of the value, or <c>None</c> when the document holds no such key.</returns>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is <c>null</c>.</exception>
    /// <remarks>
    /// Never walks the section hierarchy. From C# the result is a possibly-<c>null</c>
    /// <c>FSharpOption&lt;string&gt;</c> whose <c>Value</c> holds the payload.
    /// </remarks>
    member TryFind: key: string -> string option

    /// <summary>Looks up one key, walking up the section hierarchy.</summary>
    /// <param name="key">The key, of the form <c>section:parameter</c>.</param>
    /// <returns>
    /// <c>Some</c> of the value found in the given section, or failing that in its nearest dotted
    /// ancestor, or failing that in the root section; <c>None</c> when none of them holds the parameter.
    /// </returns>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is <c>null</c>.</exception>
    /// <example>
    /// Over the document <c>timeout = 30</c>, <c>[server] host = localhost</c>,
    /// <c>[server.dev] port = 5000</c>: <c>server.dev:port</c> gives <c>5000</c> from the section itself,
    /// <c>server.dev:host</c> gives <c>localhost</c> inherited from <c>[server]</c>, and
    /// <c>server.dev:timeout</c> gives <c>30</c> inherited from the root.
    /// </example>
    member TryFindNested: key: string -> string option

    /// <summary>Looks up one key, exactly. The partial counterpart of <c>TryFind</c>.</summary>
    /// <param name="key">The key, of the form <c>section:parameter</c>.</param>
    /// <returns>The value.</returns>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is <c>null</c>.</exception>
    /// <exception cref="T:System.Collections.Generic.KeyNotFoundException">
    /// The document holds no such key.
    /// </exception>
    member Find: key: string -> string

    /// <summary>
    /// Looks up one key, walking up the section hierarchy. The partial counterpart of
    /// <c>TryFindNested</c>.
    /// </summary>
    /// <param name="key">The key, of the form <c>section:parameter</c>.</param>
    /// <returns>The value found in the given section, in its nearest dotted ancestor, or in the root.</returns>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is <c>null</c>.</exception>
    /// <exception cref="T:System.Collections.Generic.KeyNotFoundException">
    /// Neither the section nor any of its ancestors nor the root holds the parameter.
    /// </exception>
    member FindNested: key: string -> string

    /// <summary>Removes one parameter.</summary>
    /// <param name="key">The key, of the form <c>section:parameter</c>.</param>
    /// <returns>
    /// A new document without that parameter, or one equal to this document when the key is absent.
    /// </returns>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is <c>null</c>.</exception>
    member Remove: key: string -> Ini

    /// <summary>The number of parameters in the document.</summary>
    member Count: int

    /// <summary>Whether the document holds that key.</summary>
    /// <param name="key">The key, of the form <c>section:parameter</c>.</param>
    /// <returns><c>true</c> when the key is present, matched case-insensitively.</returns>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is <c>null</c>.</exception>
    member ContainsKey: key: string -> bool

    /// <summary>The keys, in key order, each of the form <c>section:parameter</c>.</summary>
    /// <remarks>A root parameter is keyed <c>:name</c>.</remarks>
    member Keys: seq<string>

    /// <summary>The values, in key order, aligned with <c>Keys</c>.</summary>
    member Values: seq<string>

    /// <summary>The distinct section names, in key order, <c>""</c> for the root section.</summary>
    /// <remarks>
    /// A dotted name is stored verbatim and is a section of its own, so <c>[server]</c> and
    /// <c>[server.dev]</c> are two names.
    /// </remarks>
    member Sections: seq<string>

    /// <summary>Takes one section as a standalone document.</summary>
    /// <param name="name">The section name, matched case-insensitively; <c>""</c> for the root section.</param>
    /// <returns>
    /// A new document holding only that section's parameters, with their keys unchanged, or the empty
    /// document when nothing matches.
    /// </returns>
    /// <remarks>
    /// Dotted children are excluded. The result renders back to a valid standalone document, and this
    /// document is left untouched. A <c>null</c> name is not guarded and simply matches nothing.
    /// </remarks>
    member Section: name: string -> Ini

    /// <summary>Removes one whole section. The complement of <c>Section</c>.</summary>
    /// <param name="name">The section name, matched case-insensitively; <c>""</c> for the root section.</param>
    /// <returns>
    /// A new document without that section's parameters, or one equal to this document when nothing
    /// matches.
    /// </returns>
    /// <remarks>
    /// Dotted children are sections of their own, so removing <c>alpha</c> leaves <c>alpha.beta</c> in
    /// place — remove it by name, or fold over <c>Sections</c> to drop a whole subtree. A <c>null</c> name
    /// is not guarded and simply matches nothing.
    /// </remarks>
    member RemoveSection: name: string -> Ini

    /// <summary>Renders the document.</summary>
    /// <returns>
    /// The lines of a canonical document, computed lazily: root parameters first with no header, then one
    /// <c>[section]</c> block per section separated by a blank line, all in key order. An empty value
    /// renders as <c>x =</c>.
    /// </returns>
    /// <remarks>
    /// The output is normalised, not a faithful copy of the input: comments, blank lines, original
    /// ordering and duplicate entries are not preserved. Values are, so rendering and parsing back gives
    /// an equal document.
    /// </remarks>
    member ToLines: unit -> seq<string>

    /// <summary>Renders the document to a file, by the rules <c>ToLines</c> uses.</summary>
    /// <param name="path">The path of the file to write, overwritten when it already exists.</param>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="path"/> is <c>null</c>.</exception>
    /// <exception cref="T:System.IO.DirectoryNotFoundException">The path is invalid.</exception>
    /// <exception cref="T:System.IO.IOException">The file cannot be written.</exception>
    /// <remarks>Writes the lines with <c>File.WriteAllLines</c>, so each ends with the platform newline.</remarks>
    member ToFile: path: string -> unit

    /// <summary>Renders the document, by the rules <c>ToLines</c> uses.</summary>
    /// <returns>The lines joined with <c>Environment.NewLine</c>.</returns>
    override ToString: unit -> string

    interface IEnumerable<KeyValuePair<string, string>>
    interface IEnumerable
    interface IComparable
    interface IComparable<Ini>
    interface IEquatable<Ini>
    interface IStructuralComparable
    interface IStructuralEquatable

/// <summary>
/// Functions over <see cref="T:Fini.Ini"/>, the document last so that they compose with <c>|&gt;</c>.
/// </summary>
[<RequireQualifiedAccess>]
module Ini =
    /// <summary>The empty document.</summary>
    val empty: Ini

    /// <summary>Builds a document from key/value tuples.</summary>
    /// <param name="pairs">
    /// The pairs to add, each key of the form <c>section:parameter</c>. A later pair overwrites an earlier
    /// one with the same key. Enumerated lazily, and only up to the first invalid key.
    /// </param>
    /// <returns>
    /// <c>Ok</c> of the document, or <c>Error</c> of <c>InvalidKey</c> for the first key the parser
    /// rejects.
    /// </returns>
    /// <exception cref="T:System.ArgumentNullException">
    /// <paramref name="pairs"/>, or a member of one of its tuples, is <c>null</c>.
    /// </exception>
    /// <remarks>
    /// Takes the shape <c>toSeq</c> produces, so <c>ini |&gt; Ini.toSeq |&gt; Ini.ofSeq</c> round-trips a
    /// document.
    /// </remarks>
    val inline ofSeq: pairs: seq<string * string> -> Result<Ini, ParserError>

    /// <summary>Parses a file into a document.</summary>
    /// <param name="path">The path of the file to read.</param>
    /// <returns>
    /// <c>Ok</c> of the document, or <c>Error</c> of <c>InvalidLine</c> for the first line the parser
    /// cannot read.
    /// </returns>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="path"/> is <c>null</c>.</exception>
    /// <exception cref="T:System.IO.FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="T:System.IO.DirectoryNotFoundException">The path is invalid.</exception>
    /// <exception cref="T:System.IO.IOException">The file cannot be read.</exception>
    /// <remarks>
    /// A single lazy pass that stops at the first unparsable line, closing the file handle on the way out.
    /// </remarks>
    val inline fromFile: path: string -> Result<Ini, ParserError>

    /// <summary>Parses lines into a document.</summary>
    /// <param name="lines">
    /// The lines to read: <c>[name]</c> opens a section, <c>name = value</c> is a parameter in the current
    /// section, <c>; text</c> and <c># text</c> are comments, blank and whitespace lines are ignored, and
    /// anything else is an error. Parameters before the first header belong to the root section.
    /// </param>
    /// <returns>
    /// <c>Ok</c> of the document, or <c>Error</c> of <c>InvalidLine</c> for the first line the parser
    /// cannot read.
    /// </returns>
    /// <exception cref="T:System.ArgumentNullException">
    /// <paramref name="lines"/>, or one of the lines, is <c>null</c>.
    /// </exception>
    val inline fromLines: lines: seq<string> -> Result<Ini, ParserError>

    /// <summary>Layers a file over a document.</summary>
    /// <param name="path">The path of the file to read.</param>
    /// <param name="ini">The document to layer it over, left untouched.</param>
    /// <returns>
    /// <c>Ok</c> of a new document in which the file's parameters win on a shared key and the given
    /// document's own parameters survive, or <c>Error</c> of <c>InvalidLine</c> for the first line the
    /// parser cannot read.
    /// </returns>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="path"/> is <c>null</c>.</exception>
    /// <exception cref="T:System.IO.FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="T:System.IO.DirectoryNotFoundException">The path is invalid.</exception>
    /// <exception cref="T:System.IO.IOException">The file cannot be read.</exception>
    /// <remarks>
    /// Parsing starts at the root section, by design: a layer's lines are read by exactly the rules
    /// <c>fromLines</c> uses, so load order decides which value wins, never which key a line writes to.
    /// </remarks>
    val inline appendFile: path: string -> ini: Ini -> Result<Ini, ParserError>

    /// <summary>Layers lines over a document.</summary>
    /// <param name="lines">The lines to read, by the rules <c>fromLines</c> uses.</param>
    /// <param name="ini">The document to layer them over, left untouched.</param>
    /// <returns>
    /// <c>Ok</c> of a new document in which the given parameters win on a shared key and the document's
    /// own parameters survive, or <c>Error</c> of <c>InvalidLine</c> for the first line the parser cannot
    /// read.
    /// </returns>
    /// <exception cref="T:System.ArgumentNullException">
    /// <paramref name="lines"/>, or one of the lines, is <c>null</c>.
    /// </exception>
    /// <remarks>
    /// Parsing starts at the root section, by design, so <c>Ini.empty |&gt; Ini.appendLines lines</c> is
    /// <c>Ini.fromLines lines</c>.
    /// </remarks>
    val inline appendLines: lines: seq<string> -> ini: Ini -> Result<Ini, ParserError>

    /// <summary>Whether a document holds no parameters.</summary>
    /// <param name="ini">The document.</param>
    /// <returns><c>true</c> when the document is empty.</returns>
    val inline isEmpty: ini: Ini -> bool

    /// <summary>Adds a parameter, or overwrites the one already under that key.</summary>
    /// <param name="key">The key, of the form <c>section:parameter</c>.</param>
    /// <param name="value">The value, kept as written.</param>
    /// <param name="ini">The document to add it to, left untouched.</param>
    /// <returns>
    /// <c>Ok</c> of a new document holding the parameter, or <c>Error</c> of <c>InvalidKey</c> when the
    /// parser rejects the key.
    /// </returns>
    /// <exception cref="T:System.ArgumentNullException">
    /// <paramref name="key"/> or <paramref name="value"/> is <c>null</c>.
    /// </exception>
    val inline add: key: string -> value: string -> ini: Ini -> Result<Ini, ParserError>

    /// <summary>Looks up one key, exactly.</summary>
    /// <param name="key">The key, of the form <c>section:parameter</c>.</param>
    /// <param name="ini">The document.</param>
    /// <returns><c>Some</c> of the value, or <c>None</c> when the document holds no such key.</returns>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is <c>null</c>.</exception>
    /// <remarks>Never walks the section hierarchy.</remarks>
    val inline tryFind: key: string -> ini: Ini -> string option

    /// <summary>Looks up one key, walking up the section hierarchy.</summary>
    /// <param name="key">The key, of the form <c>section:parameter</c>.</param>
    /// <param name="ini">The document.</param>
    /// <returns>
    /// <c>Some</c> of the value found in the given section, or failing that in its nearest dotted
    /// ancestor, or failing that in the root section; <c>None</c> when none of them holds the parameter.
    /// </returns>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is <c>null</c>.</exception>
    /// <example>
    /// Over the document <c>timeout = 30</c>, <c>[server] host = localhost</c>,
    /// <c>[server.dev] port = 5000</c>: <c>server.dev:port</c> gives <c>5000</c> from the section itself,
    /// <c>server.dev:host</c> gives <c>localhost</c> inherited from <c>[server]</c>, and
    /// <c>server.dev:timeout</c> gives <c>30</c> inherited from the root.
    /// </example>
    val inline tryFindNested: key: string -> ini: Ini -> string option

    /// <summary>Looks up one key, exactly. The partial counterpart of <c>tryFind</c>.</summary>
    /// <param name="key">The key, of the form <c>section:parameter</c>.</param>
    /// <param name="ini">The document.</param>
    /// <returns>The value.</returns>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is <c>null</c>.</exception>
    /// <exception cref="T:System.Collections.Generic.KeyNotFoundException">
    /// The document holds no such key.
    /// </exception>
    val inline find: key: string -> ini: Ini -> string

    /// <summary>
    /// Looks up one key, walking up the section hierarchy. The partial counterpart of
    /// <c>tryFindNested</c>.
    /// </summary>
    /// <param name="key">The key, of the form <c>section:parameter</c>.</param>
    /// <param name="ini">The document.</param>
    /// <returns>The value found in the given section, in its nearest dotted ancestor, or in the root.</returns>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is <c>null</c>.</exception>
    /// <exception cref="T:System.Collections.Generic.KeyNotFoundException">
    /// Neither the section nor any of its ancestors nor the root holds the parameter.
    /// </exception>
    val inline findNested: key: string -> ini: Ini -> string

    /// <summary>Removes one parameter.</summary>
    /// <param name="key">The key, of the form <c>section:parameter</c>.</param>
    /// <param name="ini">The document to remove it from, left untouched.</param>
    /// <returns>
    /// A new document without that parameter, or one equal to the given document when the key is absent.
    /// </returns>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is <c>null</c>.</exception>
    val inline remove: key: string -> ini: Ini -> Ini

    /// <summary>The number of parameters in a document.</summary>
    /// <param name="ini">The document.</param>
    /// <returns>The parameter count.</returns>
    val inline count: ini: Ini -> int

    /// <summary>Whether a document holds that key.</summary>
    /// <param name="key">The key, of the form <c>section:parameter</c>.</param>
    /// <param name="ini">The document.</param>
    /// <returns><c>true</c> when the key is present, matched case-insensitively.</returns>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is <c>null</c>.</exception>
    val inline containsKey: key: string -> ini: Ini -> bool

    /// <summary>The keys of a document, in key order, each of the form <c>section:parameter</c>.</summary>
    /// <param name="ini">The document.</param>
    /// <returns>The keys; a root parameter is keyed <c>:name</c>.</returns>
    val inline keys: ini: Ini -> seq<string>

    /// <summary>The values of a document, in key order, aligned with <c>keys</c>.</summary>
    /// <param name="ini">The document.</param>
    /// <returns>The values.</returns>
    val inline values: ini: Ini -> seq<string>

    /// <summary>The distinct section names of a document, in key order.</summary>
    /// <param name="ini">The document.</param>
    /// <returns>The names, <c>""</c> for the root section.</returns>
    /// <remarks>
    /// A dotted name is stored verbatim and is a section of its own, so <c>[server]</c> and
    /// <c>[server.dev]</c> are two names.
    /// </remarks>
    val inline sections: ini: Ini -> seq<string>

    /// <summary>Takes one section as a standalone document.</summary>
    /// <param name="name">The section name, matched case-insensitively; <c>""</c> for the root section.</param>
    /// <param name="ini">The document to take it from, left untouched.</param>
    /// <returns>
    /// A new document holding only that section's parameters, with their keys unchanged, or the empty
    /// document when nothing matches.
    /// </returns>
    /// <remarks>
    /// Dotted children are excluded. The result renders back to a valid standalone document:
    /// <c>ini |&gt; Ini.section "alpha" |&gt; Ini.toLines</c> gives <c>[ "[alpha]"; "x = 1" ]</c>. A
    /// <c>null</c> name is not guarded and simply matches nothing.
    /// </remarks>
    val inline section: name: string -> ini: Ini -> Ini

    /// <summary>Removes one whole section. The complement of <c>section</c>.</summary>
    /// <param name="name">The section name, matched case-insensitively; <c>""</c> for the root section.</param>
    /// <param name="ini">The document to remove it from, left untouched.</param>
    /// <returns>
    /// A new document without that section's parameters, or one equal to the given document when nothing
    /// matches.
    /// </returns>
    /// <remarks>
    /// Dotted children are sections of their own, so removing <c>alpha</c> leaves <c>alpha.beta</c> in
    /// place — remove it by name, or fold over <c>Ini.sections</c> to drop a whole subtree. A <c>null</c>
    /// name is not guarded and simply matches nothing.
    /// </remarks>
    val inline removeSection: name: string -> ini: Ini -> Ini

    /// <summary>The parameters of a document as key/value tuples, in key order.</summary>
    /// <param name="ini">The document.</param>
    /// <returns>The pairs, in the shape <c>ofSeq</c> takes.</returns>
    val inline toSeq: ini: Ini -> seq<string * string>

    /// <summary>Renders a document.</summary>
    /// <param name="ini">The document.</param>
    /// <returns>
    /// The lines of a canonical document, computed lazily: root parameters first with no header, then one
    /// <c>[section]</c> block per section separated by a blank line, all in key order. An empty value
    /// renders as <c>x =</c>.
    /// </returns>
    /// <remarks>
    /// The output is normalised, not a faithful copy of the input: comments, blank lines, original
    /// ordering and duplicate entries are not preserved. Values are, so rendering and parsing back gives
    /// an equal document.
    /// </remarks>
    val inline toLines: ini: Ini -> seq<string>

    /// <summary>Renders a document to a file, by the rules <c>toLines</c> uses.</summary>
    /// <param name="path">The path of the file to write, overwritten when it already exists.</param>
    /// <param name="ini">The document.</param>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="path"/> is <c>null</c>.</exception>
    /// <exception cref="T:System.IO.DirectoryNotFoundException">The path is invalid.</exception>
    /// <exception cref="T:System.IO.IOException">The file cannot be written.</exception>
    /// <remarks>Writes the lines with <c>File.WriteAllLines</c>, so each ends with the platform newline.</remarks>
    val inline toFile: path: string -> ini: Ini -> unit

    /// <summary>Renders a document, by the rules <c>toLines</c> uses.</summary>
    /// <param name="ini">The document.</param>
    /// <returns>The lines joined with <c>Environment.NewLine</c>.</returns>
    val inline toString: ini: Ini -> string
