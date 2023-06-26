module Dependency.Handlers.Slack

open System
open Dependency.Monitor
open Dependency.Config
open Dependency.Silos
open Dependency.Shared

let Handler (config: SlackConfig) = 
    let createNewUser period slackId silos= 
        let userId = Guid.NewGuid().ToString()
        let user = (User.Create (userId))
                    .WithSlackId slackId
        let monitor = Monitor(user, silos.Config)
        do Dependency.Silos.AddAccount user.LocalId monitor silos 
        do monitor.Start period Dependency.Silos.TemporaryFilePath

    if not <| config.Include 
    then None
    else
        Some <| {
            Setup = function
                | Context((config, silos), _) -> 
                    fun period (userId, userRef) -> 
                        match userId, userRef with 
                        | Slack id, Some oldUser -> 
                            let user = silos.Monitors[oldUser]
                            do ignore <| user.UserInstance
                                .WithSlackId (Some id)
                        | Slack id, None -> createNewUser period (Some id) silos
                        | _ -> failwith "unreacheable code"
                        ()
            Accounts = None
            Remove = None
            Watching =    function
                | Context((config, silos), _) -> 
                    fun userId -> 
                        match userId with 
                        | Some userId -> 
                            let user =  silos.Monitors[userId]
                            let message = sprintf "Currently Watching : %A" (user.Current())
                            Dependency.Slack.SendMessageAsync config (user.UserInstance.SlackId) (Text message)
                            |> Async.RunSynchronously
                        | None -> printfn "Account not yet setup, please setup the account"
            Watch =    function
                | Context((config, silos), _) -> 
                    fun userId eips -> 
                        match userId with 
                        | Some userId -> 
                            let eips = [ yield! List.takeWhile isNumber eips ] |> List.map Int32.Parse
                            let user =  silos.Monitors[userId]
                            do user.Watch (Set.ofList eips)
                            let message = sprintf "Started Watching : %A" eips
                            Dependency.Slack.SendMessageAsync config (user.UserInstance.SlackId) (Text message)
                            |> Async.RunSynchronously
                        | None -> printf "Account not yet setup, please setup the account"
            Unwatch =    function
                | Context((config, silos), _) -> 
                    fun userId eips -> 
                        match userId with 
                        | Some userId -> 
                            let eips = [ yield! List.takeWhile isNumber eips ] |> List.map Int32.Parse
                            let user =  silos.Monitors[userId]
                            do user.Unwatch (Set.ofList eips)
                                    
                            let message = sprintf "Stopped Watching : %A" eips
                            Dependency.Slack.SendMessageAsync config (user.UserInstance.SlackId) (Text message)
                            |> Async.RunSynchronously
                        | None -> printf "Account not yet setup, please setup the account"
            Notify = function 
                | Context((config, silos), _) -> 
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
                | Context((config, silos), _) -> 
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