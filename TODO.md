# TODO

Current state: a document is parsed into a flattened `Map<Key, string>` keyed by `section:parameter`.
Lookup (`tryFind`, `tryFindNested`, `find`, `findNested`, `containsKey`) and single-key mutation (`tryAdd`,
`remove`) are in place. What is still missing is **emitting** — there is no `toString` / `toLines`, so an
`Ini` cannot be written back out — and pair-wise enumeration.

This file tracks what is needed to get to a complete read-write API. Items marked **blocker** should be
settled before the features that depend on them, because they change the key model or the public surface.

---

## 1. Key model is ambiguous — **done**

Resolved by option **(a)**: `:` is excluded from the name charset in both `sectionRegex` and
`parameterRegex` (`[^=:;#\ \[\]]+`). The colon is now reserved purely as the key separator.

```fsharp
Ini.tryCreate [ "[a:b]"; "x = 1" ]   // Error "Cannot parse line: [a:b]."
Ini.tryCreate [ "[a]"; "b:x = 2" ]   // Error "Cannot parse line: b:x = 2."
```

Both documents previously produced the same key, `a:b:x`. Every key now holds exactly one `:` and splits
unambiguously into a section and a parameter, which is what `toString` (§4) needs to regroup keys by
section, and it makes `tryFindNested`'s first-colon split well defined. Values are unaffected and may still
contain colons (`url = https://example.com:8080`).

- [x] Decide the fix — option (a).
- [x] Tests pinning that a section name and a parameter name cannot be confused.
- [x] README name charset updated to include `:`.

Note for §5: if escaping is later extended to names, `:` must stay excluded or escapable-only, otherwise
this ambiguity returns.

## 2. Mutation API — **partly done**

All of these return a new `Ini`; the type stays immutable. Follow the existing convention — **the `Ini` is
always the last parameter** — so they compose with `|>`.

- [x] `Ini.tryAdd : string -> string -> Ini -> Result<Ini, string>` — overwrite on existing key, matching parse
      semantics where a duplicate key wins. Returns `Result` because of the validation decision below.
- [x] `Ini.remove : string -> Ini -> Ini` — no-op when the key is absent.
- [x] **Key validation decided: validate where a key enters the map, nowhere else.**

      A key supplied in code can contain characters the parser rejects (whitespace, `=`, `:`, `;`, `#`,
      `[`, `]`), and without a check it would be possible to build an `Ini` that `toString` (§4) emits and
      `tryCreate` then refuses to read back. There are exactly two insertion points: `Map.add` in `tryAdd` and
      `Map.add` in `tryAppend`. The latter is already guarded by `parseLine`, whose section and parameter
      regexes carry the charset; `tryAdd` is now guarded by `tryParseKey`, which uses the same charset. So every
      key in the map is well formed, by construction.

      `tryAdd` therefore returns `Result<Ini, string>` with `Error "Invalid key: <key>."`, rather than
      throwing — it is an expected failure, like a parse error, and composes with `tryCreate` /
      `tryAppend` under `Result`.

      Everything else stays unvalidated. `remove` is a write but cannot break the invariant, since it only
      takes keys out; `tryFind`, `find`, `containsKey` and the nested variants only read. A key the parser
      would reject cannot be present, so the honest answer is a miss, not an error: `tryFind` gives `None`,
      `containsKey` gives `false`, `remove` is a no-op, `find` raises `KeyNotFoundException`. Validating
      there would cost a regex match per lookup and turn total functions into `Result`-returning ones for
      no gain.

      Keys are not trimmed anywhere, so `tryAdd " x "` is rejected rather than stored under a key the same
      string could not find again — `tryAdd` and `tryFind` agree on every key either of them accepts.

      Consequence for §3: `ofSeq` is an insertion point and needs the same guard as `tryAdd`, hence
      `tryOfSeq` or an `ofSeq` returning `Result`.
- [x] C# members: `TryAdd` returning `Result<Ini, string>` and `Remove` returning a new `Ini`.
- [ ] `Ini.change : string -> (string option -> string option) -> Ini -> Ini` — wraps `Map.change`, covers
      add/update/remove in one call. Note it is an insertion point, so it needs the `tryAdd` guard and would
      have to return `Result`; or take an already-validated key.
