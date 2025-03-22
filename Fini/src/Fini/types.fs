module Types

type Section = string

type Key = string

type Value = string

type Parameter = Key * Value

type MapKey = Section * Key

let sectionName (x: MapKey * Value) : Section = x |> fst |> fst

let parameterName (x: MapKey * Value) : Key = x |> fst |> snd

let parameter (x: MapKey * Value) : Parameter = parameterName x, snd x

let formatSection (scn: Section) : string = "[" + scn + "]"

let formatParameter (par: Parameter) : string = ((fst par) + "=" + (snd par))
