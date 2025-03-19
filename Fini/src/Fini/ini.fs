namespace Fini

type Parameters = Map<string, string>
type Sections = Map<string, Parameters>

type Ini(sections: Sections) =
    new() = Ini(Map.empty)

module Ini =

    let internal addParameter (parameter: string * string) (sectionName: string) (sections: Sections) : Sections =
        let parameters = sections[sectionName].Add parameter
        let section = (sectionName, parameters)
        let sections = sections.Add section
        sections

    let internal addSection (sectionName: string) (sections: Sections) : Sections =
        let section = (sectionName, Map.empty)
        let sections = sections.Add section
        sections

    let internal parseLines (lines: Parser.Line list) : Result<Ini, string> =
        let rec loop (lines: Parser.Line list) (sectionName: string) (sections: Sections) : Result<Ini, string> =
            match lines with
            | [] -> Ini(sections) |> Ok
            | Parser.Empty :: tail -> loop tail sectionName sections
            | Parser.Section section :: tail -> loop tail section (addSection section sections)
            | Parser.Parameter parameter :: tail -> loop tail sectionName (addParameter parameter sectionName sections)
        loop lines "" (Sections [ ("", Map.empty) ])
