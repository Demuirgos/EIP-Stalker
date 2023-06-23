# EIP-Dependency
A simple app that reports EIP-Metadata

#Usage : 
 * run using : ``dotnet run ConfigArgs`` 
 * ConfigArgs ``Usage: (--period <duration>)? (--notify <email>)? --configs <ConfigJson path>``
 * ConfigJson Schema ```json {
        Server: smtpServer?
        Sender: email?
        Port: int?
        Password: alphanum?
        GitToken: alphanum
    }```
 * Opt-in/out of an Eip watch: ``Watch|Unwatch eipNumbers+``
### Eip change monitor :
```
>> dotnet run --monitor 3540 --period 60
>> Restore : map [(3540, "fc364983e4b04dffa52760f390e07a18ff95")]
   Update  : eips changed [Ok
       { Author =
          ["Alex Beregszaszi"; "Pawel Bylica"; "Andrei Maiboroda"; "Alexey Akhunov";
           "Christian Reitwiessner"; "Martin Swende"]
         Status = "Final "
         Type = "Standards Track"
         Category = "Core"
         Created = "2021-03-16"
         Require = None
         Discussion =
          Some "https://ethereum-magicians.org/t/evm-object-format-eof/5727" }]
```
