// Learn more about F# at http://docs.microsoft.com/dotnet/fsharp

open System
open System.IO
open System.Text
open System.Xml.Linq

let (|Header|_|) (str: string) =
    if str.StartsWith "== " && str.EndsWith " =="
    then Some(str.Substring(3, str.Length - 6))
    else None

let ContentDirectory =
    Path.Combine("C:", "Program Files (x86)", "Steam", "steamapps", "common", "Barotrauma", "Content")

let EnglishText =
    Path.Combine(ContentDirectory, "Texts", "English", "EnglishVanilla.xml")
    |> XDocument.Load

let getAllFiles dir =
    Directory.GetFiles(dir, "*.xml", SearchOption.AllDirectories)
    |> Array.toList

let openFile (file: string) = XDocument.Load(file)

let getItemNameFromIdentifier identifier =
    EnglishText.Root.Elements()
    |> Seq.tryFind (fun e -> e.Name.LocalName = $"entityname.%s{identifier}")
    |> Option.fold (fun _ e -> e.Value) identifier

let parseArrayAttribute (s: string) =
    s.Split
        (",",
         StringSplitOptions.RemoveEmptyEntries
         ||| StringSplitOptions.TrimEntries)
    |> Seq.toList

let getAttributeValueSafe (name: string) (element: XElement) =
    element.Attribute(name)
    |> function
    | null -> None
    | attr -> Some attr.Value

let generateTable (docs: XDocument list) tag =
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
                        getItemNameFromIdentifier
                        <| item.Attribute("identifier").Value

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

                    let (min, max) =
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
                          $"| {p}" ]
                    | None -> [])))

    let footer = [ "|}" ]

    List.concat [ header; middle; footer ]
    |> String.concat Environment.NewLine

let makePageBody docs lines =
    let generateTable' = generateTable docs
    let version = "0.12.0.2"

    let summary =
        [ $"{{{{Version|%s{version}}}}}"
          File.ReadAllText "summary.txt"
          ""
          "__TOC__" ]
        |> String.concat Environment.NewLine

    let listOfTags = StringBuilder()

    listOfTags.AppendLine "== List of Tags =="
    |> ignore

    listOfTags.AppendLine "Valid tags include:"
    |> ignore

    listOfTags.AppendLine "" |> ignore

    let autofillTables = StringBuilder()

    autofillTables.AppendLine "= Autofill Tables ="
    |> ignore

    autofillTables.AppendLine "" |> ignore

    let rec inner linesLeft =
        match linesLeft with
        | [] -> ()
        | Header category :: rest ->
            listOfTags.AppendLine "  " |> ignore

            autofillTables.AppendLine $"== %s{category} =="
            |> ignore

            inner rest
        | tag :: rest ->
            listOfTags.AppendLine $" [[#%s{tag}|%s{tag}]]"
            |> ignore

            autofillTables.AppendLine $"=== %s{tag} ==="
            |> ignore

            autofillTables.AppendLine(generateTable' tag)
            |> ignore

            inner rest

    inner lines

    [ summary
      listOfTags.ToString()
      autofillTables.ToString() ]
    |> String.concat Environment.NewLine

[<EntryPoint>]
let main argv =
    let docs =
        Path.Combine(ContentDirectory, "Items")
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

    //    if (template |> List.filter (not << ))

    let article = template |> makePageBody docs

    File.WriteAllText("Autofill Tags.txt", article)

    0 // return an integer exit code
