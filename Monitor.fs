module Dependency.Monitor


open System.IO
open FSharp.Data
open System.Net.Http
open System.Text.Json
open System.Net.Http.Headers
open System.Threading

let mutable State : Map<int, string> = Map.empty
let CancellationToken = new CancellationTokenSource()
let TemporaryFilePath = Path.Combine(System.Environment.CurrentDirectory, "eips.json")

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
        let newHash = newState.[eip] 
        let oldHash = oldState.[eip]
        if newHash <> oldHash then 
            Some eip
        else
            None
    eips |> List.map loop |> List.choose id

let public Start eips= 
    ReadInFile()
    let GetEipFileData = GetRequestWithAuth "[<Github Api Key>]"
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
                printfn "Changed EIPs : %A" changedEips
                printfn "Updated : %A" State
                State <- eipData
                
        ) 5 eips
    Async.RunSynchronously(actions, 0, CancellationToken.Token)

let public Stop args = 
    CancellationToken.Cancel()
    SaveInFile()