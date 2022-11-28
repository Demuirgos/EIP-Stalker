module Dependency.Resolver

open System
open Dependency.Core
    
let target = Console.ReadLine() |> int
let page = Metadata.FetchMetadata target
printfn "%A" page