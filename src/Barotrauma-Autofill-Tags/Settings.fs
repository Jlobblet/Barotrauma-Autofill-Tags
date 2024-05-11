module Barotrauma_Autofill_Tags.Settings

open System
open Argu

type Arguments =
    | [<Unique; CustomAppSettings("OutputLocation")>] OutputLocation of path: string
    | [<Unique; CustomAppSettings("BarotraumaLocation")>] BarotraumaLocation of path: string

    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | OutputLocation _ -> "The directory to put output files"
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
      BarotraumaLocation: string }

    static member FromArgv argv =
        let results = Parser.Parse argv

        let outputLocation =
            results.TryGetResult <@ OutputLocation @> |> Option.defaultValue "."

        let barotraumaLocation = results.GetResult <@ BarotraumaLocation @>

        { OutputLocation = outputLocation
          BarotraumaLocation = barotraumaLocation }
