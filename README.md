# SerialNumberFetcher
`SerialNumberFetcher` is a C# class designed to fetch the BIOS serial numbers of remote computers asynchronously in parallel using `PsExec`.

## Features
- Asynchronous execution of commands.
- Parallel processing with a limit on the maximum number of concurrent tasks.
- Command timeout and retry mechanism.
- Logging of attempts and errors.

## Usage
### Class Definition
The `SerialNumberFetcher` class includes the following methods:

- **FetchSerialNumbersAsync**: This method fetches the serial numbers for a collection of computers.
- **FetchSerialNumberWithRetryAsync**: This method attempts to fetch the serial number with retries.
- **GetSerialNumberAsync**: This method runs the `PsExec` command to get the serial number.
- **ExecuteCommandAsync**: This method executes the command and captures the output.
- **ParseSerialNumber**: This method parses the output to extract the serial number.

### Example
Here is an example of how to use the `SerialNumberFetcher` class within a larger program:

```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public static async Task Foo()
{
    var computers = new List<Computer>
    {
        new Computer { Name = "Computer1" },
        new Computer { Name = "Computer2" },
        // Add more computers as needed
    };

    var fetcher = new SerialNumberFetcher();

    await fetcher.FetchSerialNumbersAsync(computers, 
        computer => computer.Name, 
        (computer, serial) => computer.SerialNumber = serial);

    foreach (var computer in computers)
    {
        Console.WriteLine($"Name: {computer.Name}, Serial Number: {computer.SerialNumber}");
    }
}

public class Computer
{
    public string Name { get; set; }
    public string SerialNumber { get; set; }
}
```
