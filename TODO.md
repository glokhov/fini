# TODO

Current state: `0.3.0-preview*` is **read-only**. A document is parsed into a flattened
`Map<Key, string>` keyed by `section:parameter`; there is no way to add, remove or emit values.

This file tracks what is needed to get to a read-write API. Items marked **blocker** should be settled
before the features that depend on them, because they change the key model or the public surface.

---

## 1. Key model is ambiguous (blocker)

`:` is currently a legal character in both section and parameter names — neither `sectionRegex` nor
`parameterRegex` excludes it. Two different documents therefore collapse onto the same key:

```fsharp
Ini.tryCreate [ "[a:b]"; "x = 1" ] |> Result.map Ini.keys   // Ok [ "a:b:x" ]
Ini.tryCreate [ "[a]"; "b:x = 2" ] |> Result.map Ini.keys   // Ok [ "a:b:x" ]
```

Nothing can split `a:b:x` back into a section and a parameter, so this blocks `toString` (which must
regroup keys by section) and makes `tryFindNested`'s first-colon split arbitrary.

- [ ] Decide the fix:
  - **(a)** exclude `:` from the name charset in both regexes — smallest change, makes the flattened key a
    bijection, breaking only for documents that use `:` in a name;
  - **(b)** split on the last `:` rather than the first — keeps `:` legal in section names only;
  - **(c)** stop flattening: store `Map<string, Map<string, string>>` or `Map<string * string, string>`.
- [ ] Whichever is chosen, add tests pinning that a section name and a parameter name cannot be confused.

## 2. Mutation API

All of these return a new `Ini`; the type stays immutable. Follow the existing convention — **the `Ini` is
always the last parameter** — so they compose with `|>`.

- [ ] `Ini.add : string -> string -> Ini -> Ini` — overwrite on existing key, matching parse semantics
      where a duplicate key wins.
- [ ] `Ini.remove : string -> Ini -> Ini` — no-op when the key is absent.
- [ ] `Ini.change : string -> (string option -> string option) -> Ini -> Ini` — wraps `Map.change`, covers
      add/update/remove in one call.
- [ ] Decide **key validation**. A key supplied in code can contain characters the parser rejects
      (whitespace, `=`, `;`, `#`, `[`, `]`, newline). Without a check it is possible to build an `Ini` that
      `toString` emits and `tryCreate` then refuses to read back. Options: `tryAdd` returning
      `Result<Ini, string>`, a validating `add` that throws, or silent rejection. This should match whatever
      escaping (§5) ends up allowing.
- [ ] Section-level operations, natural for the flattened layout:
      `Ini.removeSection : string -> Ini -> Ini`, and possibly `Ini.renameSection`.
- [ ] C# members: `Add`, `Remove`, `Change` returning a new `Ini`, mirroring `ImmutableDictionary` style.

## 3. Enumeration of key-value pairs

`keys` and `values` are aligned but separate, so callers wanting pairs must `Seq.zip` and walk the map twice.
`Ini` does not implement `IEnumerable` today, so C# cannot `foreach` it or use LINQ.

- [ ] `Ini.toSeq : Ini -> (string * string) seq`, plus `toList` / `toArray`.
- [ ] `Ini.ofSeq : (string * string) seq -> Ini` (or `tryOfSeq`, pending §2 key validation) to close the
      round-trip with `toSeq`.
- [ ] Implement `IEnumerable<KeyValuePair<string, string>>` on `Ini` for `foreach`, LINQ and collection
      expressions in C#.
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

## 6. API surface and correctness fixes

- [ ] `Fini.Key` is a **publicly exported type with private fields**, so callers can see it but cannot
      construct one. Make it `internal`.
- [ ] `Key.Equals` and `Key.CompareTo` raise `invalidArg` when given a non-`Key` argument. `Object.Equals`
      is contractually required to return `false` instead. Currently unreachable from outside because no
      `Key` can be obtained; fix alongside the visibility change.
- [ ] Annotate the `out` parameters of `TryFind` / `TryFindNested` with `[<MaybeNull>]` /
      `NotNullWhen(true)`. They are set to `null` on a miss, but the project enables `<Nullable>enable</Nullable>`
      and the signature claims non-null, so C# flow analysis is wrong today.
- [ ] `Ini` structural equality already works and is case-insensitive
      (`[a] x = 1` equals `[A] X = 1`, with matching hash codes). Add tests and mention it in the README,
      or suppress it deliberately.

## 7. Parsing

- [ ] Include the 1-based line number in parse errors. `Cannot parse line: oops.` does not locate the
      problem in a large file.
- [ ] Consider reporting all parse errors rather than only the first.
- [ ] `Result.traverse` folds the entire sequence even after an error, so a malformed first line still reads
      the whole document, and an infinite sequence never terminates. Short-circuit instead.
- [ ] `tryAppend` restarts at the root section on every call, so appending `port = 9090` lands at `:port`
      regardless of the sections already present. Documented, but consider a variant that continues from the
      last section of the existing document.

## 8. Convenience and infrastructure

- [ ] File helpers: `Ini.tryLoad : string -> Result<Ini, string>` and
      `Ini.trySave : string -> Ini -> Result<unit, string>`, once §4 lands.
- [ ] No CI exists (`.github/workflows` is absent). Add build + test + pack, and publish on tag.
- [ ] Generate and publish API documentation; `GenerateDocumentationFile` is already on, but the source
      carries no XML doc comments.
