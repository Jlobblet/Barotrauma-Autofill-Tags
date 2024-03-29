﻿// Learn more about F# at http://docs.microsoft.com/dotnet/fsharp

open System
open System.IO
open System.Text
open System.Xml.Linq
open Barotrauma_Autofill_Tags
open Barotrauma_Autofill_Tags.Settings
open FSharp.XExtensions

let (|Header|_|) (str: string) =
    if str.StartsWith "== " && str.EndsWith " =="
    then Some(str.Substring(3, str.Length - 6))
    else None

let getAllFiles dir =
    Directory.GetFiles(dir, "*.xml", SearchOption.AllDirectories)
    |> Array.toList

let openFile (file: string) = XDocument.Load(file)

let getItemNameFromIdentifier (text: XDocument) identifier =
    text.Root.Elements()
    |> Seq.tryFind (fun e -> e.Name.LocalName = $"entityname.%s{identifier}")
    |> Option.fold (fun _ e -> e.Value) identifier

let parseArrayAttribute (s: string) =
    s.Split
        (",",
         StringSplitOptions.RemoveEmptyEntries
         ||| StringSplitOptions.TrimEntries)
    |> Seq.toList

let getAttributeValueSafe (name: string) (element: XElement) =
    element.Attribute name
    |> function
    | null -> None
    | attr -> Some attr.Value

let generateTable text (docs: XDocument list) tag =
    let header =
        [ "{| class=\"wikitable\""
          "|-"
          "| Item Name"
          "| Min Number Spawned"
          "| Max Number Spawned"
          "| Probability of Item Being Spawned" ]

    let middle =
        docs
        |> List.collect (fun doc ->
            let items = doc.Root.Elements("Item")

            items
            |> List.ofSeq
            |> List.collect (fun item ->
                let preferredContainers =
                    item.Elements("PreferredContainer")
                    |> Seq.filter (fun elt ->
                        [ getAttributeValueSafe "primary" elt
                          getAttributeValueSafe "secondary" elt ]
                        |> List.choose id
                        |> List.collect parseArrayAttribute
                        |> Set.ofList
                        |> Set.contains tag)

                preferredContainers
                |> List.ofSeq
                |> List.collect (fun container ->
                    let itemName =
                        getItemNameFromIdentifier text <| item.Attribute("identifier").Value

                    let initMin =
                        getAttributeValueSafe "minamount" container
                        |> Option.map int

                    let initMax =
                        getAttributeValueSafe "maxamount" container
                        |> Option.map int

                    let initProb =
                        getAttributeValueSafe "spawnprobability" container
                        |> Option.map float

                    let prob =
                        match initProb, initMax with
                        | None, None -> None
                        | None, Some v when v <= 0 -> None
                        | None, Some v when v > 0 -> Some 1.
                        | Some v, _ -> Some v

                    let min, max =
                        match initMin, initMax with
                        | None, None -> 1, 1
                        | Some min, Some max -> min, max
                        | Some min, None -> min, 0
                        | None, Some max -> 0, max

                    match prob with
                    | Some p ->
                        [ "|-"
                          $"| {{{{hyperlink|%s{itemName}}}}}"
                          $"| %i{min}"
                          $"| %i{max}"
                          $"| {p:P1}" ]
                    | None -> [])))

    let footer = [ "|}" ]

    List.concat [ header; middle; footer ]
    |> String.concat Environment.NewLine

let makePageBody text docs lines =
    let generateTable' = generateTable text docs
    let version = "0.12.0.2"

    let summary =
        [ $"{{{{Version|%s{version}}}}}"
          File.ReadAllText "summary.txt"
          ""
          "__TOC__" ]
        |> String.concat Environment.NewLine

    let listOfTags = StringBuilder()

    listOfTags.AppendLine "== List of Tags =="
    |> ignore<StringBuilder>

    listOfTags.AppendLine "Valid tags include:"
    |> ignore<StringBuilder>

    listOfTags.AppendLine "" |> ignore<StringBuilder>

    let autofillTables = StringBuilder()

    autofillTables.AppendLine "= Autofill Tables ="
    |> ignore<StringBuilder>

    autofillTables.AppendLine "" |> ignore<StringBuilder>

    let rec inner linesLeft =
        match linesLeft with
        | [] -> ()
        | Header category :: rest ->
            listOfTags.AppendLine "  " |> ignore<StringBuilder>

            autofillTables.AppendLine $"== %s{category} =="
            |> ignore<StringBuilder>

            inner rest
        | tag :: rest ->
            listOfTags.AppendLine $" [[#%s{tag}|%s{tag}]]"
            |> ignore<StringBuilder>

            autofillTables.AppendLine $"=== %s{tag} ==="
            |> ignore<StringBuilder>

            autofillTables.AppendLine(generateTable' tag)
            |> ignore<StringBuilder>

            inner rest

    inner lines

    [ summary
      listOfTags.ToString()
      autofillTables.ToString() ]
    |> String.concat Environment.NewLine

[<EntryPoint>]
let main argv =
    let settings = Settings.FromArgv argv
    
    let contentDirectory = Path.Combine(settings.BarotraumaLocation, "Content")
    
    let EnglishText =
        Path.Combine(contentDirectory, "Texts", "English", "EnglishVanilla.xml")
        |> XDocument.Load
    
    let docs =
        Path.Combine(contentDirectory, "Items")
        |> getAllFiles
        |> List.map openFile

    let allTags =
        docs
        |> Seq.collect (fun d -> d.Descendants "PreferredContainer")
        |> Seq.collect (fun xe ->
            [ getAttributeValueSafe "primary" xe
              getAttributeValueSafe "secondary" xe ]
            |> List.choose id)
        |> Seq.collect parseArrayAttribute
        |> Set.ofSeq

    File.WriteAllLines("tags.txt", allTags)

    let template =
        File.ReadAllLines "template.txt" |> List.ofArray

    let article = template |> makePageBody EnglishText docs

    File.WriteAllText("Autofill Tags.txt", article)

    0 // return an integer exit code
