module Dependency.Discord 

open System
open Discord
open Discord.WebSocket
open System.Threading.Tasks
open Dependency.Shared
open Dependency.Config

type Message = 
    | Metadata of Core.Metadata list 
    | Text of string

let mutable client : DiscordSocketClient = DiscordSocketClient()

let public Run config silos handler =
    do client.LoginAsync(TokenType.Bot, config.DiscordToken)
        |> Async.AwaitTask

    client.add_MessageReceived(fun msg -> 
        task {
            do Shared.HandleMessage silos  (msg.Author.Id, msg.Content) handler
            return 0
        }
    )

    do client.StartAsync()
        |> Async.AwaitTask
    Task.Delay -1

let public SendMessageAsync  config userId (message: Message)= 
    let messageBody = 
        match message with 
        | Text body -> body 
        | Metadata metadata -> 
            String.Join('\n', metadata |> List.map (fun entry -> sprintf "Eip %d: %s" entry.Number entry.Link))
    
    async {
        let! channel = 
            client.GetChannelAsync(config.Channel).AsTask() 
            |> Async.AwaitTask 
        let messageChannel = channel :?> IMessageChannel
        let! msg = match userId with 
            | Some(userId) -> 
                async {
                    let! user = userId |> messageChannel.GetUserAsync |> Async.AwaitTask
                    return sprintf "%s: \n%s" user.Mention messageBody 
                }
            | None -> 
                async {
                    return sprintf "%s" messageBody 
                }
        do  messageChannel.SendMessageAsync msg
            |> Async.AwaitTask 
            |> ignore
        return ()
    }

