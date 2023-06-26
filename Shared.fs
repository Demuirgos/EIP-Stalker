module Dependency.Shared

open System
open System.Threading
open Dependency.Core

type 't Context= Context of 't * msgBody:string

type Message = 
    | Metadata of Core.Metadata list 
    | Text of string

type 't Handler = {
    Setup : Option<'t Context -> int -> string -> unit>
    Accounts : Option<'t Context -> unit>
    Remove : Option<'t Context -> string -> unit>
    Watching: 't Context -> string -> unit
    Watch: 't Context -> string -> string list -> unit
    Unwatch: 't Context -> string -> string list -> unit
    Notify: 't Context -> string -> string -> unit
    Ignore: 't Context -> string  -> unit
}

let isNumber listOfNumericalStrings = Seq.forall Char.IsDigit listOfNumericalStrings

let rec HandleMessage (preContext:'a) ((userId, msgBody):string * string) (handler:'a Handler)= 
    let commandLine = msgBody.Split() |> List.ofArray |> List.map (fun str -> str.Trim())
    match commandLine with 
    | "setup"::"--period"::period::rest -> 
        let user = 
            match rest with 
            | "--notify"::[email] -> email
            | [] -> userId
        match handler.Setup with 
        | Some(setup) -> setup (Context (preContext, msgBody)) (Int32.Parse period) user
        | None -> ()
    | ["accounts?"]-> 
        match handler.Accounts with 
        | Some(show) -> show (Context (preContext, msgBody)) 
        | None -> ()
        
    | ["remove"]-> 
        match handler.Remove with 
        | Some(remove) -> remove (Context (preContext, msgBody)) userId
        | None -> ()
    | ["watching?"]->
        handler.Watching (Context (preContext, msgBody)) userId
    | "watch"::eips ->  
        handler.Watch (Context (preContext, msgBody)) userId eips
    | "unwatch"::eips -> 
        handler.Unwatch (Context (preContext, msgBody)) userId eips
    | "notify"::[email] -> 
        handler.Notify (Context (preContext, msgBody)) userId email
    | "ignore"::["email"] -> 
        handler.Ignore (Context (preContext, msgBody)) userId
    | _ -> ()

let rec ReadLiveCommand preCtx handler= 
    printf "\n::> "
    HandleMessage preCtx (String.Empty, Console.ReadLine()) handler
    do ReadLiveCommand preCtx
