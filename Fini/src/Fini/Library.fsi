namespace Fini

open System
open System.Collections
open System.Collections.Generic

/// <summary>
/// Why an operation failed. A line that can't be read or an invalid key stops a document from being
/// built. On <c>net10.0</c>, a typed lookup also reports a value that can't be converted to the requested
/// type. Each case holds the exact text that caused the problem, with nothing trimmed or added.
/// </summary>
/// <remarks>
/// Two errors are equal if they are the same case with the same text. <c>ToString</c> shows both, for
/// example <c>InvalidKey "a b"</c>. In C#, each case is a nested type, and its text is a property named
/// after the field: <c>Key</c>, <c>Line</c>, and <c>Value</c> on <c>net10.0</c>. Use
/// <c>IsInvalidKey</c> / <c>IsInvalidLine</c> to check the case, and <c>NewInvalidKey</c> /
/// <c>NewInvalidLine</c> to create one. The <c>net48</c> build has only two cases.
/// </remarks>
type ParserError =
    /// <summary>
    /// A key that doesn't have the form <c>section:parameter</c>. Returned by <c>Add</c> and
    /// <c>Create</c> / <c>Ini.add</c> and <c>Ini.ofSeq</c>. Rejecting these keys guarantees that anything
    /// <c>ToLines</c> writes can be read back.
    /// </summary>
    /// <remarks>
    /// Neither part may contain whitespace or any of <c>= : ; # [ ]</c>. The parameter part can't be
    /// empty. The section part can be empty, which means the root section. So <c>a b</c>, <c>alpha:</c>
    /// and <c>alpha:beta:x</c> are all invalid keys.
    /// </remarks>
    | InvalidKey of Key: string

    /// <summary>
    /// A line that isn't a section header, a parameter, a comment or a blank line. Returned by
    /// <c>FromLines</c>, <c>FromFile</c>, <c>AppendLines</c> and <c>AppendFile</c> / <c>Ini.fromLines</c>,
    /// <c>Ini.fromFile</c>, <c>Ini.appendLines</c> and <c>Ini.appendFile</c>.
    /// </summary>
    | InvalidLine of Line: string

#if NET
    /// <summary>
    /// A value that can't be converted to the requested type. Returned by <c>TryFindValue</c> and
    /// <c>TryFindNestedValue</c> (<c>Ini.tryFindValue</c> / <c>Ini.tryFindNestedValue</c>) when the key
    /// exists but its value can't be parsed.
    /// </summary>
    /// <remarks>
    /// Parsing uses <c>T.TryParse</c> with <c>CultureInfo.InvariantCulture</c>. A missing key returns
    /// <c>None</c>, never this error. This case and the two lookups that return it exist only in the
    /// <c>net10.0</c> build, because <c>net48</c> has no <c>IParsable&lt;T&gt;</c>. So
    /// <c>ParserError</c> has three cases on <c>net10.0</c> and two on <c>net48</c>.
    /// </remarks>
    | InvalidValue of Value: string
#endif

