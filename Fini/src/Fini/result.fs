namespace Fini

open TakeUntil

module internal Result =
    let private combine (results: Result<'T, 'E> list) : Result<'T list, 'E> =
        let rec loop results acc =
            match results with
            | [] -> acc |> List.rev |> Ok
            | Error error :: _ -> Error error
            | Ok head :: tail -> loop tail (head :: acc)
        loop results List.Empty

    let collect (results: Result<'T, 'E> seq) : Result<'T list, 'E> =
        results |> Seq.takeUntil _.IsError |> Seq.toList |> combine

    let private combineUnit (results: Result<unit, 'E> list) : Result<unit, 'E> =
        let rec loop results =
            match results with
            | [] -> Ok()
            | Error error :: _ -> Error error
            | Ok _ :: tail -> loop tail
        loop results

    let collectUnit (results: Result<unit, 'E> seq) : Result<unit, 'E> =
        results |> Seq.takeUntil _.IsError |> Seq.toList |> combineUnit
