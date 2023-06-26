module Dependency.Discord 

open System
open Discord
open Discord.WebSocket
open System.Threading.Tasks
open Dependency.Shared
open Dependency.Config

let mutable client : DiscordSocketClient = 
    let privilage = GatewayIntents.AllUnprivileged ||| GatewayIntents.MessageContent ||| GatewayIntents.GuildPresences
    let discordSocketConfig = new DiscordSocketConfig()
    discordSocketConfig.GatewayIntents <- privilage
    DiscordSocketClient(discordSocketConfig)

let public Run config silos handler =
    async {
        do! client.LoginAsync(TokenType.Bot, config.DiscordConfig.Token)
            |> Async.AwaitTask

        client.add_MessageReceived(fun msg -> 
            task {
                if msg.Channel.Id = config.DiscordConfig.Channel then 
                    do Shared.HandleMessage silos  (string msg.Author.Id, msg.Content) handler
                return 0
            }
        )

        do! client.StartAsync()
            |> Async.AwaitTask
        do! Task.Delay -1
            |> Async.AwaitTask
    }


let public SendMessageAsync  config userId (message: Message)= 
    let messageBody = 
        match message with 
        | Text body -> body 
        | Metadata metadata -> 
            String.Join('\n', metadata |> List.map (fun entry -> sprintf "Eip %d: %s" entry.Number entry.Link))
    
    async {
        let! channel = 
            client.GetChannelAsync(config.DiscordConfig.Channel).AsTask() 
            |> Async.AwaitTask 
        let messageChannel = channel :?> IMessageChannel
        match userId with 
            | Some(userId) -> 
                let! user = messageChannel.GetUserAsync(userId, CacheMode.AllowDownload ) |> Async.AwaitTask
                let! _ = user.SendMessageAsync (sprintf "%s here are the eips that changed: \n%s" user.Mention messageBody) |> Async.AwaitTask 
                return ()
            | None -> 
                let! _ = messageChannel.SendMessageAsync (sprintf "%s" messageBody)|> Async.AwaitTask 
                return ()
    }

