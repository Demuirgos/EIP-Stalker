module Dependency.Resolver

open System
open Dependency.Core
open System.Threading
    

[<EntryPoint>]
let main args = 
    Console.CancelKeyPress.Add(fun _ -> Monitor.Stop())
    match Array.toList args with 
    | "--eip"::eip::t-> 
        let target = int eip
        let depth = match t with 
                    | "--depth"::depth::_ -> int depth
                    | _ -> 1
        let page = Metadata.FetchMetadata target depth
        printfn "%A" page
        0
    | "--monitor"::eips-> 
        let target = List.map int eips 
        Monitor.Start target
        0
    | _ -> 
        printfn "Usage: --eip <eip> --depth <depth> || --monitor <eip>+"
        1