/// <summary>
/// An immutable INI document: a set of parameters, each stored under a key of the form
/// <c>section:parameter</c>.
/// </summary>
/// <remarks>
/// <para>
/// An <c>Ini</c> is a value. Any operation that changes a document returns a new one and leaves the
/// original unchanged. Two documents are equal if they have the same keys and the same values. Letter case
/// in names, the order of entries, comments and blank lines don't matter; values and their case do. Equal
/// documents have the same hash code. Documents can also be sorted: they are compared by their keys first
/// and then by their values, consistently with equality.
/// </para>
/// <para>
/// The structure is flat: there are no nested objects. Lookup, enumeration and writing all use the same
/// list of pairs, in key order. The root section comes first, then the other sections sorted
/// case-insensitively with a parent before its children, and parameters are sorted within each section.
/// To get the pairs, enumerate the document: it is an
/// <c>IEnumerable&lt;KeyValuePair&lt;string, string&gt;&gt;</c>, with keys of the form
/// <c>section:parameter</c> and root keys of the form <c>:name</c>. In F#, you can also use
/// <c>Ini.toSeq</c>.
/// </para>
/// <para>
/// Keys are case-insensitive (<c>OrdinalIgnoreCase</c>). A key without a colon belongs to the root
/// section, so <c>x</c> and <c>:x</c> are the same key. Keys are never trimmed or corrected. An invalid key
/// is simply not found: <c>TryFind</c> returns <c>None</c>, <c>ContainsKey</c> returns <c>false</c>, and
/// <c>Remove</c> does nothing.
/// </para>
/// <para>
/// Problems with the content, such as a line that can't be read or an invalid key, are returned as
/// <c>Error</c> of <c>ParserError</c>, not thrown. A missing key is not an error: <c>TryFind</c> and
/// <c>TryFindNested</c> return an option. The typed versions <c>TryFindValue</c> and
/// <c>TryFindNestedValue</c> (<c>net10.0</c> only) return an option that contains a result. They return
/// <c>Some</c> of <c>Error</c> of <c>InvalidValue</c> when the key exists but its value can't be parsed.
/// A <c>null</c> argument or a file system problem throws an exception.
/// </para>
/// </remarks>
[<Sealed>]
type Ini =
    /// <summary>The empty document.</summary>
    static member Empty: Ini

    /// <summary>Creates a document from key/value pairs.</summary>
    /// <param name="pairs">
    /// The pairs to add, each with a key of the form <c>section:parameter</c>. If the same key appears
    /// twice, the later value wins. The pairs are read one at a time, and reading stops at the first
    /// invalid key.
    /// </param>
    /// <returns>
    /// <c>Ok</c> of the document, or <c>Error</c> of <c>InvalidKey</c> for the first invalid key.
    /// </returns>
    /// <exception cref="T:System.ArgumentNullException">
    /// <paramref name="pairs"/>, or the key or value of one of the pairs, is <c>null</c>.
    /// </exception>
    /// <remarks>
    /// Accepts the same pairs that enumerating a document gives, so <c>Ini.Create(ini)</c> returns an
    /// equal document.
    /// </remarks>
    static member Create: pairs: seq<KeyValuePair<string, string>> -> Result<Ini, ParserError>

    /// <summary>Reads a document from a file.</summary>
    /// <param name="path">The path of the file to read.</param>
    /// <returns>
    /// <c>Ok</c> of the document, or <c>Error</c> of <c>InvalidLine</c> for the first line that can't be
    /// read.
    /// </returns>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="path"/> is <c>null</c>.</exception>
    /// <exception cref="T:System.IO.FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="T:System.IO.DirectoryNotFoundException">The path is invalid.</exception>
    /// <exception cref="T:System.IO.IOException">The file can't be read.</exception>
    /// <remarks>
    /// Lines are read one at a time, and reading stops at the first invalid line. If line 3 of a
    /// 10,000-line file is invalid, only three lines are read. The file is always closed afterwards.
    /// </remarks>
    static member FromFile: path: string -> Result<Ini, ParserError>

    /// <summary>Reads a document from lines.</summary>
    /// <param name="lines">
    /// The lines to read. <c>[name]</c> opens a section, <c>name = value</c> is a parameter in the current
    /// section, <c>; text</c> and <c># text</c> are comments, and blank lines are ignored. Anything else
    /// is an error. Parameters before the first section header belong to the root section.
    /// </param>
    /// <returns>
    /// <c>Ok</c> of the document, or <c>Error</c> of <c>InvalidLine</c> for the first line that can't be
    /// read.
    /// </returns>
    /// <exception cref="T:System.ArgumentNullException">
    /// <paramref name="lines"/>, or one of the lines, is <c>null</c>.
    /// </exception>
    /// <remarks>
    /// The value is everything after the first <c>=</c>, with whitespace trimmed from both ends. It can
    /// contain <c>= : ; #</c>, and it can be empty. If the same key appears twice, the later value wins,
    /// and a section can be opened again further down.
    /// </remarks>
    static member FromLines: lines: seq<string> -> Result<Ini, ParserError>

    /// <summary>Reads a file on top of this document.</summary>
    /// <param name="path">The path of the file to read.</param>
    /// <returns>
    /// <c>Ok</c> of a new document, or <c>Error</c> of <c>InvalidLine</c> for the first line that can't be
    /// read. If the file and this document have the same key, the file's value wins; all other parameters
    /// of this document are kept.
    /// </returns>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="path"/> is <c>null</c>.</exception>
    /// <exception cref="T:System.IO.FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="T:System.IO.DirectoryNotFoundException">The path is invalid.</exception>
    /// <exception cref="T:System.IO.IOException">The file can't be read.</exception>
    /// <remarks>
    /// The file is read with the same rules as <c>FromLines</c>, so it starts in the root section. The
    /// load order decides which value wins, but never changes which key a line is stored under.
    /// </remarks>
    member AppendFile: path: string -> Result<Ini, ParserError>

    /// <summary>Reads lines on top of this document.</summary>
    /// <param name="lines">The lines to read, with the same rules as <c>FromLines</c>.</param>
    /// <returns>
    /// <c>Ok</c> of a new document, or <c>Error</c> of <c>InvalidLine</c> for the first line that can't be
    /// read. If the lines and this document have the same key, the new value wins; all other parameters
    /// of this document are kept.
    /// </returns>
    /// <exception cref="T:System.ArgumentNullException">
    /// <paramref name="lines"/>, or one of the lines, is <c>null</c>.
    /// </exception>
    /// <remarks>
    /// The lines always start in the root section, so <c>Ini.Empty.AppendLines(lines)</c> is the same as
    /// <c>Ini.FromLines(lines)</c>. To add parameters to a section, the lines must open that section
    /// themselves.
    /// </remarks>
    member AppendLines: lines: seq<string> -> Result<Ini, ParserError>

    /// <summary>Whether the document has no parameters.</summary>
    member IsEmpty: bool

    /// <summary>Adds a parameter, or replaces the value if the key already exists.</summary>
    /// <param name="key">The key, of the form <c>section:parameter</c>.</param>
    /// <param name="value">The value, stored exactly as given.</param>
    /// <returns>
    /// <c>Ok</c> of a new document with the parameter, or <c>Error</c> of <c>InvalidKey</c> if the key is
    /// invalid.
    /// </returns>
    /// <exception cref="T:System.ArgumentNullException">
    /// <paramref name="key"/> or <paramref name="value"/> is <c>null</c>.
    /// </exception>
    member Add: key: string * value: string -> Result<Ini, ParserError>

    /// <summary>Finds the value of a key, looking only in the given section.</summary>
    /// <param name="key">The key, of the form <c>section:parameter</c>.</param>
    /// <returns><c>Some</c> of the value, or <c>None</c> if the key doesn't exist.</returns>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is <c>null</c>.</exception>
    /// <remarks>
    /// Doesn't look in parent sections; use <c>TryFindNested</c> for that. In C#, the result is a
    /// <c>FSharpOption&lt;string&gt;</c> that is <c>null</c> when the key is missing, and its
    /// <c>Value</c> property holds the value.
    /// </remarks>
    member TryFind: key: string -> string option

