namespace TakeUntil

module Seq =

    let takeUntil (predicate: 'T -> bool) (sequence: 'T seq) : 'T seq =
        seq {
            use e = sequence.GetEnumerator()

            let mutable current = Unchecked.defaultof<_>
            let mutable ok = true

            while ok && e.MoveNext() do
                current <- e.Current
                ok <- not <| predicate current
                yield current
        }

module List =

    let takeUntil (predicate: 'T -> bool) (list: 'T list) : 'T list =
        match list |> List.tryFindIndex predicate with
        | Some ix -> list |> List.take (ix + 1)
        | None -> list

module Array =

    let takeUntil (predicate: 'T -> bool) (array: 'T array) : 'T array =
        match array |> Array.tryFindIndex predicate with
        | Some ix -> array |> Array.take (ix + 1)
        | None -> array
