module IO.Tests

open System.IO
open Xunit

[<Fact>]
let ``if exception is thrown readLines returns a sequence with an error`` () =
    use reader = new StringReader("")
    reader.Close()

    match IO.TextReader.readLines reader |> Seq.toList with
    | [] -> Assert.Fail()
    | head :: _ -> Assert.True head.IsError

[<Fact>]
let ``if string is empty readLines returns an empty sequence`` () =
    use reader = new StringReader("")

    Assert.Equal(0, IO.TextReader.readLines reader |> Seq.length)

[<Fact>]
let ``if string has one line readLines returns a singleton sequence`` () =
    use reader = new StringReader("foo\n")

    Assert.Equal(1, IO.TextReader.readLines reader |> Seq.length)

[<Fact>]
let ``if string has multiple lines readLines returns equivalent sequence`` () =
    use reader = new StringReader("foo\nbar\n")

    Assert.Equal(2, IO.TextReader.readLines reader |> Seq.length)