- [ ] Section-level operations, natural for the flattened layout:
      `Ini.removeSection : string -> Ini -> Ini`, and possibly `Ini.renameSection`. `removeSection` is a
      pure delete and needs no validation; `renameSection` introduces a name and does.
- [ ] C# `Change` member, mirroring `ImmutableDictionary` style, once `change` lands.

## 3. Enumeration of key-value pairs — **partly done**

`keys` and `values` are aligned but separate, so callers wanting pairs from F# must `Seq.zip` and walk the
map twice. C# can now `foreach` an `Ini` and use LINQ over it.

- [x] `IEnumerable<KeyValuePair<string, string>>` on `Ini`, plus the non-generic `IEnumerable`, mapping each
      `Key` to its string `Path` so the private key type stays private. Pairs come out in key order, the same
      order as `Keys` and `Values`. Documented in the README under "C#".

      The map cannot simply be delegated to, even though `Map<_,_>` implements
      `IEnumerable<KeyValuePair<_,_>>`: it enumerates `KeyValuePair<Key, string>`, not
      `KeyValuePair<string, string>`, and `Key` is private so it cannot appear in a public interface. The
      `Key -> Path` projection is therefore unavoidable. It lives in the generic implementation, and the
      non-generic one delegates to the generic interface.

      Both implementations were briefly self-recursive — each upcast `this` to the interface it was
      implementing and called `GetEnumerator` on it, which dispatches straight back to the same member. See
      the note at the end of this file. Delegating from the non-generic to the *generic* interface is fine,
      because that is a different interface and so a different member.
- [x] **`IReadOnlyDictionary` / `IReadOnlyCollection` were tried and removed.** They add surface without
      adding capability: `Count`, `ContainsKey`, `Keys` and `Values` already exist as members, and the
      indexer and `TryGetValue` that the interface requires duplicate `Find` and `TryFind` exactly. What
      they cost is real — two more interfaces to keep consistent, two members that exist only to satisfy
      them, and the explicit-implementation trap below. `IEnumerable<KeyValuePair<string, string>>` is kept,
      because `foreach` and LINQ are not otherwise expressible.

      Worth knowing if this is ever revisited: **F# interface implementations are always explicit**, so
      putting `Count`, `ContainsKey`, `Keys` and `Values` *only* in an `interface ... with` block removes
      them from `Ini` itself — `error FS0039: The type 'Ini' does not define a field, constructor, or member
      named 'Count'` from the `Ini` module, and the same loss for C# callers, where an explicit
      implementation also needs a cast. Those members have to stay intrinsic, with the interface slots
      forwarding to them.
- [x] Tests for enumeration: F# and C# both, covering key order, agreement with `keys` / `values`, the empty
      `Ini`, LINQ, and the non-generic `IEnumerable` path. Ten tests across the two suites. None of this was
      covered before, which is why the `GetEnumerator` recursion shipped undetected: nothing enumerated an
      `Ini`.
- [ ] `Ini.toSeq : Ini -> (string * string) seq`, plus `toList` / `toArray`. Note these yield F# tuples,
      not `KeyValuePair`, so they are not just an alias for the interface above.
- [ ] `Ini.tryOfSeq : (string * string) seq -> Result<Ini, string>` to close the round-trip with `toSeq`.
      It is an insertion point, so by the §2 decision it validates each key through `tryParseKey` and returns
      `Result`, reporting the first key it rejects exactly as `tryAdd` does.
- [ ] Consider `Ini.sections : Ini -> string seq` (distinct section names, in order) and
      `Ini.section : string -> Ini -> (string * string) seq` (the parameters of one section). Both are
      awkward to express from the outside once keys are flattened.
- [ ] Keep `keys` / `values` for compatibility.

## 4. Rendering (`toString`)

`Ini.ToString()` currently leaks the internal representation:

```
{ Map = map [({ Path = "a:x" }, "1")] }
```