#if NET
    /// <summary>
    /// Finds the value of a key, looking only in the given section, and converts it to any
    /// <c>IParsable</c> type.
    /// </summary>
    /// <typeparam name="T">The type to convert the value to. It must implement <c>IParsable&lt;T&gt;</c>.</typeparam>
    /// <param name="key">The key, of the form <c>section:parameter</c>.</param>
    /// <returns>
    /// <c>Some</c> of <c>Ok</c> of the converted value if the key exists and its value can be parsed,
    /// <c>Some</c> of <c>Error</c> of <c>InvalidValue</c> if the key exists but its value can't be parsed,
    /// or <c>None</c> if the key doesn't exist.
    /// </returns>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is <c>null</c>.</exception>
    /// <remarks>
    /// Doesn't look in parent sections. Parsing uses <c>T.TryParse</c> with
    /// <c>CultureInfo.InvariantCulture</c>, so it works with the primitive types, <c>Guid</c>,
    /// <c>DateTime</c>, <c>TimeSpan</c> and your own types that implement the interface. The interface
    /// doesn't accept <c>NumberStyles</c>, so each type uses its own default rules. For example,
    /// <c>float</c> allows a thousands separator and reads <c>3,14</c> as <c>314</c>, but <c>int</c>
    /// doesn't. Not available on <c>net48</c>, which has no <c>IParsable&lt;T&gt;</c>.
    /// </remarks>
    member TryFindValue<'T when 'T :> IParsable<'T>> : key: string -> Result<'T, ParserError> option
