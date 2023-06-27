module Dependency.Handlers.Discord

open System
open Dependency.Monitor
open Dependency.Config
open Dependency.Silos
open Dependency.Shared

let Handler (config: Config) = 
    let createNewUser period discordId silos= 
        match Dependency.Silos.ResolveAccount silos (Discord discordId) with 
        | Some id -> None
        | _ -> 
            let userId = Guid.NewGuid().ToString()
            let user = (User.Create (userId))
                        .WithDiscordId (Some discordId)
            let monitor = Monitor(user, silos.Config)
            do Dependency.Silos.AddAccount user.LocalId monitor silos 
            do monitor.Start period Dependency.Silos.TemporaryFilePath
            Some userId
    if not <| config.DiscordConfig.Include 
    then None
    else
        Some <| {
            Setup = function
                | Context((silos, channelId:uint64), _) -> 
                    fun period (userId, userRef) -> 
                        let (id, message) = 
                            match userId, userRef with 
                            | Discord id, Some oldUser -> 
                                let user = silos.Monitors[oldUser]
                                do ignore <| user.UserInstance
                                    .WithDiscordId (Some id)
                                id, sprintf "Discord account hooked to Id : %s" oldUser 
                            | Discord id, None -> 
                                let message = 
                                    match createNewUser period id silos with
                                    | Some ref_id -> sprintf "Discord account hooked with Id : %s" ref_id 
                                    | None -> sprintf "Account with Id : %d already exists" id
                                id, message
                            | _ -> failwith "unreacheable code"
                        Dependency.Discord.SendMessageAsync config (Some id) (Text message)
                        |> Async.RunSynchronously
            Accounts = None
            Remove = None
            Watching =    function
                | Context((silos, channelId), _) -> 
                    fun userId -> 
                        match userId with 
                        | Some userId -> 
                            let user = silos.Monitors[userId]
                            let message = sprintf "Currently Watching : %A" (user.Current())
                            Dependency.Discord.SendMessageAsync config (user.UserInstance.DiscordId) (Text message)
                            |> Async.RunSynchronously
                        | None -> printfn "Account not yet setup, please setup the account"
            Watch =    function
                | Context((silos, channelId), _) -> 
                    fun userId eips -> 
                        match userId with 
                        | Some userId -> 
                            let eips = [ yield! List.takeWhile isNumber eips ] |> List.map Int32.Parse
                            let user =  silos.Monitors[userId]
                            do user.Watch (Set.ofList eips)
                            let message = sprintf "Started Watching : %A" eips
                            Dependency.Discord.SendMessageAsync config (user.UserInstance.DiscordId) (Text message)
                            |> Async.RunSynchronously
                        | None -> printf "Account not yet setup, please setup the account"
            Unwatch =    function
                | Context((silos, channelId), _) -> 
                    fun userId eips -> 
                        match userId with 
                        | Some userId -> 
                            let eips = [ yield! List.takeWhile isNumber eips ] |> List.map Int32.Parse
                            let user =  silos.Monitors[userId]
                            do user.Unwatch (Set.ofList eips)
                                    
                            let message = sprintf "Stopped Watching : %A" eips
                            Dependency.Discord.SendMessageAsync config (user.UserInstance.DiscordId) (Text message)
                            |> Async.RunSynchronously
                        | None -> printf "Account not yet setup, please setup the account"
            Notify = function 
                | Context((silos, channelId), _) -> 
                    fun userId email ->  
                        match userId with 
                        | Some userId -> 
                            let user =  silos.Monitors[userId]
                            let message = 
                                ignore <| user.UserInstance.WithEmail (Some email)
                                sprintf "Email %s notifications activated" email
                            Dependency.Discord.SendMessageAsync config (user.UserInstance.DiscordId) (Text message)
                            |> Async.RunSynchronously
                        | None -> printf "Account not yet setup, please setup the account"
            Ignore = function 
                | Context((silos, channelId), _) -> 
                    fun userId ->  
                        match userId with 
                        | Some userId -> 
                            let user =  silos.Monitors[userId]
                            let message = 
                                ignore <| user.UserInstance.WithEmail (None)
                                sprintf "Email notifications deactivated" 
                            Dependency.Discord.SendMessageAsync config (user.UserInstance.DiscordId) (Text message)
                            |> Async.RunSynchronously
                        | None -> printf "Account not yet setup, please setup the account"
        }