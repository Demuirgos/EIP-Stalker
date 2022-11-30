# EIP-Dependency
A simple app that reports EIP-Metadata

#Usage : 
* run using : ``dotnet run`` or ``dotnet  build`` 
### Eip metadata :
```
>> dotnet run --eip 3540 --depth 2
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
>> dotnet run --monitor 3540
>> Restore : map [(3540, "123456azert12345yuiop")]
   Update  : eips changed [3540]
```
