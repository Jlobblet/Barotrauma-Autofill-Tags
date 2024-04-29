module Barotrauma_Autofill_Tags.Settings

open System
open Argu

type Arguments =
    | [<Unique; CustomAppSettings("OutputLocation")>] OutputLocation of path: string
    | [<Unique; CustomAppSettings("SummaryLocation")>] SummaryLocation of path: string
    | [<Unique; CustomAppSettings("TemplateLocation")>] TemplateLocation of path: string
    | [<Unique; CustomAppSettings("BarotraumaLocation")>] BarotraumaLocation of path: string

    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | OutputLocation _ -> "The directory to put output files"
            | SummaryLocation _ -> "Path to the summary displayed at the top of the article is"
            | TemplateLocation _ -> "Path to the template for the order of tags"
            | BarotraumaLocation _ -> "The location where Barotrauma is installed"

let errorHandler =
    ProcessExiter(
        colorizer =
            function
            | ErrorCode.HelpText -> None
            | _ -> Some ConsoleColor.Red
    )

let Parser = ArgumentParser.Create<Arguments>(errorHandler = errorHandler)

[<Struct>]
type Settings =
    { OutputLocation: string
      SummaryLocation: string
      TemplateLocation: string
      BarotraumaLocation: string }

    static member FromArgv argv =
        let results = Parser.Parse argv

        let outputLocation =
            results.TryGetResult <@ OutputLocation @> |> Option.defaultValue "."

        let summaryLocation =
            results.TryGetResult <@ SummaryLocation @> |> Option.defaultValue "summary.txt"

        let templateLocation =
            results.TryGetResult <@ TemplateLocation @>
            |> Option.defaultValue "template.txt"

        let barotraumaLocation = results.GetResult <@ BarotraumaLocation @>

        { OutputLocation = outputLocation
          SummaryLocation = summaryLocation
          TemplateLocation = templateLocation
          BarotraumaLocation = barotraumaLocation }
