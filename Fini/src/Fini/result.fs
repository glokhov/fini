module internal Result

let combine (results: Result<'T, 'U> list) : Result<'T list, 'U> =
    let rec loop results acc =
        match results with
        | [] -> Ok acc
        | Error error :: _ -> Error error
        | Ok head :: tail -> loop tail (head :: acc)
    loop results List.Empty
