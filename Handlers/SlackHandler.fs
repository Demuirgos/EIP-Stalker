﻿module Dependency.Handlers.Slack

open System
open Dependency.Monitor
open Dependency.Config
open Dependency.Silos
open Dependency.Shared

let Handler (config: Config) = 
    let createNewUser period slackId silos= 
        match Dependency.Silos.ResolveAccount silos (UserID.Slack slackId) with 
        | Some id -> None
        | _ -> 
            let userId = Guid.NewGuid().ToString()
            let user = (User.Create (userId))
                        .WithSlackId (Some slackId)
            let monitor = Monitor(period, user, silos.Config)
            do Dependency.Silos.AddAccount user.LocalId monitor silos 
            do monitor.Start Dependency.Silos.TemporaryFilePath
            Some userId

    if not <| config.SlackConfig.Include 
    then None
    else
        Some <| {
            Setup = Some <| function
                | Context((silos, channelId:string), _) -> 
                    fun period (userId, userRef) -> 
                        let (id, message) = 
                            match userId, userRef with 
                            | UserID.Slack id, Some oldUser -> 
                                let user = silos.Monitors[oldUser]
                                do ignore <| user.UserInstance
                                    .WithSlackId (Some id)
                                id, sprintf "Slack account hooked to Id : %s" oldUser 
                            | UserID.Slack id, None -> 
                                let message = 
                                    match createNewUser (Some period) id silos with
                                    | Some ref_id -> sprintf "Slack account hooked with Id : %s" ref_id 
                                    | None -> sprintf "Account with Id : %s already exists" id
                                id, message
                            | _ -> failwith "unreacheable code"
                        Dependency.Slack.SendMessageAsync config (Some id) (Text message)
                        |> Async.RunSynchronously
            Accounts = None
            Remove = None
            Watching =    function
                | Context((silos, channelId), _) -> 
                    fun userId -> 
                        match userId with 
                        | Some userId -> 
                            let user =  silos.Monitors[userId]
                            let message = sprintf "Currently Watching : %A" (user.Current())
                            Dependency.Slack.SendMessageAsync config (user.UserInstance.SlackId) (Text message)
                            |> Async.RunSynchronously
                        | None -> printfn "Account not yet setup, please setup the account"
            Watch =    function
                | Context((silos, channelId), _) -> 
                    fun userId eips -> 
                        match userId with 
                        | Some userId -> 
                            let eips = [ yield! List.takeWhile IsNumber eips ] |> List.map Int32.Parse
                            let user =  silos.Monitors[userId]
                            do user.Watch (Set.ofList eips)
                            let message = sprintf "Started Watching : %A" eips
                            Dependency.Slack.SendMessageAsync config (user.UserInstance.SlackId) (Text message)
                            |> Async.RunSynchronously
                        | None -> printf "Account not yet setup, please setup the account"
            Unwatch =    function
                | Context((silos, channelId), _) -> 
                    fun userId eips -> 
                        match userId with 
                        | Some userId -> 
                            let eips = [ yield! List.takeWhile IsNumber eips ] |> List.map Int32.Parse
                            let user =  silos.Monitors[userId]
                            do user.Unwatch (Set.ofList eips)
                                    
                            let message = sprintf "Stopped Watching : %A" eips
                            Dependency.Slack.SendMessageAsync config (user.UserInstance.SlackId) (Text message)
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
                            Dependency.Slack.SendMessageAsync config (user.UserInstance.SlackId) (Text message)
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
                            Dependency.Slack.SendMessageAsync config (user.UserInstance.SlackId) (Text message)
                            |> Async.RunSynchronously
                        | None -> printf "Account not yet setup, please setup the account"
        }