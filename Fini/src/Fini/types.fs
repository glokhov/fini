module internal Types

type Section = string

type Key = string

type Value = string

type Parameter = Key * Value

type MapKey = Section * Key

let section (x: MapKey * Value) : Section = x |> fst |> fst

let key (x: MapKey * Value) : Key = x |> fst |> snd

let parameter (x: MapKey * Value) : Parameter = key x, snd x

let formatSection (scn: Section) : string = $"[{scn}]"

let formatParameter (par: Parameter) : string = $"{fst par}={snd par}"
