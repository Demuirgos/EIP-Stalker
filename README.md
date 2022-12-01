# EIP-Dependency
A simple app that reports EIP-Metadata

#Usage : 
 * run using : ``dotnet run`` or ``dotnet  build`` 

 * cmdline args : 
   * get eip info :``--query <eip> (--depth <depth>)?``
   * monitor eip : ``--monitor <eip>+ (--period <duration>)? (--notify <email>)?``
### Eip metadata :
```
>> dotnet run --query 3540 --depth 2
>> Ok
  { Author = ["Alex Beregszaszi"; "Pawel Bylica"; "Andrei Maiboroda"]
    Status = "Review "
    Type = "Standards Track"
    Category = "Core"
    Created = "2021-03-16"
    Require = [3541; 3860; 170]
    Discussion =
     Some "https://ethereum-magicians.org/t/evm-object-format-eof/5727" 
  }
```
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
