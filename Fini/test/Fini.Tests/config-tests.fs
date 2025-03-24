module Config.Tests

open Fini.Config
open Xunit

let input =
    "g-key=g-value
[foo]
key=value
f-key=f-value
[bar]
key=value"

let output =
    "g-key=g-value
[bar]
key=value
[foo]
f-key=f-value
key=value"

[<Fact>]
let ``fromString and toString`` () =
    let config = fromString input

    let str =
        match config with
        | Ok config -> toString config
        | Error error -> error

    Assert.Equal(output, str)
