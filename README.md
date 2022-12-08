# HttpSysLaggyPerformance

Following description copied from https://github.com/grpc/grpc-dotnet/issues/1971

When simulating a laggy connection, server streaming is extremely slow when "larger" payloads are used ( > 4kb)
In my following samples, I have used [clumsy](https://jagt.github.io/clumsy/) to apply a 250ms Lag (Inbound and Outbound) and a 2% Drop (Inbound and Outbound) on a specific port that I use for my grpc connection (1080).

In [HttpContextSerializationContext.cs](https://github.com/grpc/grpc-dotnet/blob/master/src/Grpc.AspNetCore.Server/Internal/HttpContextSerializationContext.cs#L230) we find that grpcdotnet uses the .Write() extension method on the PipeWriter. This behaviour has been simulated in the project you can find [here](https://github.com/LaurensVergote/HttpSysLaggyPerformance).
When using this method, in conjunction with HttpSys and a laggy connection, performance takes an enormous hit.

Some samples recorded locally of requesting a 1MB payload:

| Description | Average speed (kbps) | Time Spent | 
| --- | --- | --- |
| WriteAsync() + HttpSys | 452 | 0:02 |
| Write() + HttpSys | 6,8 | 02:32 |
| WriteAsync() + Kestrel | 254 | 0:04 |
| Write() + Kestrel | 300 | 0:03 |

In order to get HttpSys to work you might have to run a netsh command:

`netsh http add urlacl url="http://+:1080/" user="DOMAIN\USER"`

You can toggle between using Kestrel and HttpSys by quoting the line `webBuilder.UseHttpSys();` in [Program.cs](https://github.com/LaurensVergote/HttpSysLaggyPerformance/blob/main/Server/Program.cs#L19)

Once the application is running you can use curl to access the API

`curl -o /dev/null 'http://localhost:1080/blobSync?writeSize=1M'`

`curl -o /dev/null 'http://localhost:1080/blobAsync?writeSize=1M'`

The difference should be obvious.
This sample uses localhost only (which exaggerates the problem a bit), but it should be easy enough to run the server and the curl command on different machines. The performance difference will still be very noticeable.

I do know that Kestrel is the preferred server for grpc, but due to some legacy on our side, we have to use HttpSys for the forseeable future.
Additionally, in [Startup.cs](https://github.com/LaurensVergote/HttpSysLaggyPerformance/blob/main/Server/Startup.cs#L15), you will find a boolean `private bool EnableHack = false;` which will activate a "hack" on the internal PipeWriter to increase its minimum buffer size using Reflection. While this does help with the performance, it is still not on par with what we can see when using PipeWriter.WriteAsync() instead.

I'm aware the sample code does not actively use grpc, but I believe this simulates it best and is easiest to follow.
