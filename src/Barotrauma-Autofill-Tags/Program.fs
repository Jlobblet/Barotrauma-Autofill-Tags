// Learn more about F# at http://docs.microsoft.com/dotnet/fsharp

open System
open System.IO
open System.Xml.Linq
open Barotrauma_Autofill_Tags.Settings

let getAllFiles dir =
    Directory.GetFiles(dir, "*.xml", SearchOption.AllDirectories)

let parseArrayAttribute (s: string) =
    s.Split(",", StringSplitOptions.RemoveEmptyEntries ||| StringSplitOptions.TrimEntries)

let getAttributeValueSafe (name: string) (element: XElement) =
    match element.Attribute name with
    | null -> None
    | attr -> Some attr.Value

type SpawnAmount =
    | Exact of int
    | Range of min: int * max: int

module SpawnAmount =
    let format i = function
        | Exact n -> $"c%i{i}amount = %i{n}"
        | Range (min, max) -> $"c%i{i}minamount = %i{min} | c%i{i}maxamount = %i{max}"

type PreferredContainer =
    { Type: string
      Tag: string
      SpawnProbability: Decimal
      Amount: SpawnAmount
      NotCampaign: bool }
    
module PreferredContainer =
    let format i { Type = type'
                   Tag = tag
                   SpawnProbability = spawnProbability
                   NotCampaign = notCampaign
                   Amount = amount } =
        [ $"c%i{i}type = %s{type'}"
          $"c%i{i}tag = %s{tag}"
          $"c%i{i}spawnprobability = {spawnProbability}"
          $"c%i{i}notcampaign = {notCampaign}"
          SpawnAmount.format i amount ]
        |> String.concat " | "

    let parse (element: XElement): PreferredContainer[] =
        
        [|"primary"; "secondary"|]
        |> Array.collect (fun t ->
            getAttributeValueSafe t element
            |> Option.map (fun raw ->
                parseArrayAttribute raw
                |> Array.map (fun tag -> t, tag))
            |> Option.defaultValue [||])
        |> Array.map (fun (type', tag) ->
            let prob = getAttributeValueSafe "spawnprobability" element |> Option.map Decimal.Parse
            let amount = getAttributeValueSafe "amount" element |> Option.map int
            let maxAmount = getAttributeValueSafe "maxamount" element |> Option.map int
            let minAmount = getAttributeValueSafe "minamount" element |> Option.fold (fun _ -> int) 0
            let campaignOnly = getAttributeValueSafe "campaignonly" element |> Option.fold (fun _ -> bool.Parse) false

            let prob, spawnAmount =
                match prob, amount, maxAmount with
                // if spawn probability and amount is defined, use it
                | Some(p), Some(a), _ when a > 0 -> p, SpawnAmount.Exact a
                | Some(p), _, Some(ma) when ma > 0 -> p, SpawnAmount.Range(minAmount, max minAmount ma)
                // if spawn probability is not defined but amount is, assume the probability is 1
                | None, Some(a), _ when a > 0 -> 1m, SpawnAmount.Exact a
                | None, _, Some(ma) when ma > 0 -> 1m, SpawnAmount.Range(minAmount, max minAmount ma)
                // spawn probability defined but amount isn't, assume amount is 1
                | Some(p), None, None -> p, SpawnAmount.Exact 1
                // otherwise, set the probability to 0 and the amount to 1
                | _ -> 0m, SpawnAmount.Exact 1

            { Type = type'
              Tag = tag
              SpawnProbability = prob
              Amount = spawnAmount
              NotCampaign = campaignOnly })

let processItem (item: XElement) =
    let itemIdentifier = item.Attribute("identifier")
    
    if itemIdentifier = null then
        None
    else
        let details =
            item.Elements("PreferredContainer")
            |> Seq.collect PreferredContainer.parse
            |> Seq.mapi (fun i -> PreferredContainer.format (i + 1))

        if Seq.isEmpty details then
            None
        else
            Some $$$"""{{afr | %s{{{itemIdentifier.Value}}} | %s{{{String.concat " | " details}}} }}"""

let makePageBody docs =
    docs
    |> Seq.collect (fun (doc: XDocument) -> doc.Root.Elements() |> Seq.map processItem)
    |> Seq.choose id
    |> String.concat Environment.NewLine

[<EntryPoint>]
let main argv =
    let settings = Settings.FromArgv argv

    let contentDirectory = Path.Combine(settings.BarotraumaLocation, "Content")

    let docs =
        Path.Combine(contentDirectory, "Items")
        |> getAllFiles
        |> Array.map XDocument.Load
        |> Array.filter (fun doc -> doc.Root.Name.LocalName = "Items")

    let allTags =
        docs
        |> Seq.collect (fun d -> d.Descendants "PreferredContainer")
        |> Seq.collect (fun xe ->
            [ getAttributeValueSafe "primary" xe; getAttributeValueSafe "secondary" xe ]
            |> List.choose id)
        |> Seq.collect parseArrayAttribute
        |> Set.ofSeq

    File.WriteAllLines("tags.txt", allTags)

    let article = makePageBody docs

    File.WriteAllText("Autofill Tags.txt", article)

    0 // return an integer exit code
