module Config.Tests

open Fini.Config
open Xunit

[<Fact>]
let ``fromString`` () =
    let config = fromString "key=value\n[foo]\nkey=value\n[bar]\nkey=value\n[xyz]\nkey=value\n[abc]\nkey=value\n[bob]\nkey=value\n[red]\nkey=value\n[pop]\nkey=value\n[not]\nkey=value\n[net]\nkey=value\n[ups]\nkey=value"
    Assert.True config.IsOk
