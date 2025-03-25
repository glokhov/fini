namespace Fini

type Config =
    new: unit -> Config

    static member Empty: Config
    static member ReadLines: string seq -> Result<Config, string>
    static member ReadString: string -> Result<Config, string>

    member IsEmpty: bool
    member ContainsKey: section: string * key: string -> bool

    member Find: section: string * key: string -> string option
    member Add: section: string * key: string * value: string -> Config
    member Change: section: string * key: string * value: string -> Config
    member Remove: section: string * key: string -> Config
    
    member WriteLines: unit -> string seq
    member WriteString: unit -> string

module Config =

    [<CompiledName("Empty")>]
    val empty: Config

    [<CompiledName("ReadLines")>]
    val readLines: string seq -> Result<Config, string>

    [<CompiledName("ReadString")>]
    val readString: string -> Result<Config, string>

    [<CompiledName("IsEmpty")>]
    val isEmpty: Config -> bool
    
    [<CompiledName("Contains")>]
    val containsKey: string -> string -> Config -> bool
    
    [<CompiledName("Find")>]
    val find: string -> string -> Config -> string option

    [<CompiledName("Add")>]
    val add: string -> string -> string -> Config -> Config

    [<CompiledName("Change")>]
    val change: string -> string -> string -> Config -> Config

    [<CompiledName("Remove")>]
    val remove: string -> string -> Config -> Config

    [<CompiledName("WriteLines")>]
    val writeLines: Config -> string seq

    [<CompiledName("WriteString")>]
    val writeString: Config -> string