#endif

    /// <summary>Finds the value of a key, looking in parent sections if needed.</summary>
    /// <param name="key">The key, of the form <c>section:parameter</c>.</param>
    /// <returns>
    /// <c>Some</c> of the value from the given section. If the section doesn't have the parameter, the
    /// value comes from the closest parent section that does (<c>a.b</c> is the parent of <c>a.b.c</c>),
    /// and then from the root section. <c>None</c> if none of them has the parameter.
    /// </returns>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is <c>null</c>.</exception>
    /// <example>
    /// For the document <c>timeout = 30</c>, <c>[server] host = localhost</c>,
    /// <c>[server.dev] port = 5000</c>: <c>server.dev:port</c> gives <c>5000</c> from the section itself,
    /// <c>server.dev:host</c> gives <c>localhost</c> from <c>[server]</c>, and
    /// <c>server.dev:timeout</c> gives <c>30</c> from the root.
    /// </example>
    member TryFindNested: key: string -> string option

#if NET
    /// <summary>
    /// Finds the value of a key, looking in parent sections if needed, and converts it to any
    /// <c>IParsable</c> type.
    /// </summary>
    /// <typeparam name="T">The type to convert the value to. It must implement <c>IParsable&lt;T&gt;</c>.</typeparam>
    /// <param name="key">The key, of the form <c>section:parameter</c>.</param>
    /// <returns>
    /// <c>Some</c> of <c>Ok</c> of the converted value if the key is found, <c>Some</c> of <c>Error</c> of
    /// <c>InvalidValue</c> if the value found can't be parsed, or <c>None</c> if neither the given
    /// section, its parent sections nor the root section has the parameter.
    /// </returns>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is <c>null</c>.</exception>
    /// <remarks>
    /// Looks in the same sections as <c>TryFindNested</c>. Parsing uses <c>T.TryParse</c> with
    /// <c>CultureInfo.InvariantCulture</c>. Not available on <c>net48</c>, which has no
    /// <c>IParsable&lt;T&gt;</c>.
    /// </remarks>
    member TryFindNestedValue<'T when 'T :> IParsable<'T>> : key: string -> Result<'T, ParserError> option
