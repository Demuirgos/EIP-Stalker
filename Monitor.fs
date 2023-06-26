module Dependency.Monitor

open System.IO
open FSharp.Data
open System.Net.Http
open System.Text.Json
open System.Net.Http.Headers
open System.Threading
open Dependency.Config
open Dependency.Shared
open Dependency.Core

open System
open System.Collections.Generic
open System.Collections.Concurrent

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

[<Class>]
type Monitor(recepient: User, config: Config) = 
    let mutable State : Dictionary<int, string> = Dictionary<_, _>()
    let mutable Flagged : int Set = Set.empty
    let mutable Config : Config = config
    let CancellationToken = new CancellationTokenSource()

    new(path: string, filename:string, config:Config) as self= 
        let user = User.Create filename
        Monitor(user, config)
        then self.Start (3600 * 24) path


    member val UserInstance : User = recepient with get, set
    member public _.Current() = (State, Flagged)
    member public self.Path() = sprintf "%s.json" (self.UserInstance.ToString)

    member private self.TakeSnapshot () : Snapshot.Snapshot =
        {
            Email = self.UserInstance.Email
            Discord = self.UserInstance.DiscordId
            Slack = self.UserInstance.SlackId

            State = State
            Flagged =  Flagged
            Period = 3600 * 24
        }

    member private _.GetRequestWithAuth (key:string) eip =
        async {
            let client = new HttpClient()
            client.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json")
            client.DefaultRequestHeaders.Authorization <- AuthenticationHeaderValue("token", key)
            client.DefaultRequestHeaders.UserAgent.Add(ProductInfoHeaderValue("FSharp", "6.0"))
            let! response =  client.GetAsync(sprintf "https://api.github.com/repos/ethereum/EIPs/contents/EIPS/eip-%i.md" eip) |> Async.AwaitTask
            return! response.Content.ReadAsStringAsync() |> Async.AwaitTask
        } |> Async.RunSynchronously
          |> JsonValue.Parse

    member public self.SaveInFile pathPrefix =
        try
            printf "Save : %A\n::> " State
            let snapshot = self.TakeSnapshot()
            let json = JsonSerializer.Serialize(snapshot)
            File.WriteAllText(Path.Combine(pathPrefix, self.Path()), json)
        with
            | e -> printf "%s" e.Message

    member private self.ReadInFile path =
        try
            let fileContent = File.ReadAllText(path)
            let snapshot = JsonSerializer.Deserialize<Snapshot.Snapshot>(fileContent)
            do ignore <| self.UserInstance
                .WithDiscordId(snapshot.Discord)
                .WithSlackId(snapshot.Slack)
                .WithEmail(snapshot.Email)
                
            State <- snapshot.State
            self.Watch Flagged
            printfn "::> Restore : %A" State
        with
            | _ -> ()

    member private _.RunEvery action (period : int)= 
        let rec loop () =
            async {
                let _ = action Flagged
                do! Async.Sleep (period * 1000)
                do! loop()
            }
        loop()

    member private _.CompareDiffs (oldState : Dictionary<_, _>) (newState : Dictionary<_, _>) eips =
        let loop eip = 
            let newHashExists = newState.ContainsKey eip
            let oldHashExists = oldState.ContainsKey eip
            if newHashExists = oldHashExists && oldHashExists = true 
            then 
                let mismatch = oldState[eip] <> newState[eip]
                oldState[eip] <- newState[eip]
                mismatch
            else 
                if newHashExists then 
                    oldState[eip] <- newState[eip]
                false

                
        eips |> Set.filter loop

    member public self.Watch (eips:int Set) = 
        Flagged <- Set.union eips Flagged
        self.HandleEips Flagged

    member public _.Unwatch eips = 
        Flagged <- Set.difference Flagged eips 
        eips |> Set.iter (fun eip -> ignore <| State.Remove(eip))
    
    member private self.GetEipMetadata eips : Dictionary<_, _> =
        let GetEipFileData = self.GetRequestWithAuth Config.GithubConfig.Token
        let newState = new Dictionary<int, string>()
        do eips |> Set.iter (fun eip -> newState[eip] <- (GetEipFileData  eip).["sha"].AsString())
        newState

    member private self.HandleEips eips= 
        let eipData = self.GetEipMetadata eips 
        let changedEips = self.CompareDiffs State eipData eips
        match changedEips with 
        | _ when Set.isEmpty changedEips -> ()
        | _ -> 
            let metadata = changedEips |> Set.map (fun eip -> Metadata.FetchMetadata eip 0)
            printf "Changed EIPs : %A\n::> " (metadata)
            let results = 
                let rec flatten flatres res = 
                    match res with 
                    | [] -> flatres
                    | Ok h::t -> flatten (h::flatres) t 
                    | Error e::t -> flatten flatres t 
                        
                metadata
                |> List.ofSeq
                |> flatten []


            match self.UserInstance.DiscordId with
            | Some(userId) -> 
                Metadata results
                |> Dependency.Discord.SendMessageAsync Config (Some userId)
                |> Async.StartImmediate
            | None -> ()
            
            match self.UserInstance.Email with
            | Some(email) -> 
                results
                |> Mail.NotifyEmail Config email
            | None -> ()

            match self.UserInstance.SlackId with
            | Some(username) -> 
                Metadata results
                |> Slack.SendMessageAsync Config (Some username)
                |> Async.StartImmediate
            | None -> ()



    member public self.Start period silosPath= 
        let thread = 
            new Thread(fun () -> 
                try 
                    self.ReadInFile silosPath
                    let actions = self.RunEvery (self.HandleEips) period
                    Async.RunSynchronously(actions, 0, CancellationToken.Token)
                with 
                | :? System.Exception -> printf "Stopped reading commands\n::> "
            )
        thread.Start()
