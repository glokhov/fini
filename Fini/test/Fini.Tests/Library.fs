namespace Fini

open Xunit

module IniTests =

    // Sample content covering global, single and nested sections.
    let private sampleLines =
        [ "global_key=global_value"; "[one]"; "one_key=one_value"; "[one.two]"; "two_key=two_value" ]

    // tryCreate/tryAppend return Result<Ini, string>; tests that expect success unwrap it here.
    let private unwrap result =
        match result with
        | Ok value -> value
        | Error err -> failwith err

    let private sample () = Ini.tryCreate sampleLines |> unwrap

    // ---- create / tryFind ----

    [<Fact>]
    let ``create parses a value from a single section`` () =
        let ini = sample ()

        match Ini.tryFind "one:one_key" ini with
        | Some value -> Assert.Equal("one_value", value)
        | None -> Assert.Fail("expected to find one:one_key")

    [<Fact>]
    let ``create parses a value from a nested section`` () =
        let ini = sample ()

        match Ini.tryFind "one.two:two_key" ini with
        | Some value -> Assert.Equal("two_value", value)
        | None -> Assert.Fail("expected to find one.two:two_key")

    [<Fact>]
    let ``create stores global parameters under a leading separator key`` () =
        let ini = sample ()

        match Ini.tryFind ":global_key" ini with
        | Some value -> Assert.Equal("global_value", value)
        | None -> Assert.Fail("expected to find :global_key")

    [<Fact>]
    let ``tryFind returns None for a missing key`` () =
        let ini = sample ()

        Assert.Equal<string option>(None, Ini.tryFind "one:missing" ini)

    [<Fact>]
    let ``tryFind is exact and does not match a bare parameter key`` () =
        let ini = sample ()

        // Stored as "one:one_key", so the bare "one_key" must not match.
        Assert.Equal<string option>(None, Ini.tryFind "one_key" ini)

    // ---- parsing rules: comments, whitespace, blank lines ----

    [<Fact>]
    let ``create trims inline comments starting with hash`` () =
        let ini = Ini.tryCreate [ "[s]"; "key=value # trailing comment" ] |> unwrap

        match Ini.tryFind "s:key" ini with
        | Some value -> Assert.Equal("value", value)
        | None -> Assert.Fail("expected to find s:key")

    [<Fact>]
    let ``create trims surrounding whitespace around keys and values`` () =
        let ini = Ini.tryCreate [ "[s]"; "   key   =   value   " ] |> unwrap

        match Ini.tryFind "s:key" ini with
        | Some value -> Assert.Equal("value", value)
        | None -> Assert.Fail("expected to find s:key")

    [<Fact>]
    let ``create ignores blank and whitespace-only lines`` () =
        let ini = Ini.tryCreate [ ""; "   "; "[s]"; ""; "key=value"; "   " ] |> unwrap

        match Ini.tryFind "s:key" ini with
        | Some value -> Assert.Equal("value", value)
        | None -> Assert.Fail("expected to find s:key")

    [<Fact>]
    let ``create keeps whitespace inside a value`` () =
        let ini = Ini.tryCreate [ "[s]"; "key = hello world" ] |> unwrap

        match Ini.tryFind "s:key" ini with
        | Some value -> Assert.Equal("hello world", value)
        | None -> Assert.Fail("expected to find s:key")

    [<Fact>]
    let ``create with a fully commented line drops the parameter`` () =
        let ini = Ini.tryCreate [ "[s]"; "# key=value" ] |> unwrap

        Assert.Equal<string option>(None, Ini.tryFind "s:key" ini)

    // ---- tryFindNested ----

    [<Fact>]
    let ``tryFindNested finds the parameter in the nearest parent section`` () =
        // "c" is not in section [a.b], but it is in the parent section [a].
        let ini = Ini.tryCreate [ "[a]"; "c=parent"; "[a.b]"; "other=x" ] |> unwrap

        match Ini.tryFindNested "a.b:c" ini with
        | Some value -> Assert.Equal("parent", value)
        | None -> Assert.Fail("expected to find c in parent section a")

    [<Fact>]
    let ``tryFindNested checks the given section first`` () =
        // "c" exists in the given section [a.b], so that value wins over any parent.
        let ini = Ini.tryCreate [ "[a]"; "c=parent"; "[a.b]"; "c=child" ] |> unwrap

        match Ini.tryFindNested "a.b:c" ini with
        | Some value -> Assert.Equal("child", value)
        | None -> Assert.Fail("expected to find c in section a.b")

    [<Fact>]
    let ``tryFindNested falls back to the global section`` () =
        // "c" only exists globally, stored under ".c".
        let ini = Ini.tryCreate [ "c=global"; "[a]"; "x=1"; "[a.b]"; "y=2" ] |> unwrap

        match Ini.tryFindNested "a.b:c" ini with
        | Some value -> Assert.Equal("global", value)
        | None -> Assert.Fail("expected to fall back to the global .c")

    [<Fact>]
    let ``tryFindNested returns None when the parameter exists in no section`` () =
        let ini = sample ()

        // "missing" is not in a.b, a, or the global section.
        Assert.Equal<string option>(None, Ini.tryFindNested "one.two:missing" ini)

    [<Fact>]
    let ``tryFind treats a key without a separator as a global key`` () =
        let ini = sample ()

        // "global_key" is normalised to ":global_key" before lookup.
        match Ini.tryFind "global_key" ini with
        | Some value -> Assert.Equal("global_value", value)
        | None -> Assert.Fail("expected 'global_key' to resolve to the global value")

    [<Fact>]
    let ``tryFind resolves global keys with or without a leading separator`` () =
        let ini = sample ()

        Assert.Equal<string option>(Ini.tryFind "global_key" ini, Ini.tryFind ":global_key" ini)

    [<Fact>]
    let ``tryFindNested treats a key without a separator as a global key`` () =
        let ini = sample ()

        match Ini.tryFindNested "global_key" ini with
        | Some value -> Assert.Equal("global_value", value)
        | None -> Assert.Fail("expected 'global_key' to resolve to the global value")

    // ---- case sensitivity ----

    [<Fact>]
    let ``create uses a case-insensitive (OrdinalIgnoreCase) comparer by default`` () =
        let ini = sample ()

        match Ini.tryFind "ONE:ONE_KEY" ini with
        | Some value -> Assert.Equal("one_value", value)
        | None -> Assert.Fail("expected case-insensitive match for ONE:ONE_KEY by default")

    // ---- empty input ----

    [<Fact>]
    let ``create with no lines yields an empty table`` () =
        let ini = Ini.tryCreate [] |> unwrap

        Assert.True(ini.IsEmpty)
        Assert.Equal<string option>(None, Ini.tryFind ":anything" ini)

    // ---- empty / append ----

    [<Fact>]
    let ``empty yields an empty table`` () =
        let ini = Ini.empty

        Assert.True(ini.IsEmpty)

    [<Fact>]
    let ``append adds parsed lines to an existing ini`` () =
        let ini = Ini.empty |> Ini.tryAppend sampleLines |> unwrap

        match Ini.tryFind "one:one_key" ini with
        | Some value -> Assert.Equal("one_value", value)
        | None -> Assert.Fail("expected append to add one:one_key")

    [<Fact>]
    let ``append merges into and overrides an existing ini`` () =
        let ini =
            Ini.tryCreate [ "[s]"; "key=first" ]
            |> Result.bind (Ini.tryAppend [ "[s]"; "key=second"; "other=new" ])
            |> unwrap

        // The later value wins.
        match Ini.tryFind "s:key" ini with
        | Some value -> Assert.Equal("second", value)
        | None -> Assert.Fail("expected append to override s:key")

        // Pre-existing entries not touched by the append are preserved alongside new ones.
        match Ini.tryFind "s:other" ini with
        | Some value -> Assert.Equal("new", value)
        | None -> Assert.Fail("expected append to add s:other")

    // ---- dots inside parameter names ----

    [<Fact>]
    let ``tryFind resolves a parameter name containing dots`` () =
        let ini = Ini.tryCreate [ "[one]"; "a.b.c=from-one" ] |> unwrap

        match Ini.tryFind "one:a.b.c" ini with
        | Some value -> Assert.Equal("from-one", value)
        | None -> Assert.Fail("expected to find the dotted parameter a.b.c in section one")

    [<Fact>]
    let ``tryFindNested walks sections when the parameter name contains dots`` () =
        // The parameter name "a.b.c" must stay intact while the section walk goes
        // [one.two] -> [one], rather than being split on its own dots.
        let ini = Ini.tryCreate [ "[one]"; "a.b.c=from-one"; "[one.two]"; "x=1" ] |> unwrap

        match Ini.tryFindNested "one.two:a.b.c" ini with
        | Some value -> Assert.Equal("from-one", value)
        | None -> Assert.Fail("expected the dotted parameter to be inherited from section one")

    [<Fact>]
    let ``tryFindNested falls back to a global parameter whose name contains dots`` () =
        let ini = Ini.tryCreate [ "a.b.c=global"; "[x.y]"; "k=1" ] |> unwrap

        match Ini.tryFindNested "x.y:a.b.c" ini with
        | Some value -> Assert.Equal("global", value)
        | None -> Assert.Fail("expected to fall back to the global dotted parameter")

    [<Fact>]
    let ``a dotted parameter does not collide with a nested section`` () =
        // "two.k" in [one] and "k" in [one.two] are distinct keys.
        let ini = Ini.tryCreate [ "[one]"; "two.k=A"; "[one.two]"; "k=B" ] |> unwrap

        Assert.Equal<string option>(Some "A", Ini.tryFind "one:two.k" ini)
        Assert.Equal<string option>(Some "B", Ini.tryFind "one.two:k" ini)

    // ---- the separator is reserved ----

    [<Fact>]
    let ``a colon in a section name is rejected`` () =
        match Ini.tryCreate [ "[a:b]" ] with
        | Error _ -> ()
        | Ok _ -> Assert.Fail("expected tryCreate to reject a colon in a section name")

    [<Fact>]
    let ``a colon in a parameter name is rejected`` () =
        match Ini.tryCreate [ "a:b=v" ] with
        | Error _ -> ()
        | Ok _ -> Assert.Fail("expected tryCreate to reject a colon in a parameter name")

    [<Fact>]
    let ``a value may contain a colon`` () =
        let ini = Ini.tryCreate [ "u=http://example.com" ] |> unwrap

        match Ini.tryFind "u" ini with
        | Some value -> Assert.Equal("http://example.com", value)
        | None -> Assert.Fail("expected a colon to be allowed inside a value")
