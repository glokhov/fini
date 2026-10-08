# TODO

Current state: a document is parsed into a flattened `Map<Key, string>`, where a `Key` is a section and a
parameter and renders as `section:parameter`.
Construction (`fromLines`, `fromFile`, `ofSeq`), appending (`appendLines`, `appendFile`), lookup (`tryFind`,
`tryFindNested`, `find`, `findNested`, `containsKey`), enumeration (`keys`, `values`, `toSeq`), single-key
mutation (`add`, `remove`) and emitting (`toLines`, `toString`, `toFile`) are all in place, so the API is
read-write and round-trips. What is left is **escaping** (§6), without which inline comments cannot be
supported and a value carrying padding or a newline cannot be rendered back.

The suite is 264 tests: 213 in `Fini.Tests` (F#) and 51 in `Fini.CSharp.Tests`.

This file tracks what is needed to get to a complete read-write API. Items marked **blocker** should be
settled before the features that depend on them, because they change the key model or the public surface.

---

## 1. Key model is ambiguous — **done**

Resolved by option **(a)**: `:` is excluded from the name charset in both `sectionRegex` and
`parameterRegex` (`[^=:;#\s\[\]]+`). The colon is now reserved purely as the key separator.

```fsharp
Ini.fromLines [ "[a:b]"; "x = 1" ]   // Error "Cannot parse line: [a:b]."
Ini.fromLines [ "[a]"; "b:x = 2" ]   // Error "Cannot parse line: b:x = 2."
```

Both documents previously produced the same key, `a:b:x`. Every key now holds exactly one `:` and splits
unambiguously into a section and a parameter, which is what `toLines` (§4) needs to regroup keys by
section, and it makes `tryFindNested`'s first-colon split well defined. Values are unaffected and may still
contain colons (`url = https://example.com:8080`).

- [x] Decide the fix — option (a).
- [x] Tests pinning that a section name and a parameter name cannot be confused.
- [x] README name charset updated to include `:`.
- [x] **`Key` now holds the two parts rather than the joined path.** The record is
      `{ Section: string; Parameter: string }`, with `Path` a computed `Section + ":" + Parameter` kept for
      the one place a string is needed — the `convert` projection behind `keys` and the pair enumeration
      (§4). Since option (a) above guarantees exactly one `:` per key, the split is total, so making it
      eager loses nothing.

      This is what the rest of the file had been working around. `toLines` (§5) was splitting the path back
      apart twice per pair and stripping the leading `:` off the parameter by hand; `tryFindNested` was
      re-concatenating and re-splitting a path on every step up the hierarchy; `appendLines` and the key
      parser were building `section + ":" + name` for the map to take apart again. All of that is now field
      access, and `Key.split` and the `String.contains` helper are gone.

      The comparison changed with it, and that is the substantive part: **root section first, then section
      `OrdinalIgnoreCase`, then parameter.** Comparing the parts rather than the joined string removed two
      artefacts of the `:` separator. A section named `0` or `-` used to sort ahead of the root section,
      because `:` is `0x3A`; and `server.dev` used to sort ahead of `server`, because `.` is `0x2E` and beat
      the `:` that terminated the parent. Now the root section leads unconditionally, each section is one
      contiguous run, and a parent precedes its children. Two test assertions moved as a result, both to the
      new order, and a test was added pinning parent-before-child.

      `Key.create` still takes the parts as given: it splits at the first `:` and does not repair or trim,
      so a key the parser would reject stays unfindable rather than turning into a different one, as §3
      requires of the lookups.

Note for §5: if escaping is later extended to names, `:` must stay excluded or escapable-only, otherwise
this ambiguity returns.

## 2. Naming — **done**

The rule: **only a function that returns an `option` carries the `try` prefix.** That is `tryFind` and
`tryFindNested`, where a miss is an answer rather than a failure.

Everything else is expected to work, and when it cannot it returns `Result<_, string>` instead of raising —
`fromLines`, `fromFile`, `ofSeq`, `appendLines`, `appendFile`, `add`. The signature already says the call
may not produce an `Ini`, so a `try` prefix would add nothing. Functions that cannot fail return their
value directly: `isEmpty`, `count`, `keys`, `values`, `toSeq`, `containsKey`, `remove`.

- [x] `tryCreate` → `fromLines`, `tryAppend` → `appendLines`, `tryAdd` → `add`.
- [x] `ofSeq` rather than `tryOfSeq`, likewise for every future insertion point.
- [x] **`find` and `findNested` keep raising `KeyNotFoundException`, by design.** They are the partial
      counterparts of `tryFind` and `tryFindNested` — same lookup, no `option` — for call sites where a
      missing key is a bug rather than a case to handle. They are not a gap in the `Result` convention:
      that convention covers failures the caller is expected to handle, and a partial function is the
      deliberate opposite. `Find` / `FindNested` behave identically on the C# side.

      The two now fail identically too. `findNested` raises `KeyNotFoundException()` with no message of
      its own, so it reads exactly like the one `Map.find` raises out of `find`; it used to carry
      `Key not found: <key>.`, which made the same failure look like two different ones. A test asserts
      the two messages agree rather than hardcoding the framework string, which is localisable.
- [x] `null` is an `ArgumentNullException` everywhere, including paths. `readLines` and `writeLines` now
      run `nullArgCheck "path"` before handing the path to `File.ReadLines` / `File.WriteAllLines`, so
      `fromFile null` raises instead of returning `Error "Value cannot be null. (Parameter 'path')"` as it
      briefly did. An empty path still comes back as `Error`, since it reaches `File.ReadLines` and is a
      runtime condition rather than a broken call. The README documents the split under "Errors", and both
      sides are pinned by tests.

## 3. Mutation API — **partly done**

All of these return a new `Ini`; the type stays immutable. Follow the existing convention — **the `Ini` is
always the last parameter** — so they compose with `|>`.

- [x] `Ini.add : string -> string -> Ini -> Result<Ini, string>` — overwrite on existing key, matching parse
      semantics where a duplicate key wins. Returns `Result` because of the validation decision below.
- [x] `Ini.remove : string -> Ini -> Ini` — no-op when the key is absent.
- [x] **Key validation decided: validate where a key enters the map, nowhere else.**

      A key supplied in code can contain characters the parser rejects (whitespace, `=`, `:`, `;`, `#`,
      `[`, `]`), and without a check it would be possible to build an `Ini` that `toLines` (§4) emits and
      `fromLines` then refuses to read back. There are exactly three insertion points: `Map.add` in `add`,
      in `ofSeq` and in `appendLines`. The last is guarded by `tryParseLine`, whose section and parameter
      regexes carry the charset; the other two are guarded by `tryParseKey`, which uses the same charset. So
      every key in the map is well formed, by construction.

      `add` and `ofSeq` therefore return `Result<Ini, string>` with `Error "Invalid key: <key>."`, rather
      than throwing — it is an expected failure, like a parse error, and composes with the rest of the API
      under `Result`.

      Everything else stays unvalidated. `remove` is a write but cannot break the invariant, since it only
      takes keys out; `tryFind`, `find`, `containsKey` and the nested variants only read. A key the parser
      would reject cannot be present, so the honest answer is a miss, not an error: `tryFind` gives `None`,
      `containsKey` gives `false`, `remove` is a no-op, `find` raises `KeyNotFoundException`. Validating
      there would cost a regex match per lookup and turn total functions into `Result`-returning ones for
      no gain.

      Keys are not trimmed anywhere, so `add " x "` is rejected rather than stored under a key the same
      string could not find again — `add` and `tryFind` agree on every key either of them accepts.
- [x] C# members: `Add` returning `Result<Ini, string>` and `Remove` returning a new `Ini`.
- [x] **`Ini.change` will not be implemented.** `Map.change` takes a key to a `'T option -> 'T option` and
      covers add/update/remove in one call, which reads well on a map the caller owns. An `Ini` is not that
      map: the key has to be parsed and validated first, so the signature would have to be
      `string -> (string option -> string option) -> Ini -> Result<Ini, string>` — a `Result` wrapping a
      function that itself decides between three outcomes, for a document format whose callers want
      `add` and `remove`. It is a `Map` operation, not an `Ini` one. No C# `Change` member either.
- [ ] Section-level operations, natural for the flattened layout:
      `Ini.removeSection : string -> Ini -> Ini`, and possibly `Ini.renameSection`. `removeSection` is a
      pure delete and needs no validation, so it returns an `Ini`; `renameSection` introduces a name, so it
      validates and returns `Result`. Cheaper since §1: a `Key` carries its `Section`, so both filter on a
      field instead of splitting a path, and the keys of a section are one contiguous run in key order.

## 4. Enumeration of key-value pairs — **partly done**

- [x] `Ini.toSeq : Ini -> KeyValuePair<string, string> seq`, in key order, aligned with `keys` and `values`.
- [x] `Ini.ofSeq : KeyValuePair<string, string> seq -> Result<Ini, string>` closes the round-trip with
      `toSeq`. It is an insertion point, so it validates each key through `tryParseKey` and reports the
      first one it rejects, exactly as `add` does. Like `appendLines` it parses straight into the map in a
      single short-circuiting pass over the enumerator, so an invalid key stops the walk immediately and the
      enumerator is disposed.
- [x] `IEnumerable<KeyValuePair<string, string>>` on `Ini`, plus the non-generic `IEnumerable`, mapping each
      `Key` to its string `Path` so the private key type stays private. Pairs come out in key order, the same
      order as `Keys` and `Values`.

      The map cannot simply be delegated to, even though `Map<_,_>` implements
      `IEnumerable<KeyValuePair<_,_>>`: it enumerates `KeyValuePair<Key, string>`, not
      `KeyValuePair<string, string>`, and `Key` is private so it cannot appear in a public interface. The
      `convert` projection is therefore unavoidable. It is the only one now, since both languages take the
      same shape; the non-generic implementation delegates to the generic interface.

      Both interface implementations were briefly self-recursive — each upcast `this` to the interface it
      was implementing and called `GetEnumerator` on it, which dispatches straight back to the same member.
      See the note at the end of this file. Delegating from the non-generic to the *generic* interface is
      fine, because that is a different interface and so a different member.
- [x] **`IReadOnlyDictionary` / `IReadOnlyCollection` were tried and removed.** They add surface without
      adding capability: `Count`, `ContainsKey`, `Keys` and `Values` already exist as members, and the
      indexer and `TryGetValue` that the interface requires duplicate `Find` and `TryFind` exactly. What
      they cost is real — two more interfaces to keep consistent, two members that exist only to satisfy
      them, and the explicit-implementation trap below. The pair enumeration is kept, because `foreach` and
      LINQ are not otherwise expressible.

      Worth knowing if this is ever revisited: **F# interface implementations are always explicit**, so
      putting `Count`, `ContainsKey`, `Keys` and `Values` *only* in an `interface ... with` block removes
      them from `Ini` itself — `error FS0039: The type 'Ini' does not define a field, constructor, or member
      named 'Count'` from the `Ini` module, and the same loss for C# callers, where an explicit
      implementation also needs a cast. Those members have to stay intrinsic, with the interface slots
      forwarding to them.
- [x] Tests for enumeration and for `toSeq` / `ofSeq`: F# and C# both, covering key order, agreement with
      `keys` / `values`, the empty `Ini`, LINQ, the non-generic `IEnumerable` path, the `toSeq`/`ofSeq`
      round trip, and `ofSeq` short-circuiting on the first invalid key.
- [x] **One pair type for both languages: `KeyValuePair<string, string>`.** `toSeq`, `ofSeq`,
      `KeyValuePairs` and the `IEnumerable` interface all speak it, so `toSeq` feeds `ofSeq` with no
      projection and C# can pass an `Ini` straight to `Ini.OfSeq`, since an `Ini` *is* a `KeyValuePair`
      sequence. Two earlier shapes were tried and dropped: tuples on both sides, which C# sees as
      `Tuple<string, string>` with `Item1` / `Item2`; and a split surface — tuples for F# through an
      internal `Pairs` member, `KeyValuePair` for C# — which read well in each language but made `ofSeq`
      and enumeration disagree, so a C# round trip needed a `Select`. The cost of the shared shape is that
      F# callers construct `KeyValuePair("x", "1")` rather than `"x", "1"`.
- [x] `Ini.OfSeq` is public. It was briefly `internal`, left over from the split surface above, which
      removed the only way for C# to build an `Ini` from pairs and broke four C# tests.
- [x] **`toList` / `toArray` / `ofList` / `ofArray` will not be added.** `ofSeq` already takes a list, an
      array or any other `seq` — `'a list` and `'a array` are `seq<'a>` — so `ofList` and `ofArray` would be
      aliases that add nothing. Verified with a list, an array and a `ResizeArray`. In the other direction
      `Ini.toSeq ini |> List.ofSeq` is one pipe, and C# gets `ToList()` / `ToArray()` from LINQ because
      `Ini` is an `IEnumerable`, pinned by a test.

      The one thing a strict `toList` would buy is a snapshot, and that only matters when the source can
      change under the caller. An `Ini` is immutable and `toSeq` is a `Seq.map` over an immutable `Map`, so
      enumerating it twice yields the same pairs — tested — and the lazy sequence is already as good as a
      copy. Four more functions, two more C# members, nothing new to do with them.
- [ ] Consider `Ini.sections : Ini -> string seq` (distinct section names, in order) and
      `Ini.section : string -> Ini -> KeyValuePair<string, string> seq` (the parameters of one section). Both are
      awkward to express from the outside once keys are flattened, but straightforward inside since §1:
      `sections` is the section-change walk `ToLines` already does, and `section` is a contiguous run.
- [ ] Keep `keys` / `values` for compatibility.

## 5. Rendering — **done**

`Ini.ToString()` used to leak the internal representation, `{ Map = map [({ Path = "a:x" }, "1")] }`, and
then a `"todo: implement Ini.ToString()"` placeholder. Both are gone.

- [x] `Ini.toLines : Ini -> string seq` is the primitive — the exact inverse of `fromLines`, which consumes
      lines, and it feeds `File.WriteAllLines` directly. `toString = String.concat "\n" (toLines ini)` and
      `toFile path = writeLines path (toLines ini)`.
- [x] `ToLines()` is public, so C# renders without going through `ToString()`; `ToString()` is overridden
      and `ToFile` added. `\n` rather than `Environment.NewLine`, so `toString` is the same on every
      platform — `toFile` is where the platform line terminator comes in, from `File.WriteAllLines`.
- [x] Canonical layout: root-section parameters first with no header, then one `[name]` header per section
      followed by its parameters, all in key order, with **a blank line before each header** (none before
      the first line of output, and no trailing blank line). An empty `Ini` renders to no lines at all.
- [x] **Root parameters must come first, and not only for looks.** A root parameter emitted after a header
      would be read back as part of that section, so `x = root` must precede `[0]` no matter how `0` and
      `:` compare. This used to need two passes over the map — root parameters, then the sections — because
      key order put `[ "-:b"; "0:a"; ":x" ]` for `x = root`, `[0] a = 1`, `[-] b = 2`: `:` is `0x3A`, so a
      section name starting with a digit, `-` or `.` sorted ahead of the root section.

      The `Key` change in §1 moved the rule into the comparison, where it belongs. Key order is now root
      section first, then section, then parameter, so the same document gives `[ ":x"; "-:b"; "0:a" ]` and
      one pass suffices: root parameters come out before any header by construction, and `ToLines` writes a
      header whenever the section changes. The test still pins both the key order and the rendering for
      exactly that document; the rendering is unchanged.
- [x] One header per section even when its name is spelled with different case in different places, since
      `[Alpha] x = 1` and `[alpha] y = 2` are two distinct keys in one logical section. The grouping
      compares names `OrdinalIgnoreCase` and keeps the first spelling. Contiguity is guaranteed by the key
      comparison, which orders on the section before the parameter, so no other section's keys can sort
      between them.
- [x] An empty value is written as `x =` rather than `x = `, so nothing has trailing whitespace.
- [x] Documented in the README as **canonical, not faithful**: comments, blank lines, declaration order,
      repeated-section layout and value padding are all lost.
- [x] Round-trip tests: `fromLines (toLines ini) = ini` over eleven documents, including dotted sections,
      reopened sections, empty values, a value containing `:` and `=`, a value containing `;`, and the
      digit/`-` section case above. Also for an `Ini` built by `ofSeq`, and for `toFile` + `fromFile`.
- [ ] The round trip is **not** total for an `Ini` built in code, because `add` and `ofSeq` do not
      constrain the value: surrounding whitespace is trimmed on the way back in (`add "x" " padded "`
      renders as `x =  padded ` and reads back as `padded`), and a value containing a newline renders as
      two lines, the second of which does not parse. Two tests pin the current behaviour so that escaping
      (§6) has a tripwire. Fixing it belongs to §6, not here — rendering cannot do better until a value
      can be escaped.

## 6. Escaping (prerequisite for inline comments)

Documented in the README as unsupported and planned. Inline comments cannot be added without it: stripping
`;` and `#` from values would make them impossible to write, and they appear in real values
(`separators = ;#`, `fragment = https://example.com/doc#section`).

- [ ] Choose an escape character (`\` is conventional) and the set of escapes:
      `;` `#` for inline comments, leading/trailing whitespace, newline for multi-line values, `=`, `:` if
      §1(a) is not taken, and the escape character itself.
- [ ] Unescape on parse, escape on render — §5 and §6 must be exact inverses or the round-trip test fails.
- [ ] Only then implement inline comment stripping, and update the README's "Inline comments" section.
- [ ] Decide whether escaping also applies to section and parameter names, which would let names contain
      characters currently rejected outright.

## 7. API surface and correctness fixes — **done**

- [x] `Fini.Key` is no longer exported. `type private Key` leaves only `Fini.Ini` and `Fini.IniModule` on
      the public surface.
- [x] `Key.Equals` returns `false` for a non-`Key` argument instead of raising, satisfying the
      `Object.Equals` contract. `IComparable.CompareTo` still raises `ArgumentException`, which is correct —
      that is the documented behaviour for comparison against an incompatible type.
- [x] `out` parameters of `TryFind` / `TryFindNested` annotated `[<Out; MaybeNullWhen(false)>]`.

      `NotNullWhen(true)` was tried first and is a **no-op** here. It only constrains a parameter that is
      already nullable, and F# emits no nullable metadata for this assembly, so C# sees a plain
      `out string` and assumes non-null regardless of the return value. Verified against a scratch
      `<Nullable>enable</Nullable>` C# project: with `NotNullWhen(true)` the build was clean, while
      `MaybeNullWhen(false)` correctly raises `CS8602` on a dereference after a `false` return and stays
      silent after a `true` one.
- [x] `Ini` structural equality is covered by tests: name case, declaration order, and comments/blank lines
      are all ignored; values and their case are respected; hash codes agree. Case-insensitive comparison is
      documented in the README under "Keys", together with a line stating that two `Ini` values can be
      compared at all and what the comparison does and does not take into account.
- [x] The unused private `KeyValuePair.deconstruct` helper has been removed. F# does not warn on unused
      private module functions, so these have to be noticed by eye. `File.writeLines` was the next one on
      the list and is now live, called by `toFile` (§9). The §1 `Key` change retired `String.contains`,
      whose only caller was the `ensureSeparator` that `Key.create` no longer needs; `String.indexOf` and
      `String.lastIndexOf` are both still in use. No dead private helpers remain.

## 8. Parsing

- [ ] Include the 1-based line number in parse errors. `Cannot parse line: oops.` does not locate the
      problem in a large file.
- [ ] Consider reporting all parse errors rather than only the first. Note this now conflicts with the
      short-circuiting below — collecting every error means reading the whole document again, so it would
      have to be an opt-in variant rather than the default.
- [x] **Done.** `Result.traverse` folded the entire sequence even after an error. It has been removed, and
      `appendLines` now parses straight into the map in a single short-circuiting pass over the enumerator:

      ```fsharp
      use enumerator = lines |> getEnumerator

      let rec loop map section =
          if moveNext enumerator |> not then Ok { Map = map }
          else
              match current enumerator |> tryParseLine with
              | Error err -> Error err
              | Ok line -> ...
      ```

      This also drops the intermediate whole-document `Line list`, so parsing is one pass instead of two.
      The explicit `use` is required: abandoning the enumerator early means nothing else will dispose it,
      which matters for a source like `File.ReadLines` that holds a file handle.

      Measured with an unparsable line at position 3 of 10,000: 3 lines consumed, previously all 10,000.
      A source with no errors is still read in full. Four regression tests cover it, and all four were
      confirmed to fail against the previous implementation — the infinite-sequence case hung until its
      10s timeout. `ofSeq` is built the same way and has the same four tests.
- [ ] `appendLines` restarts at the root section on every call, so appending `port = 9090` lands at `:port`
      regardless of the sections already present. Documented, but consider a variant that continues from the
      last section of the existing document.

## 9. Files — **partly done**

- [x] `Ini.fromFile : string -> Result<Ini, string>` and `Ini.appendFile : string -> Ini -> Result<Ini, string>`,
      plus `FromFile` / `AppendFile` on the C# side. Both read with `File.ReadLines`, so the document is
      streamed rather than held in memory twice, and both wrap the call in `FInvoke`'s `Result.invoke`,
      reporting an IO failure as `Error <exception message>` instead of raising. `File.ReadLines` validates
      the path and opens the file eagerly — verified for a missing file, an empty path and a directory — so
      the failure is caught at the call and never escapes later, mid-enumeration. A `null` path is rejected
      before that by `nullArgCheck`, per §2.
- [x] The file is closed on every path, including when parsing stops at a bad line, because `appendLines`
      disposes the enumerator it took. Two tests pin it by deleting the file afterwards, which fails on
      Windows while a handle is open.
- [x] `Ini.toFile : string -> Ini -> Result<unit, string>`, wrapping `File.WriteAllLines` through
      `File.writeLines` the same way `readLines` wraps `File.ReadLines`: a `null` path raises, an unwritable
      one — a directory, say — comes back as `Error`. The file ends with a line terminator and an existing
      file is overwritten, both pinned by tests, as is `toFile` + `fromFile` returning an equal `Ini`.
- [ ] `writeLines` does not guard its `lines` argument, so a `null` there would come back as
      `Error "Value cannot be null. (Parameter 'contents')"` rather than raising. Unreachable today —
      `toFile` always passes `toLines`, which is never `null` — but inconsistent with §2 if `writeLines`
      ever gains another caller.
- [ ] A `FInvoke` dependency was added for `Result.invoke` / `Result.invoke2`. It is one package for two
      call sites; keep it only if §5 and §9 use it more widely, otherwise inline the `try ... with`.

## 10. Infrastructure

- [ ] No CI exists (`.github/workflows` is absent). Add build + test + pack, and publish on tag. `dotnet test`
      runs both suites as they are — 264 tests — so the workflow needs nothing special. Note that
      xunit.v3 4.0 runs on Microsoft.Testing.Platform, which rejects VSTest-era arguments such as
      `--nologo`, so pass it nothing it does not understand.
- [ ] Generate and publish API documentation; `GenerateDocumentationFile` is already on, but the source
      carries no XML doc comments.
- [ ] Stale build artefacts from the `Fini.v3.Tests` → `Fini.Tests` rename are still in `bin` / `obj`
      (`.msCoverageSourceRootsMapping_Fini.v3.Tests`, `Fini.v3.Tests.fsproj.nuget.*`). Harmless, but a
      clean clone will not have them, so a `git clean` of the build output is worth doing before measuring
      anything.

---

## Note: members that call themselves

Twice now a member has been written as a one-liner that resolves to itself, and both compiled without a
warning:

```fsharp
member this.IsEmpty: bool = this.IsEmpty          // not this.Map.IsEmpty

interface IEnumerable<KeyValuePair<string, string>> with
    member this.GetEnumerator() =
        let e = this :> IEnumerable<KeyValuePair<string, string>>
        e.GetEnumerator()                         // dispatches back to this member
```

Neither fails loudly. `IsEmpty` is a tail call, so it becomes an infinite loop rather than a stack
overflow — the test run simply never finishes, with no failure and no output to point at. The interface
upcast looks like delegation to the underlying map but the map is never mentioned; `this` already *is* the
interface, so the cast is a no-op.

Worth remembering when forwarding a member to the wrapped `Map`: the forwarding target is
`this.Map.Something`, never `this.Something`. For an interface implementation, delegating to a *different*
interface is fine — the non-generic `IEnumerable.GetEnumerator` calls
`(this.KeyValuePairs :> IEnumerable).GetEnumerator()` and that resolves to the projection, not to itself.
Casting to the interface currently being implemented is what loops.

The reverse direction is safe: inside an `interface ... with` block, `this` is typed as `Ini`, and F#
resolves `this.Count` to the intrinsic member, because interface slots are not accessible as members of the
concrete type. So `member this.Count = this.Count` in an interface block terminates — it reads like the
`IsEmpty` bug but is not one. That only matters if the read-only dictionary interfaces come back.
