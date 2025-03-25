module Parser.Tests

open Microsoft.FSharp.Core
open FsUnit
open Types
open Xunit

[<Fact>]
let ``if key missing ParseLine returns none`` () =
    let parameter =
        match "=bar" with
        | ParseLine line -> Some line
        | _ -> None
    parameter |> should equal None

[<Fact>]
let ``if key missing ParseLine returns none spaces`` () =
    let parameter =
        match " = bar " with
        | ParseLine line -> Some line
        | _ -> None
    parameter |> should equal None

[<Fact>]
let ``if key present and value missing ParseLine returns some parameter`` () =
    let parameter =
        match "foo=" with
        | ParseLine line -> Some line
        | _ -> None
    parameter |> should equal (("foo", "") |> Parameter |> Some)

[<Fact>]
let ``if key present and value missing ParseLine returns some parameter spaces`` () =
    let parameter =
        match " foo = " with
        | ParseLine line -> Some line
        | _ -> None
    parameter |> should equal (("foo", "") |> Parameter |> Some)

[<Fact>]
let ``if key and value present ParseLine returns some parameter`` () =
    let parameter =
        match "foo=bar" with
        | ParseLine line -> Some line
        | _ -> None
    parameter |> should equal (("foo", "bar") |> Parameter |> Some)

[<Fact>]
let ``if key and value present ParseLine returns some parameter spaces`` () =
    let parameter =
        match " foo = bar " with
        | ParseLine line -> Some line
        | _ -> None
    parameter |> should equal (("foo", "bar") |> Parameter |> Some)

[<Fact>]
let ``if open bracket missing ParseLine returns none`` () =
    let section =
        match "foo]" with
        | ParseLine line -> Some line
        | _ -> None
    section |> should equal None

[<Fact>]
let ``if open bracket missing ParseLine returns none spaces`` () =
    let section =
        match " foo ] " with
        | ParseLine line -> Some line
        | _ -> None
    section |> should equal None

[<Fact>]
let ``if close bracket missing ParseLine returns none`` () =
    let section =
        match "[foo" with
        | ParseLine line -> Some line
        | _ -> None
    section |> should equal None

[<Fact>]
let ``if close bracket missing ParseLine returns none spaces`` () =
    let section =
        match " [ foo " with
        | ParseLine line -> Some line
        | _ -> None
    section |> should equal None

[<Fact>]
let ``if open and close brackets present ParseLine returns some section`` () =
    let section =
        match "[foo]" with
        | ParseLine line -> Some line
        | _ -> None
    section |> should equal ("foo" |> Section |> Some)

[<Fact>]
let ``if open and close brackets present ParseLine returns some section spaces`` () =
    let section =
        match " [ foo ] " with
        | ParseLine line -> Some line
        | _ -> None
    section |> should equal ("foo" |> Section |> Some)

[<Fact>]
let ``if text is a section parseLine returns ok section`` () =
    let line = parseLine (1, "[foo]")
    line |> should equal ("foo" |> Section |> Result<Line, string>.Ok)

[<Fact>]
let ``if text is a parameter parseLine returns ok parameter`` () =
    let line = parseLine (1, "foo=bar")
    line |> should equal (("foo", "bar") |> Parameter |> Result<Line, string>.Ok)

[<Fact>]
let ``if cannot parse parseLine returns err`` () =
    let line = parseLine (1, "foo")
    line |> should equal ("Cannot parse line 2: foo." |> Result<Line, string>.Error)

[<Fact>]
let ``if cannot parse parseLine returns err spaces`` () =
    let line = parseLine (1, " foo ")
    line |> should equal ("Cannot parse line 2:  foo ." |> Result<Line, string>.Error)

[<Fact>]
let ``if cannot parse parseLine returns err variant 1`` () =
    let line = parseLine (1, "a[foo]")
    line |> should equal ("Cannot parse line 2: a[foo]." |> Result<Line, string>.Error)

[<Fact>]
let ``if cannot parse parseLine returns err variant 2`` () =
    let line = parseLine (1, "a[foo]b")
    line |> should equal ("Cannot parse line 2: a[foo]b." |> Result<Line, string>.Error)

[<Fact>]
let ``if cannot parse parseLine returns err variant 3`` () =
    let line = parseLine (1, "[foo]b")
    line |> should equal ("Cannot parse line 2: [foo]b." |> Result<Line, string>.Error)

[<Fact>]
let ``if cannot parse parseLine returns err variant 4`` () =
    let line = parseLine (1, "a foo=bar")
    line
    |> should equal ("Cannot parse line 2: a foo=bar." |> Result<Line, string>.Error)

[<Fact>]
let ``if cannot parse parseLine returns err variant 5`` () =
    let line = parseLine (1, "a foo=bar b")
    line |> should equal ("Cannot parse line 2: a foo=bar b." |> Result<Line, string>.Error)

[<Fact>]
let ``if cannot parse parseLine returns err variant 6`` () =
    let line = parseLine (1, "foo=bar b")
    line |> should equal (("foo", "bar b") |> Parameter |> Result<Line, string>.Ok)

[<Fact>]
let ``if cannot parse parseLine returns err variant 7`` () =
    let line = parseLine (1, " foo = bar b ")
    line |> should equal (("foo", "bar b") |> Parameter |> Result<Line, string>.Ok)

[<Fact>]
let ``if empty string removeComment returns empty string`` () =
    let noComment = removeComment (1, "")
    noComment |> should equal (1, "")

[<Fact>]
let ``if no comment removeComment returns original string`` () =
    let noComment = removeComment (2, "foo")
    noComment |> should equal (2, "foo")

[<Fact>]
let ``if starts with comment removeComment returns empty string`` () =
    let noComment = removeComment (3, "# foo")
    noComment |> should equal (3, "")

[<Fact>]
let ``if contains comment removeComment removes comment`` () =
    let noComment = removeComment (4, "foo # bar")
    noComment |> should equal (4, "foo")

[<Fact>]
let ``if not whitespace isNotWhiteSpace returns true`` () =
    let result = isNotWhiteSpace (1, "foo")
    result |> should be True

[<Fact>]
let ``if empty string isNotWhiteSpace returns false`` () =
    let result = isNotWhiteSpace (1, "")
    result |> should be False

[<Fact>]
let ``if whitespace isNotWhiteSpace returns false`` () =
    let result = isNotWhiteSpace (1, " ")
    result |> should be False

[<Fact>]
let ``if ini valid parse returns ok line list`` () =
    let result =
        seq {
            "[foo]"
            "# comment"
            "foo=bar"
        }
        |> parse

    let list =
        match result with
        | Ok ok -> ok
        | Error _ -> List.Empty

    result.IsOk |> should be True
    list.Length |> should equal 2

[<Fact>]
let ``if ini is invalid parse returns error`` () =
    let result =
        seq {
            "[foo]"
            "# comment"
            "foo=bar"
            "bar"
            "key=value"
        }
        |> parse

    result.IsError |> should be True
