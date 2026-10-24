namespace Fini

open System
open System.Collections.Generic

type ParserError =
    | InvalidName of Name: string
    | InvalidLine of Line: string
    | InvalidValue of Value: string

[<Sealed>]
type Name =
    static member Empty: Name

    static member TryParse: name: string -> Result<Name, ParserError>

    static member Parse: name: string -> Name

    static member op_Implicit: name: string -> Name

    static member op_Implicit: section: Name -> string

    override ToString: unit -> string

    interface IComparable

[<RequireQualifiedAccess>]
module Name =
    val empty: Name

    val tryParse: name: string -> Result<Name, ParserError>

    val parse: name: string -> Name

[<Sealed>]
type Key =
    static member TryParse: name: string -> Result<Key, ParserError>

    static member Parse: name: string -> Key

    static member op_Implicit: name: string -> Key

    static member op_Implicit: key: Key -> string

    override ToString: unit -> string

    interface IComparable

[<RequireQualifiedAccess>]
module Key =
    val tryParse: key: string -> Result<Key, ParserError>

    val parse: key: string -> Key

[<Sealed>]
type Ini =
    static member Empty: Ini

    static member Create: pairs: KeyValuePair<Key, string> seq -> Ini

    static member FromLines: lines: string seq -> Result<Ini, ParserError>

    static member FromFile: path: string -> Result<Ini, ParserError>

    member Append: pairs: KeyValuePair<Key, string> seq -> Ini

    member AppendLines: lines: string seq -> Result<Ini, ParserError>

    member AppendFile: path: string -> Result<Ini, ParserError>

    member IsEmpty: bool

    member Add: key: Key * value: string -> Ini

    member TryFind: key: Key -> string option

#if NET
    member TryFindAs<'T when 'T :> IParsable<'T>> : key: Key -> Result<'T, ParserError> option
#endif

    member TryFindWith: parser: Func<string, 'T> * key: Key -> Result<'T, ParserError> option

    member TryFindNested: key: Key -> string option

#if NET
    member TryFindNestedAs<'T when 'T :> IParsable<'T>> : key: Key -> Result<'T, ParserError> option
#endif

    member TryFindNestedWith: parser: Func<string, 'T> * key: Key -> Result<'T, ParserError> option

    member Find: key: Key -> string

#if NET
    member FindAs<'T when 'T :> IParsable<'T>> : key: Key -> 'T
#endif

    member FindWith: parser: Func<string, 'T> * key: Key -> 'T

    member FindNested: key: Key -> string

#if NET
    member FindNestedAs<'T when 'T :> IParsable<'T>> : key: Key -> 'T
#endif

    member FindNestedWith: parser: Func<string, 'T> * key: Key -> 'T

    member Remove: key: Key -> Ini

    member Count: int

    member ContainsKey: key: Key -> bool

    member Keys: Key seq

    member Values: string seq

    member KeyValuePairs: KeyValuePair<Key, string> seq

    member Sections: Name seq

    member Section: name: Name -> Ini

    member RemoveSection: name: Name -> Ini

    member ToLines: unit -> string seq

    member ToFile: path: string -> unit

    override ToString: unit -> string

    interface IEnumerable<KeyValuePair<Key, string>>

[<RequireQualifiedAccess>]
module Ini =
    val empty: Ini

    val ofSeq: pairs: (Key * string) seq -> Ini

    val fromLines: lines: string seq -> Result<Ini, ParserError>

    val fromFile: path: string -> Result<Ini, ParserError>

    val append: pairs: (Key * string) seq -> ini: Ini -> Ini

    val appendLines: lines: string seq -> ini: Ini -> Result<Ini, ParserError>

    val appendFile: path: string -> ini: Ini -> Result<Ini, ParserError>

    val isEmpty: ini: Ini -> bool

    val add: key: Key -> value: string -> ini: Ini -> Ini

    val tryFind: key: Key -> ini: Ini -> string option

#if NET
    val tryFindAs<'T when 'T :> IParsable<'T>> : key: Key -> ini: Ini -> Result<'T, ParserError> option
#endif

    val tryFindWith: parser: (string -> 'T) -> key: Key -> ini: Ini -> Result<'T, ParserError> option

    val tryFindNested: key: Key -> ini: Ini -> string option

#if NET
    val tryFindNestedAs<'T when 'T :> IParsable<'T>> : key: Key -> ini: Ini -> Result<'T, ParserError> option
#endif

    val tryFindNestedWith: parser: (string -> 'T) -> key: Key -> ini: Ini -> Result<'T, ParserError> option

    val find: key: Key -> ini: Ini -> string

#if NET
    val findAs<'T when 'T :> IParsable<'T>> : key: Key -> ini: Ini -> 'T
#endif

    val findWith: parser: (string -> 'T) -> key: Key -> ini: Ini -> 'T

    val findNested: key: Key -> ini: Ini -> string

#if NET
    val findNestedAs<'T when 'T :> IParsable<'T>> : key: Key -> ini: Ini -> 'T
#endif

    val findNestedWith: parser: (string -> 'T) -> key: Key -> ini: Ini -> 'T

    val remove: key: Key -> ini: Ini -> Ini

    val count: ini: Ini -> int

    val containsKey: key: Key -> ini: Ini -> bool

    val keys: ini: Ini -> Key seq

    val values: ini: Ini -> string seq

    val sections: ini: Ini -> Name seq

    val section: name: Name -> ini: Ini -> Ini

    val removeSection: name: Name -> ini: Ini -> Ini

    val toSeq: ini: Ini -> (Key * string) seq

    val toLines: ini: Ini -> string seq

    val toFile: path: string -> ini: Ini -> unit

    val toString: ini: Ini -> string

[<AutoOpen>]
module Casting =
    val inline name: name: string -> Name

    val inline key: key: string -> Key
