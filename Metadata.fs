module Dependency.Core

open FSharp.Data
open System.Collections.Generic

type Metadata = {
    Author  : string list
    Status  : string
    Type    : string
    Category: string
    Created : string
    Require : int list
    Discussion : string option
}

module Metadata = 
    let private Fetch eipNum = 
        try 
            Some (HtmlDocument.Load(sprintf "https://eips.ethereum.org/EIPS/eip-%d" eipNum))
        with
        | _ -> None
    let rec private ExtractMetadata (page:HtmlDocument) =
        let rec ResolveDependencies (eips:int list) =
            let memo = new HashSet<_>()
            let rec Resolve eip : unit =
                if memo.Contains(eip) then ()
                else
                    let eipPage = Fetch eip
                    let dependencies = Option.map ExtractMetadata eipPage  
                    ignore (memo.Add(eip))
                    match dependencies with 
                    | Some deps -> 
                        deps.Require 
                        |> List.map Resolve
                        |> ignore
                    | None -> ()

            if List.isEmpty eips then [] 
            else 
                do eips |> List.iter Resolve
                memo |> Seq.toList

        let main = 
            page.Descendants["div"]
            |> Seq.filter (fun x -> x.HasClass("home"))

        if Seq.isEmpty main then
            failwith "No main div found"
        else 
            let table = 
                main |> Seq.head
                |> fun x -> x.Descendants["table"]
                |> Seq.filter (fun x -> x.InnerText().Contains("Author"))
                |> Seq.head |> fun x -> x.Descendants["tr"]
            
            let [ AuthorRow; DiscussionRow; StatusRow; CategoryRow; TypeRow; CreatedRow; RequireRow ] = 
                ["Author"; "Discussions"; "Status"; "Category"; "Type"; "Created"; "Requires"]
                |> List.map (fun x -> table |> Seq.filter (fun y -> y.InnerText().Contains(x)) |> Seq.tryHead)
            
            let parseList (nodeOpt : HtmlNode option) = 
                match nodeOpt with
                | Some node -> 
                    node.Descendants["td"] |> Seq.head
                    |> fun x -> x.Descendants["a"]
                    |> Seq.map (fun x -> x.InnerText()) 
                | None -> Seq.empty
                
            
            let parseSingle (nodeOpt : HtmlNode option) = 
                match nodeOpt with
                | Some node -> 
                    node.Descendants["td"] |> Seq.head
                    |> fun x -> x.InnerText()
                | None -> System.String.Empty

            { 
                Author = parseList AuthorRow 
                         |> List.ofSeq; 
                Status = parseSingle StatusRow; 
                Type = parseSingle TypeRow; 
                Category = parseSingle CategoryRow; 
                Created = parseSingle CreatedRow; 
                Discussion = parseList DiscussionRow 
                             |> Seq.tryHead 
                Require = parseList RequireRow 
                            |> Seq.map int 
                            |> List.ofSeq 
                            |> ResolveDependencies
            }

    let public FetchMetadata eip = 
        eip 
        |> Fetch 
        |> function 
        | Some page -> Ok <| ExtractMetadata page
        | None -> Error "Failed to fetch page"
    