#endif

    /// <summary>Removes a parameter.</summary>
    /// <param name="key">The key, of the form <c>section:parameter</c>.</param>
    /// <returns>
    /// A new document without the parameter. If the key doesn't exist, the result is equal to this
    /// document.
    /// </returns>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is <c>null</c>.</exception>
    member Remove: key: string -> Ini

    /// <summary>The number of parameters in the document.</summary>
    member Count: int

    /// <summary>Whether the document has the key.</summary>
    /// <param name="key">The key, of the form <c>section:parameter</c>.</param>
    /// <returns><c>true</c> if the key exists. Letter case is ignored.</returns>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is <c>null</c>.</exception>
    member ContainsKey: key: string -> bool

    /// <summary>The keys, in key order, each of the form <c>section:parameter</c>.</summary>
    /// <remarks>Root keys have the form <c>:name</c>.</remarks>
    member Keys: seq<string>

    /// <summary>The values, in the same order as <c>Keys</c>.</summary>
    member Values: seq<string>

    /// <summary>The section names, each listed once, in key order. The root section is <c>""</c>.</summary>
    /// <remarks>
    /// A dotted name like <c>server.dev</c> is a separate section, so <c>[server]</c> and
    /// <c>[server.dev]</c> are two names.
    /// </remarks>
    member Sections: seq<string>

    /// <summary>Returns one section as a new document.</summary>
    /// <param name="name">The section name, case-insensitive. Use <c>""</c> for the root section.</param>
    /// <returns>
    /// A new document with only that section's parameters, under the same keys, or the empty document if
    /// there is no such section.
    /// </returns>
    /// <remarks>
    /// Child sections like <c>name.child</c> are not included. The result is a complete document that can
    /// be written and read back. This document is not changed. A <c>null</c> name is not checked; it
    /// simply matches nothing.
    /// </remarks>
    member Section: name: string -> Ini

    /// <summary>Removes a whole section. The opposite of <c>Section</c>.</summary>
    /// <param name="name">The section name, case-insensitive. Use <c>""</c> for the root section.</param>
    /// <returns>
    /// A new document without that section's parameters. If there is no such section, the result is
    /// equal to this document.
    /// </returns>
    /// <remarks>
    /// Child sections are separate sections, so removing <c>alpha</c> keeps <c>alpha.beta</c>. To remove
    /// both, remove each one by name, or loop over <c>Sections</c>. A <c>null</c> name is not checked; it
    /// simply matches nothing.
    /// </remarks>
    member RemoveSection: name: string -> Ini

    /// <summary>Writes the document as lines.</summary>
    /// <returns>
    /// The lines in a standard layout, produced on demand. Root parameters come first, without a header.
    /// Then each section follows as a <c>[section]</c> block, with a blank line between blocks, all in key
    /// order. An empty value is written as <c>x =</c>.
    /// </returns>
    /// <remarks>
    /// The output doesn't reproduce the original input: comments, blank lines, the original order and
    /// duplicate keys are lost. All values are kept, so writing a document and reading it back gives an
    /// equal document.
    /// </remarks>
    member ToLines: unit -> seq<string>

    /// <summary>Writes the document to a file, in the same layout as <c>ToLines</c>.</summary>
    /// <param name="path">The path of the file to write. An existing file is overwritten.</param>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="path"/> is <c>null</c>.</exception>
    /// <exception cref="T:System.IO.DirectoryNotFoundException">The path is invalid.</exception>
    /// <exception cref="T:System.IO.IOException">The file can't be written.</exception>
    /// <remarks>Uses <c>File.WriteAllLines</c>, so each line ends with the platform's newline.</remarks>
    member ToFile: path: string -> unit

    /// <summary>Writes the document as a string, in the same layout as <c>ToLines</c>.</summary>
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
/// Functions for <see cref="T:Fini.Ini"/>. The document is always the last argument, so the functions
/// work with <c>|&gt;</c>.
/// </summary>
[<RequireQualifiedAccess>]
module Ini =
    /// <summary>The empty document.</summary>
    val empty: Ini

    /// <summary>Creates a document from key/value tuples.</summary>
    /// <param name="pairs">
    /// The pairs to add, each with a key of the form <c>section:parameter</c>. If the same key appears
    /// twice, the later value wins. The pairs are read one at a time, and reading stops at the first
    /// invalid key.
    /// </param>
    /// <returns>
    /// <c>Ok</c> of the document, or <c>Error</c> of <c>InvalidKey</c> for the first invalid key.
    /// </returns>
    /// <exception cref="T:System.ArgumentNullException">
    /// <paramref name="pairs"/>, or the key or value of one of the tuples, is <c>null</c>.
    /// </exception>
    /// <remarks>
    /// Accepts the same tuples that <c>toSeq</c> returns, so <c>ini |&gt; Ini.toSeq |&gt; Ini.ofSeq</c>
    /// returns an equal document.
    /// </remarks>
    val ofSeq: pairs: seq<string * string> -> Result<Ini, ParserError>

    /// <summary>Reads a document from a file.</summary>
    /// <param name="path">The path of the file to read.</param>
    /// <returns>
    /// <c>Ok</c> of the document, or <c>Error</c> of <c>InvalidLine</c> for the first line that can't be
    /// read.
    /// </returns>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="path"/> is <c>null</c>.</exception>
    /// <exception cref="T:System.IO.FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="T:System.IO.DirectoryNotFoundException">The path is invalid.</exception>
    /// <exception cref="T:System.IO.IOException">The file can't be read.</exception>
    /// <remarks>
    /// Lines are read one at a time, and reading stops at the first invalid line. The file is always
    /// closed afterwards.
    /// </remarks>
    val fromFile: path: string -> Result<Ini, ParserError>

    /// <summary>Reads a document from lines.</summary>
    /// <param name="lines">
    /// The lines to read. <c>[name]</c> opens a section, <c>name = value</c> is a parameter in the current
    /// section, <c>; text</c> and <c># text</c> are comments, and blank lines are ignored. Anything else
    /// is an error. Parameters before the first section header belong to the root section.
    /// </param>
    /// <returns>
    /// <c>Ok</c> of the document, or <c>Error</c> of <c>InvalidLine</c> for the first line that can't be
    /// read.
    /// </returns>
    /// <exception cref="T:System.ArgumentNullException">
    /// <paramref name="lines"/>, or one of the lines, is <c>null</c>.
    /// </exception>
    val fromLines: lines: seq<string> -> Result<Ini, ParserError>

    /// <summary>Reads a file on top of a document.</summary>
    /// <param name="path">The path of the file to read.</param>
    /// <param name="ini">The existing document. It is not changed.</param>
    /// <returns>
    /// <c>Ok</c> of a new document, or <c>Error</c> of <c>InvalidLine</c> for the first line that can't be
    /// read. If the file and the document have the same key, the file's value wins; all other parameters
    /// of the document are kept.
    /// </returns>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="path"/> is <c>null</c>.</exception>
    /// <exception cref="T:System.IO.FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="T:System.IO.DirectoryNotFoundException">The path is invalid.</exception>
    /// <exception cref="T:System.IO.IOException">The file can't be read.</exception>
    /// <remarks>
    /// The file is read with the same rules as <c>fromLines</c>, so it starts in the root section. The
    /// load order decides which value wins, but never changes which key a line is stored under.
    /// </remarks>
    val appendFile: path: string -> ini: Ini -> Result<Ini, ParserError>

    /// <summary>Reads lines on top of a document.</summary>
    /// <param name="lines">The lines to read, with the same rules as <c>fromLines</c>.</param>
    /// <param name="ini">The existing document. It is not changed.</param>
    /// <returns>
    /// <c>Ok</c> of a new document, or <c>Error</c> of <c>InvalidLine</c> for the first line that can't be
    /// read. If the lines and the document have the same key, the new value wins; all other parameters of
    /// the document are kept.
    /// </returns>
    /// <exception cref="T:System.ArgumentNullException">
    /// <paramref name="lines"/>, or one of the lines, is <c>null</c>.
    /// </exception>
    /// <remarks>
    /// The lines always start in the root section, so <c>Ini.empty |&gt; Ini.appendLines lines</c> is the
    /// same as <c>Ini.fromLines lines</c>.
    /// </remarks>
    val appendLines: lines: seq<string> -> ini: Ini -> Result<Ini, ParserError>

    /// <summary>Whether a document has no parameters.</summary>
    /// <param name="ini">The document.</param>
    /// <returns><c>true</c> if the document is empty.</returns>
    val isEmpty: ini: Ini -> bool

    /// <summary>Adds a parameter, or replaces the value if the key already exists.</summary>
    /// <param name="key">The key, of the form <c>section:parameter</c>.</param>
    /// <param name="value">The value, stored exactly as given.</param>
    /// <param name="ini">The existing document. It is not changed.</param>
    /// <returns>
    /// <c>Ok</c> of a new document with the parameter, or <c>Error</c> of <c>InvalidKey</c> if the key is
    /// invalid.
    /// </returns>
    /// <exception cref="T:System.ArgumentNullException">
    /// <paramref name="key"/> or <paramref name="value"/> is <c>null</c>.
    /// </exception>
    val add: key: string -> value: string -> ini: Ini -> Result<Ini, ParserError>

    /// <summary>Finds the value of a key, looking only in the given section.</summary>
    /// <param name="key">The key, of the form <c>section:parameter</c>.</param>
    /// <param name="ini">The document.</param>
    /// <returns><c>Some</c> of the value, or <c>None</c> if the key doesn't exist.</returns>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is <c>null</c>.</exception>
    /// <remarks>Doesn't look in parent sections; use <c>tryFindNested</c> for that.</remarks>
    val tryFind: key: string -> ini: Ini -> string option

