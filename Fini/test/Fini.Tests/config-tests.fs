module Config.Tests

open Fini.Config
open Xunit

let input =
    "g-key=g-value
[foo.bar]
f-key=f-b-value
a-key=a-value
g-key=g-b-value
[foo]
o-key=o-value
f-key=f-value
[bar]
b-key=b-value
g-key=g-b-value"

let output =
    "g-key=g-value
[bar]
b-key=b-value
g-key=g-b-value
[foo]
f-key=f-value
o-key=o-value
[foo.bar]
a-key=a-value
f-key=f-b-value
g-key=g-b-value"

[<Fact>]
let ``fromString and toString`` () =
    let config = fromString input

    let str =
        match config with
        | Ok config -> toString config
        | Error error -> error

    Assert.Equal(output, str)

[<Fact>]
let ``if a value is present value returns some value`` () =
    let config = fromString input

    let result =
        match config with
        | Ok config ->
            match find "foo" "f-key" config with
            | Some value -> value
            | None -> "none"
        | Error _ -> "error"

    Assert.Equal("f-value", result)

[<Fact>]
let ``if no section is present value returns none`` () =
    let config = fromString input

    let result =
        match config with
        | Ok config ->
            match find "boo" "key" config with
            | Some value -> value
            | None -> "none"
        | Error _ -> "error"

    Assert.Equal("none", result)

[<Fact>]
let ``if no value is present value returns no`` () =
    let config = fromString input

    let result =
        match config with
        | Ok config ->
            match find "foo" "x-key" config with
            | Some value -> value
            | None -> "none"
        | Error _ -> "error"

    Assert.Equal("none", result)