- [ ] Prefer `Ini.toLines : Ini -> string seq` as the primitive — it is the exact inverse of `tryCreate`,
      which consumes lines, and feeds `File.WriteAllLines` directly. Define
      `toString = toLines >> String.concat Environment.NewLine`.
- [ ] Override `Ini.ToString()` and add a `ToString()` / `ToLines()` C# member.
- [ ] Canonical layout: root-section parameters first with no header, then each section as `[name]` followed
      by its parameters, all in key order. Decide whether to separate sections with a blank line and whether
      to emit a trailing newline.
- [ ] Document that rendering is **canonical, not faithful**. The map retains neither comments, blank lines,
      original ordering, repeated-section layout, nor value padding — `x =   padded   ` parses to `"padded"`,
      so the original spacing cannot be restored.
- [ ] Round-trip test: `tryCreate lines |> toLines |> tryCreate` should be idempotent, i.e. rendering an
      `Ini` and re-parsing it yields an equal `Ini`. This is the real test of §1 and §5.

## 5. Escaping (prerequisite for inline comments)

Documented in the README as unsupported and planned. Inline comments cannot be added without it: stripping
`;` and `#` from values would make them impossible to write, and they appear in real values
(`separators = ;#`, `fragment = https://example.com/doc#section`).

- [ ] Choose an escape character (`\` is conventional) and the set of escapes:
      `;` `#` for inline comments, leading/trailing whitespace, newline for multi-line values, `=`, `:` if
      §1(a) is not taken, and the escape character itself.
- [ ] Unescape on parse, escape on render — §4 and §5 must be exact inverses or the round-trip test fails.
- [ ] Only then implement inline comment stripping, and update the README's "Inline comments" section.
- [ ] Decide whether escaping also applies to section and parameter names, which would let names contain
      characters currently rejected outright.

## 6. API surface and correctness fixes — **done**

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

## 7. Parsing

- [ ] Include the 1-based line number in parse errors. `Cannot parse line: oops.` does not locate the
      problem in a large file.
- [ ] Consider reporting all parse errors rather than only the first. Note this now conflicts with the
      short-circuiting below — collecting every error means reading the whole document again, so it would
      have to be an opt-in variant rather than the default.
- [x] **Done.** `Result.traverse` folded the entire sequence even after an error. It has been removed, and
      `tryAppend` now parses straight into the map in a single short-circuiting pass over the enumerator:

      ```fsharp
      use enumerator = (nullArgCheck "lines" lines).GetEnumerator()

      let rec loop map section =
          if not (enumerator.MoveNext()) then Ok map
          else
              match parseLine enumerator.Current with
              | Error err -> Error err
              | Ok line -> ...
      ```

      This also drops the intermediate whole-document `Line list`, so parsing is one pass instead of two.
      The explicit `use` is required: abandoning the enumerator early means nothing else will dispose it,
      which matters for a source like `File.ReadLines` that holds a file handle.

      Measured with an unparsable line at position 3 of 10,000: 3 lines consumed, previously all 10,000.
      A source with no errors is still read in full. Four regression tests cover it, and all four were
      confirmed to fail against the previous implementation — the infinite-sequence case hung until its
      10s timeout.
- [ ] `tryAppend` restarts at the root section on every call, so appending `port = 9090` lands at `:port`
      regardless of the sections already present. Documented, but consider a variant that continues from the
      last section of the existing document.

## 8. Convenience and infrastructure

- [ ] File helpers: `Ini.tryLoad : string -> Result<Ini, string>` and
      `Ini.trySave : string -> Ini -> Result<unit, string>`, once §4 lands.
- [ ] No CI exists (`.github/workflows` is absent). Add build + test + pack, and publish on tag.
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
`(this :> IEnumerable<KeyValuePair<string, string>>).GetEnumerator()` and that resolves to the generic
member, not to itself. Casting to the interface currently being implemented is what loops.

The reverse direction is safe: inside an `interface ... with` block, `this` is typed as `Ini`, and F#
resolves `this.Count` to the intrinsic member, because interface slots are not accessible as members of the
concrete type. So `member this.Count = this.Count` in an interface block terminates — it reads like the
`IsEmpty` bug but is not one. That only matters if the read-only dictionary interfaces come back.
