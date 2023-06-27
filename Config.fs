module Dependency.Config

open System

type DiscordConfig = {
    Include: bool
    Channel: UInt64
    Token: string
}

type SlackConfig = {
    Include: bool
    Channel: string
    ApiToken: string
    AppToken: string
}

type GithubConfig = {
    Token: string
}

type SMTPConfig = {
    Include: bool
    Server: string
    Sender: string
    Password: string
    Port: int
    EnableSsl : bool
}

type Config = {
    GithubConfig: GithubConfig
    EmailConfig: SMTPConfig
    DiscordConfig: DiscordConfig
    SlackConfig: SlackConfig
}

let public GetConfigFromFile filePath = 
    try 
        let fileContent = System.IO.File.ReadAllText(filePath)
        let configs = System.Text.Json.JsonSerializer.Deserialize<Config>(fileContent)
        Some (configs)
    with 
    | ex -> None