#if NET
    /// <summary>
    /// Finds the value of a key, looking only in the given section, and converts it to any
    /// <c>IParsable</c> type.
    /// </summary>
    /// <typeparam name="T">The type to convert the value to. It must implement <c>IParsable&lt;T&gt;</c>.</typeparam>
    /// <param name="key">The key, of the form <c>section:parameter</c>.</param>
    /// <param name="ini">The document.</param>
    /// <returns>
    /// <c>Some</c> of <c>Ok</c> of the converted value if the key exists and its value can be parsed,
    /// <c>Some</c> of <c>Error</c> of <c>InvalidValue</c> if the key exists but its value can't be parsed,
    /// or <c>None</c> if the key doesn't exist.
    /// </returns>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is <c>null</c>.</exception>
    /// <remarks>
    /// Doesn't look in parent sections. Parsing uses <c>T.TryParse</c> with
    /// <c>CultureInfo.InvariantCulture</c>, so it works with the primitive types, <c>Guid</c>,
    /// <c>DateTime</c>, <c>TimeSpan</c> and your own types that implement the interface. The interface
    /// doesn't accept <c>NumberStyles</c>, so each type uses its own default rules. For example,
    /// <c>float</c> allows a thousands separator and reads <c>3,14</c> as <c>314</c>, but <c>int</c>
    /// doesn't. Not available on <c>net48</c>, which has no <c>IParsable&lt;T&gt;</c>.
    /// </remarks>
    /// <example><c>ini |&gt; Ini.tryFindValue&lt;int&gt; "server:port"</c></example>
    val tryFindValue<'T when 'T :> IParsable<'T>> : key: string -> ini: Ini -> Result<'T, ParserError> option
