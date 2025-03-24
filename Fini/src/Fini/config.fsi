namespace Fini

type Config =
    new: unit -> Config

module Config =
    val value: Config -> string -> string -> string option
    val globalValue: Config -> string -> string option
    val fromLines: string seq -> Result<Config, string>
    val fromString: string -> Result<Config, string>
    val toLines: Config -> string seq
    val toString: Config -> string
