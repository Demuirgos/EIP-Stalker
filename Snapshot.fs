module Dependency.Snapshot

open System
open System.Collections.Generic

type Snapshot = {
    Email: string option
    Discord: uint64 option
    Slack: string option

    State: Dictionary<int, string>
    Flagged: int Set
    Period: int
}

let public GetSnapshotFromFile filePath = 
    try 
        let fileContent = System.IO.File.ReadAllText(filePath)
        let configs = System.Text.Json.JsonSerializer.Deserialize<Snapshot>(fileContent)
        Some (configs)
    with 
    | ex -> None