#endif

    /// <summary>Finds the value of a key, looking in parent sections if needed.</summary>
    /// <param name="key">The key, of the form <c>section:parameter</c>.</param>
    /// <param name="ini">The document.</param>
    /// <returns>
    /// <c>Some</c> of the value from the given section. If the section doesn't have the parameter, the
    /// value comes from the closest parent section that does (<c>a.b</c> is the parent of <c>a.b.c</c>),
    /// and then from the root section. <c>None</c> if none of them has the parameter.
    /// </returns>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is <c>null</c>.</exception>
    /// <example>
    /// For the document <c>timeout = 30</c>, <c>[server] host = localhost</c>,
    /// <c>[server.dev] port = 5000</c>: <c>server.dev:port</c> gives <c>5000</c> from the section itself,
    /// <c>server.dev:host</c> gives <c>localhost</c> from <c>[server]</c>, and
    /// <c>server.dev:timeout</c> gives <c>30</c> from the root.
    /// </example>
    val tryFindNested: key: string -> ini: Ini -> string option

#if NET
    /// <summary>
    /// Finds the value of a key, looking in parent sections if needed, and converts it to any
    /// <c>IParsable</c> type.
    /// </summary>
    /// <typeparam name="T">The type to convert the value to. It must implement <c>IParsable&lt;T&gt;</c>.</typeparam>
    /// <param name="key">The key, of the form <c>section:parameter</c>.</param>
    /// <param name="ini">The document.</param>
    /// <returns>
    /// <c>Some</c> of <c>Ok</c> of the converted value if the key is found, <c>Some</c> of <c>Error</c> of
    /// <c>InvalidValue</c> if the value found can't be parsed, or <c>None</c> if neither the given
    /// section, its parent sections nor the root section has the parameter.
    /// </returns>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is <c>null</c>.</exception>
    /// <remarks>
    /// Looks in the same sections as <c>tryFindNested</c>. Parsing uses <c>T.TryParse</c> with
    /// <c>CultureInfo.InvariantCulture</c>. Not available on <c>net48</c>, which has no
    /// <c>IParsable&lt;T&gt;</c>.
    /// </remarks>
    /// <example><c>ini |&gt; Ini.tryFindNestedValue&lt;int&gt; "server.dev:port"</c></example>
    val tryFindNestedValue<'T when 'T :> IParsable<'T>> : key: string -> ini: Ini -> Result<'T, ParserError> option
