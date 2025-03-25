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

let section (p: MParameter) : Section = p |> fst |> fst

let key (p: MParameter) : PKey = p |> fst |> snd

let parameter (p: MParameter) : Parameter = key p, snd p
