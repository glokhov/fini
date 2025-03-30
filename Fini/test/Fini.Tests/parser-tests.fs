module Parser.Tests

open Fini
open Microsoft.FSharp.Core
open FsUnit
open Xunit
open Fini.Line

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
    section |> should equal ("foo" |> Line.Section |> Some)

[<Fact>]
let ``if open and close brackets present ParseLine returns some section spaces`` () =
    let section =
        match " [ foo ] " with
        | ParseLine line -> Some line
        | _ -> None
    section |> should equal ("foo" |> Line.Section |> Some)

[<Fact>]
let ``if text is a section parseLine returns ok section`` () =
    let line = parseLine (1, "[foo]")
    line |> should equal ("foo" |> Line.Section |> Result<Line, string>.Ok)

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
