module Dependency.Monitor

open System.IO
open FSharp.Data
open System.Net.Http
open System.Text.Json
open System.Net.Http.Headers
open System.Threading
open Dependency.Mail
open Dependency.Core

open System
open System.Collections.Generic

[<Class>]
type Monitor(recepient: string option, config: Config) = 
    let mutable State : Dictionary<int, string> = new Dictionary<int, string>()
    let mutable Flagged : int Set = Set.empty
    let mutable Config : Config = config
    let mutable Email : String option = recepient

    let CancellationToken = new CancellationTokenSource()
    let TemporaryFilePath = Path.Combine(System.Environment.CurrentDirectory, "eips.json")

    member public _.Current = (State, Flagged)

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

    member private _.SaveInFile () =
        try
            printf "Save : %A\n::> " State
            let json = JsonSerializer.Serialize(State)
            File.WriteAllText(TemporaryFilePath, json)
        with
            | _ -> ()


    member private self.ReadInFile () =
        try
            let json = File.ReadAllText(TemporaryFilePath)
            State <- JsonSerializer.Deserialize<Dictionary<int, string>>(json)
            self.Watch (Set.ofSeq State.Keys) 
            self.HandleEips (Set.ofSeq State.Keys)
            printf "Restore : %A\n::> " State
        with
            | _ -> ()

    member private _.RunEvery action (period : int) args= 
        let rec loop () =
            async {
                let _ = action args
                do! Async.Sleep (period * 1000)
                do! loop()
            }
        loop()

    member private _.CompareDiffs (oldState : Dictionary<int, string>) (newState : Dictionary<int, string>) eips =
        let loop eip = 
            let newHash = newState.ContainsKey eip
            let oldHash = oldState.ContainsKey eip
            if oldHash = newHash && oldHash = true 
            then oldState[eip] <> newState[eip]
            else false
        eips |> Set.filter loop

    member public self.Watch (eips:int Set) = 
        Flagged <- Set.union eips Flagged
        let newStateSegment = self.GetEipMetadata eips
        for kvp in newStateSegment do 
            State[kvp.Key] <- kvp.Value

    member public _.Unwatch eips = 
        Flagged <- Set.difference Flagged eips 
        eips |> Set.iter (fun eip -> ignore <| State.Remove(eip))
    
    member private self.GetEipMetadata eips : Dictionary<int, string> =
        let GetEipFileData = self.GetRequestWithAuth Config.GitToken
        let newState = new Dictionary<int, string>()
        do eips |> Set.iter (fun eip -> newState[eip] <- (GetEipFileData  eip).["sha"].AsString())
        newState

    member private self.HandleEips eips = 
        let eipData = self.GetEipMetadata eips 
        let changedEips = self.CompareDiffs State eipData eips
        State <- eipData
        match changedEips with 
        | _ when Set.isEmpty changedEips -> ()
        | _ -> 
            let metadata = changedEips |> Set.map (fun eip -> Metadata.FetchMetadata eip 0)
            printf "Changed EIPs : %A\n::> " (metadata)
            match Email with
            | Some email -> 
                let results = 
                    let rec flatten flatres res = 
                        match res with 
                        | [] -> flatres
                        | Ok h::t -> flatten (h::flatres) t 
                        | Error e::t -> flatten flatres t 
                        
                    metadata
                    |> List.ofSeq
                    |> flatten []

                results
                |> Mail.NotifyEmail Config email
            | _ -> ()

    member public self.Start period hooks= 
        self.ReadInFile()
        hooks |> List.iter (fun hook -> hook())
        let actions = self.RunEvery (self.HandleEips) period Flagged
        Async.RunSynchronously(actions, 0, CancellationToken.Token)

    member public self.Stop args = 
        self.SaveInFile()
        exit(0)