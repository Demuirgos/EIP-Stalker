module Dependency.Shared

open System
open System.Threading
open Dependency.Core
open System.Collections.Generic

type 't Context= Context of 't * msgBody:string

type Message = 
    | Metadata of Core.Metadata list 
    | Text of string


type ID = 
    | Discord of uint64
    | Slack of string
    | Guid of string
    | Admin


type 't Handler = {
    Setup : 't Context -> int -> (ID * string option) -> unit
    Accounts : Option<'t Context -> unit>
    Remove : Option<'t Context -> string option-> unit>
    Watching: 't Context -> string option-> unit
    Watch: 't Context -> string option-> string list -> unit
    Unwatch: 't Context -> string option-> string list -> unit
    Notify: 't Context -> string option-> string -> unit
    Ignore: 't Context -> string  option-> unit
}


type User = {
        mutable LocalId : string
        mutable SlackId : string option
        mutable DiscordId : uint64 option
        mutable Email : string option
    } with  member self.ToString = self.LocalId
            static member Create id = {
                LocalId = id
                SlackId = None
                DiscordId = None
                Email = None
            }
            member self.WithDiscordId discordId = self.DiscordId <- discordId; self
            member self.WithSlackId slackId = self.SlackId <- slackId; self
            member self.WithEmail email = self.Email <- email; self


let isNumber listOfNumericalStrings = Seq.forall Char.IsDigit listOfNumericalStrings





let rec HandleMessage (preContext:'a) ((userId, msgBody):ID * string) (commandHandler:'a Handler option) (accountResolver: ID -> string option)= 
    let user = accountResolver userId
    match commandHandler with 
    | None -> ()
    | Some handler ->
        let commandLine = msgBody.Split() |> List.ofArray |> List.map (fun str -> str.Trim())
        match commandLine with 
        | "setup"::"--period"::period::rest ->
            let userRef = 
                match rest with 
                | "--userRef"::[userRef] -> Some userRef
                | _ -> None
            handler.Setup (Context (preContext, msgBody)) (Int32.Parse period) (userId, userRef)
        | ["accounts?"]-> 
            match handler.Accounts with 
            | Some(show) -> show (Context (preContext, msgBody)) 
            | None -> ()
        
        | ["remove"]-> 
            match handler.Remove with 
            | Some(remove) -> remove (Context (preContext, msgBody)) user
            | None -> ()
        | ["watching?"]->
            handler.Watching (Context (preContext, msgBody)) user
        | "watch"::eips ->  
            handler.Watch (Context (preContext, msgBody)) user eips
        | "unwatch"::eips -> 
            handler.Unwatch (Context (preContext, msgBody)) user eips
        | "notify"::[email] -> 
            handler.Notify (Context (preContext, msgBody)) user email
        | "ignore"::["email"] -> 
            handler.Ignore (Context (preContext, msgBody)) user
        | _ -> ()

let rec ReadLiveCommand preCtx handler resolver= 
    printf "\n::> "
    HandleMessage preCtx (Admin, Console.ReadLine()) handler resolver
    do ReadLiveCommand preCtx handler resolver
