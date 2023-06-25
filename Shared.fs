module Dependency.Shared

open System
open System.Threading
open Dependency.Core

type 't Context= Context of 't * msgBody:string

type 't Handler = {
    Setup : 't Context -> int -> string -> unit
    Accounts : 't Context -> unit
    Remove : 't Context -> string -> unit
    Watching: 't Context -> string -> unit
    Watch: 't Context -> string -> string list -> unit
    Unwatch: 't Context -> string -> string list -> unit
}

let isNumber listOfNumericalStrings = Seq.forall Char.IsDigit listOfNumericalStrings

let rec HandleMessage (preContext:'a) ((sender, msgBody):UInt64 * string) (handler:'a Handler)= 

    let userId = sender.ToString()

    let commandLine = msgBody.Split() |> List.ofArray |> List.map (fun str -> str.Trim())
    match commandLine with 
    | "setup"::"--period"::period::rest -> 
        let user = 
            match rest with 
            | "--notify"::[email] -> email
            | [] -> userId
        handler.Setup (Context (preContext, msgBody)) (Int32.Parse period) user
    | ["accounts?"]-> 
        handler.Accounts (Context (preContext, msgBody)) 
    | ["remove"]-> 
        handler.Remove (Context (preContext, msgBody)) userId
    | ["watching?"]->
        handler.Watching (Context (preContext, msgBody)) userId
    | "watch"::eips ->  
        handler.Watch (Context (preContext, msgBody)) userId eips
    | "unwatch"::eips -> 
        handler.Unwatch (Context (preContext, msgBody)) userId eips
    | _ -> ()


let rec ReadLiveCommand preCtx handler= 
    printf "\n::> "
    HandleMessage preCtx (UInt64.MinValue, Console.ReadLine()) handler
    do ReadLiveCommand preCtx
