module Dependency.Resolver

open System
open Dependency.Core
    

[<EntryPoint>]
let main args = 
    match Array.toList args with 
    | "--eip"::eip::t-> 
        let target = int eip
        let depth = match t with 
                    | "--depth"::depth::_ -> int depth
                    | _ -> 1
        let page = Metadata.FetchMetadata target depth
        printfn "%A" page
        0
    | _ -> 
        printfn "Usage: --depth <depth> <eip>"
        1