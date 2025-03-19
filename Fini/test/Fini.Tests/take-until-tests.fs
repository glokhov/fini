module TakeUntil.Tests

open TakeUntil
open Xunit

[<Fact>]
let ``if a sequence is empty takeUntil returns an empty sequence`` () =
    let sequence = Seq.empty |> Seq.takeUntil (fun x -> x = 0)
    Assert.Empty sequence

[<Fact>]
let ``if a sequence has no matching elements takeUntil returns the full sequence`` () =
    let sequence = seq { 1..10 } |> Seq.takeUntil (fun x -> x = 0)
    Assert.Equivalent(seq { 1..10 }, sequence)

[<Fact>]
let ``if a sequence has matching element at the end takeUntil returns the full sequence`` () =
    let sequence = seq { 1..10 } |> Seq.takeUntil (fun x -> x = 10)
    Assert.Equivalent(seq { 1..10 }, sequence)

[<Fact>]
let ``if a sequence has matching element at the start takeUntil returns the fist element`` () =
    let sequence = seq { 1..10 } |> Seq.takeUntil (fun x -> x = 1)
    Assert.Equivalent(seq { 1 }, sequence)

[<Fact>]
let ``if a sequence has matching element in the middle takeUntil returns a sequence including matching element`` () =
    let sequence = seq { 1..10 } |> Seq.takeUntil (fun x -> x = 5)
    Assert.Equivalent(seq { 1..5 }, sequence)

[<Fact>]
let ``if a list is empty takeUntil returns an empty list`` () =
    let list = List.empty |> List.takeUntil (fun x -> x = 0)
    Assert.Empty list

[<Fact>]
let ``if a list has no matching elements takeUntil returns the full list`` () =
    let list = [ 1..10 ] |> List.takeUntil (fun x -> x = 0)
    Assert.Equivalent([ 1..10 ], list)

[<Fact>]
let ``if a list has matching element at the end takeUntil returns the full list`` () =
    let list = [ 1..10 ] |> List.takeUntil (fun x -> x = 10)
    Assert.Equivalent([ 1..10 ], list)

[<Fact>]
let ``if a list has matching element at the start takeUntil returns the fist element`` () =
    let list = [ 1..10 ] |> List.takeUntil (fun x -> x = 1)
    Assert.Equivalent([ 1 ], list)

[<Fact>]
let ``if a list has matching element in the middle takeUntil returns a list including matching element`` () =
    let list = [ 1..10 ] |> List.takeUntil (fun x -> x = 5)
    Assert.Equivalent([ 1..5 ], list)

[<Fact>]
let ``if an array is empty takeUntil returns an empty array`` () =
    let array = Array.empty |> Array.takeUntil (fun x -> x = 0)
    Assert.Empty array

[<Fact>]
let ``if an array has no matching elements takeUntil returns the full array`` () =
    let array = [| 1..10 |] |> Array.takeUntil (fun x -> x = 0)
    Assert.Equivalent([| 1..10 |], array)

[<Fact>]
let ``if an array has matching element at the end takeUntil returns the full array`` () =
    let array = [| 1..10 |] |> Array.takeUntil (fun x -> x = 10)
    Assert.Equivalent([| 1..10 |], array)

[<Fact>]
let ``if an array has matching element at the start takeUntil returns the fist element`` () =
    let array = [| 1..10 |] |> Array.takeUntil (fun x -> x = 1)
    Assert.Equivalent([| 1 |], array)

[<Fact>]
let ``if an array has matching element in the middle takeUntil returns an array including matching element`` () =
    let array = [| 1..10 |] |> Array.takeUntil (fun x -> x = 5)
    Assert.Equivalent([| 1..5 |], array)
