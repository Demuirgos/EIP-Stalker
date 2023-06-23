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
let mutable Flagged : int Set = Set.empty

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

let private NotifyEmail config email (eip : Metadata list) =
    let subject = sprintf "Some EIPs have been updated"
    let link = sprintf "https://eips.ethereum.org/EIPS/eip-%d"
    let body = sprintf "
        <table>
          <tr>
            <td>Number</td>
            <td>Created</td>
            <td>Discussion</td>
            <td>Link</td>
          </tr>
          %s
        </table>" (eip |> List.map (fun metadata -> sprintf "<tr><td>%A</td><td>%A</td><td>%A</td><td>%A</td></tr>" metadata.Number metadata.Created metadata.Discussion (link metadata.Number))
                       |> List.fold (fun acc curr -> sprintf "%s\n%s" acc curr) System.String.Empty)
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
            true
        | _ -> false
    eips |> Set.filter loop
    
let public Watch eip = 
    Flagged <- Set.add eip Flagged

let public Unwatch eip = 
    Flagged <- Set.remove eip Flagged

let public Start period email configs = 
    ReadInFile()
    let GetEipFileData = GetRequestWithAuth configs.GitToken
    let actions = 
        RunEvery (fun _ -> 
            let eipData = 
                Flagged 
                |> Set.map (fun eip -> eip,  (GetEipFileData  eip).["sha"].AsString())
                |> Map.ofSeq
            let changedEips = CompareDiffs State eipData Flagged
            State <- eipData
            match changedEips with 
            | _ when Set.isEmpty changedEips -> ()
            | _ -> 
                printfn "Changed EIPs : %A" (changedEips |> Set.map (fun eip -> Metadata.FetchMetadata eip 0))
                match email with
                | Some email -> 
                    let results = 
                        let rec flatten flatres res = 
                            match res with 
                            | [] -> flatres
                            | Ok h::t -> flatten (h::flatres) t 
                            | Error e::t -> flatten flatres t 
                        changedEips 
                        |> Set.map (fun eip -> Metadata.FetchMetadata eip 0)
                        |> List.ofSeq
                        |> flatten []

                    results
                    |> NotifyEmail configs email
                | _ -> ()
        ) period Flagged
    Async.RunSynchronously(actions, 0, CancellationToken.Token)

let public Stop args = 
    SaveInFile()
    exit(0)