#endif

    /// <summary>Removes a parameter.</summary>
    /// <param name="key">The key, of the form <c>section:parameter</c>.</param>
    /// <param name="ini">The existing document. It is not changed.</param>
    /// <returns>
    /// A new document without the parameter. If the key doesn't exist, the result is equal to the given
    /// document.
    /// </returns>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is <c>null</c>.</exception>
    val remove: key: string -> ini: Ini -> Ini

    /// <summary>The number of parameters in a document.</summary>
    /// <param name="ini">The document.</param>
    /// <returns>The number of parameters.</returns>
    val count: ini: Ini -> int

    /// <summary>Whether a document has the key.</summary>
    /// <param name="key">The key, of the form <c>section:parameter</c>.</param>
    /// <param name="ini">The document.</param>
    /// <returns><c>true</c> if the key exists. Letter case is ignored.</returns>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is <c>null</c>.</exception>
    val containsKey: key: string -> ini: Ini -> bool

    /// <summary>The keys of a document, in key order, each of the form <c>section:parameter</c>.</summary>
    /// <param name="ini">The document.</param>
    /// <returns>The keys. Root keys have the form <c>:name</c>.</returns>
    val keys: ini: Ini -> seq<string>

    /// <summary>The values of a document, in the same order as <c>keys</c>.</summary>
    /// <param name="ini">The document.</param>
    /// <returns>The values.</returns>
    val values: ini: Ini -> seq<string>

    /// <summary>The section names of a document, each listed once, in key order.</summary>
    /// <param name="ini">The document.</param>
    /// <returns>The names. The root section is <c>""</c>.</returns>
    /// <remarks>
    /// A dotted name like <c>server.dev</c> is a separate section, so <c>[server]</c> and
    /// <c>[server.dev]</c> are two names.
    /// </remarks>
    val sections: ini: Ini -> seq<string>

    /// <summary>Returns one section as a new document.</summary>
    /// <param name="name">The section name, case-insensitive. Use <c>""</c> for the root section.</param>
    /// <param name="ini">The existing document. It is not changed.</param>
    /// <returns>
    /// A new document with only that section's parameters, under the same keys, or the empty document if
    /// there is no such section.
    /// </returns>
    /// <remarks>
    /// Child sections like <c>name.child</c> are not included. The result is a complete document:
    /// <c>ini |&gt; Ini.section "alpha" |&gt; Ini.toLines</c> gives <c>[ "[alpha]"; "x = 1" ]</c>. A
    /// <c>null</c> name is not checked; it simply matches nothing.
    /// </remarks>
    val section: name: string -> ini: Ini -> Ini

    /// <summary>Removes a whole section. The opposite of <c>section</c>.</summary>
    /// <param name="name">The section name, case-insensitive. Use <c>""</c> for the root section.</param>
    /// <param name="ini">The existing document. It is not changed.</param>
    /// <returns>
    /// A new document without that section's parameters. If there is no such section, the result is
    /// equal to the given document.
    /// </returns>
    /// <remarks>
    /// Child sections are separate sections, so removing <c>alpha</c> keeps <c>alpha.beta</c>. To remove
    /// both, remove each one by name, or loop over <c>Ini.sections</c>. A <c>null</c> name is not checked;
    /// it simply matches nothing.
    /// </remarks>
    val removeSection: name: string -> ini: Ini -> Ini

    /// <summary>The parameters of a document as key/value tuples, in key order.</summary>
    /// <param name="ini">The document.</param>
    /// <returns>The pairs, in the form <c>ofSeq</c> accepts.</returns>
    val toSeq: ini: Ini -> seq<string * string>

    /// <summary>Writes a document as lines.</summary>
    /// <param name="ini">The document.</param>
    /// <returns>
    /// The lines in a standard layout, produced on demand. Root parameters come first, without a header.
    /// Then each section follows as a <c>[section]</c> block, with a blank line between blocks, all in key
    /// order. An empty value is written as <c>x =</c>.
    /// </returns>
    /// <remarks>
    /// The output doesn't reproduce the original input: comments, blank lines, the original order and
    /// duplicate keys are lost. All values are kept, so writing a document and reading it back gives an
    /// equal document.
    /// </remarks>
    val toLines: ini: Ini -> seq<string>

    /// <summary>Writes a document to a file, in the same layout as <c>toLines</c>.</summary>
    /// <param name="path">The path of the file to write. An existing file is overwritten.</param>
    /// <param name="ini">The document.</param>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="path"/> is <c>null</c>.</exception>
    /// <exception cref="T:System.IO.DirectoryNotFoundException">The path is invalid.</exception>
    /// <exception cref="T:System.IO.IOException">The file can't be written.</exception>
    /// <remarks>Uses <c>File.WriteAllLines</c>, so each line ends with the platform's newline.</remarks>
    val toFile: path: string -> ini: Ini -> unit

    /// <summary>Writes a document as a string, in the same layout as <c>toLines</c>.</summary>
    /// <param name="ini">The document.</param>
    /// <returns>The lines joined with <c>Environment.NewLine</c>.</returns>
    val toString: ini: Ini -> string
