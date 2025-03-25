namespace Fini

type Config =
    new: unit -> Config

    static member Empty: Config
    static member FromLines: string seq -> Result<Config, string>
    static member FromString: string -> Result<Config, string>

    member AppendLines: string seq -> Result<Config, string>
    member AppendString: string -> Result<Config, string>

    member IsEmpty: bool
    member ContainsKey: section: string * key: string -> bool

    member Find: section: string * key: string -> string option
    member Add: section: string * key: string * value: string -> Config
    member Change: section: string * key: string * value: string -> Config
    member Remove: section: string * key: string -> Config
    
    member ToLines: unit -> string seq
    override ToString: unit -> string

module Config =

    [<CompiledName("Empty")>]
    val empty: Config

    [<CompiledName("FromLines")>]
    val fromLines: string seq -> Result<Config, string>

    [<CompiledName("FromString")>]
    val fromString: string -> Result<Config, string>

    [<CompiledName("AppendLines")>]
    val appendLines: Config -> string seq -> Result<Config, string>

    [<CompiledName("AppendString")>]
    val appendString: Config -> string -> Result<Config, string>

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

    [<CompiledName("ToLines")>]
    val toLines: Config -> string seq

    [<CompiledName("ToString")>]
    val toString: Config -> string
