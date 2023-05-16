module Dependency.Monitor

open System.IO
open FSharp.Data
open System.Net.Http
open System.Text.Json
open System.Net.Http.Headers
open System.Threading
open Dependency.Mail
open Dependency.Core

let mutable State : Map<int, string> = Map.empty
let CancellationToken = new CancellationTokenSource()
let TemporaryFilePath = Path.Combine(System.Environment.CurrentDirectory, "eips.json")
let GithubToken : string= failwith "Please provide a Github token"

let private GetRequestWithAuth (key:string) eip =
    async {
        let client = new HttpClient()
        client.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json")
        client.DefaultRequestHeaders.Authorization <- AuthenticationHeaderValue("token", key)
        client.DefaultRequestHeaders.UserAgent.Add(ProductInfoHeaderValue("FSharp", "6.0"))
        let! response =  client.GetAsync(sprintf "https://api.github.com/repos/ethereum/EIPs/contents/EIPS/eip-%i.md" eip) |> Async.AwaitTask
        return! response.Content.ReadAsStringAsync() |> Async.AwaitTask
    } |> Async.RunSynchronously
    |> JsonValue.Parse

let private SaveInFile () =
    try
        printfn "Save : %A" State
        let json = JsonSerializer.Serialize(State)
        File.WriteAllText(TemporaryFilePath, json)
    with
        | _ -> ()

let private NotifyEmail config email eipnum (eip : Metadata) =
    let subject = sprintf "EIP %i has been updated" eipnum
    let body = sprintf "EIP %i has been updated :\n %A" eipnum eip
    sendMailMessage config email subject body ()

let private ReadInFile () =
    try
        let json = File.ReadAllText(TemporaryFilePath)
        State <- JsonSerializer.Deserialize<Map<int, string>>(json)
        printfn "Restore : %A" State
    with
        | _ -> ()

let private RunEvery action (period : int) args= 
    let rec loop () =
        async {
            let _ = action args
            do! Async.Sleep (period * 1000)
            do! loop()
        }
    loop()

let private CompareDiffs (oldState : Map<int, string>) (newState : Map<int, string>) eips =
    let loop eip = 
        let newHash = Map.tryFind eip newState
        let oldHash = Map.tryFind eip oldState
        match newHash, oldHash with
        | Some newHash, Some oldHash when newHash <> oldHash -> 
            Some eip
        | Some _, None | None, Some _ -> Some eip
        | _ -> None
    eips |> List.map loop |> List.choose id

let public Start eips period emailConfigs= 
    ReadInFile()
    let GetEipFileData = GetRequestWithAuth GithubToken
    let actions = 
        RunEvery (fun _ -> 
            let eipData = 
                eips 
                |> List.map (fun eip -> eip,  (GetEipFileData  eip).["sha"].AsString())
                |> Map.ofList
            let changedEips = CompareDiffs State eipData eips
            match changedEips with 
            | [] -> ()
            | _ -> 
                printfn "Changed EIPs : %A" (changedEips |> List.map (fun eip -> Metadata.FetchMetadata eip 0))
                match emailConfigs with
                | (Some email, Some config) -> 
                    changedEips 
                    |> List.map (fun eip -> eip, Metadata.FetchMetadata eip 0)
                    |> List.iter (fun (eip, eipData) -> 
                        match eipData with 
                        | Ok data -> 
                            NotifyEmail config email eip data
                        | _ -> ())
                | _ -> ()
                State <- eipData
        ) period eips
    Async.RunSynchronously(actions, 0, CancellationToken.Token)

let public Stop args = 
    CancellationToken.Cancel()
    SaveInFile()