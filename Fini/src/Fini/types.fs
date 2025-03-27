module internal Types

type Section = string

type PKey = string

type Value = string

type Parameter = PKey * Value

type MKey = Section * PKey

type MParameter = MKey * Value

type Line =
    | Section of Section
    | Parameter of Parameter
    | Comment of Value
    | Empty

let private section (p: MParameter) : Section = p |> fst |> fst

let private key (p: MParameter) : PKey = p |> fst |> snd

let private parameter (p: MParameter) : Parameter = key p, snd p

let formatSection (p: MParameter) : string = p |> section |> (fun section -> $"[{section}]")

let formatParameter (p: MParameter) : string = p |> parameter |> (fun parameter -> $"{fst parameter}={snd parameter}")
