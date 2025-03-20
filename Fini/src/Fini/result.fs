module internal Result

let combine (results: Result<'T, 'U> list) : Result<'T list, 'U> =
    let rec loop (results: Result<'T, 'U> list) (acc: 'T list) : Result<'T list, 'U> =
        match results with
        | [] -> Ok acc
        | Error err :: _ -> Error err
        | Ok head :: tail -> loop tail (head :: acc)
    loop results List.Empty
