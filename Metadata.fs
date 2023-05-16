module Dependency.Core

open FSharp.Data
open System.Collections.Generic

type Metadata = {
    Number  : int
    Author  : string list
    Status  : string
    Type    : string
    Category: string
    Created : string
    Require : int list option
    Discussion : string option
}

module Metadata = 
    let private Fetch eipNum = 
        try 
            Some (HtmlDocument.Load(sprintf "https://eips.ethereum.org/EIPS/eip-%d" eipNum))
        with
        | _ -> None
    let rec private ExtractMetadata (eipnum:int) (depth:int) (page:HtmlDocument) =
        let rec ResolveDependencies (eips:int list) =
            let memo = new HashSet<_>()
            let rec Resolve nesting eip : unit =
                if nesting = 0 then 
                    ignore (memo.Add(eip))
                else
                    if memo.Contains(eip) then ()
                    else
                        let eipPage = Fetch eip
                        let dependencies = Option.map (ExtractMetadata eip (depth - 1)) eipPage
                        ignore (memo.Add(eip))
                        match dependencies with 
                        | Some deps -> 
                            deps.Require 
                            |> function 
                            | Some deps -> deps |> List.iter (Resolve (nesting - 1))
                            | None -> ()
                        | None -> ()

            if List.isEmpty eips then None
            else 
                do eips |> List.iter (Resolve depth)
                memo |> Seq.toList |> Some

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
                Number = eipnum
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

    let public FetchMetadata eip depth = 
        eip 
        |> Fetch 
        |> function 
        | Some page -> Ok <| ExtractMetadata eip depth page 
        | None -> Error "Failed to fetch page"
    
