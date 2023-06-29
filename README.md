# EIP-Stalker
A simple app that monitors Eip changes

#Usage : 
 * run using : ``dotnet run ConfigArgs`` 
 * ConfigArgs ``Usage: --configs <ConfigJson path>``
 * ConfigJson Schema
   ```json
      Config = {
          GithubConfig: {
            Token: string
         }
          EmailConfig: {
            Include: bool 
            Server: string | null
            Sender: string | null
            Password: string | null
            Port: int | null
            EnableSsl : bool | null
         }
          DiscordConfig: {
            Include: bool
            Channel: UInt64 | null
            Token: string | null
         }
          SlackConfig: {
            Include: bool
            Channel: string | null
            ApiToken: string | null
            AppToken: string | null
         }
      }
    ```
### Eip change monitor :
```
>> setup --period (period:/d+) [--refUser (id:guid)]
::? Sets up a user monitor with the [period] polling time, if the refUser is provided it will try to attach the local app Id [Discord|Slack] to the id (the refUser)
<< Discord account hooked with Id : [id:guid](a guid)
```
```
>> watch (eipNumber:/d+)+
::? Adds eips in args to the watch list
<< Started Watching : [eipNumber+]
```
```
>> unwatch (eipNumber:/d+)+
::? Removes eips in args to the watch list
<< Stopped  Watching : [eipNumber+]
```
```
>> watching?
::? lists eips currently being watched 
<< Currently Watching : : [eipNumber+]
```
```
>> notify (email:/w+)
::? hooks email to receive change updates as well
<< Email email notifications activated
```
```
>> ignore "emaill"
::? remove  email hook
<< Email notifications deactivated
```
