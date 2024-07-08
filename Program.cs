using System;
using System.Collections.Concurrent;
using ArmoniK.Debogage;
using ArmoniK.Api.Common.Channel.Utils;
using Microsoft.Extensions.Logging.Abstractions;
using GrpcChannel = ArmoniK.Api.Common.Options.GrpcChannel;
using ArmoniK.Api.gRPC.V1.Worker;
using ArmoniK.Api.gRPC.V1;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using ArmoniK.Api.gRPC.V1.Agent;
using Grpc.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Configuration;
using Google.Protobuf.WellKnownTypes;
using Serilog;
using Serilog.Core;
using Serilog.Formatting.Compact;


public record Result
{
    public DateTime CreateAt { get; set; }
    public string Name { get; set; }
    public ResultStatus Status { get; init; }
    public string SessionId { get; set; }
    public string ResultId { get; set; }
    public byte[]? Data { get; set; }
}
public record TaskData
{
    public ICollection<string> DataDependencies { get; set; }
    public ICollection<string> ExpectedOutputKeys { get; set; }
    public string PayloadId { get; set; }
    public string TaskId { get; set; }
}
internal class AgentStorage
{
    public readonly HashSet<string> _notifiedResults = new();
    public  ConcurrentDictionary<string, Result> _Results = new();
    public  ConcurrentDictionary<string, TaskData> _Tasks = new();
}

internal class MyAgent : Agent.AgentBase
{
    private readonly AgentStorage _storage;

    public MyAgent(AgentStorage storage)
    {
        _storage = storage;
    }

    public override Task<CreateResultsResponse> CreateResults(CreateResultsRequest request, ServerCallContext context)
    {
        Console.ForegroundColor = ConsoleColor.Blue;
        Console.WriteLine("[INFO] Entered in CreateResults");
        Console.ResetColor();

        var results = request.Results.Select(rc =>
        {
            var resultId = Guid.NewGuid().ToString();
            var current = new Result
            {
                ResultId = resultId,
                Name = rc.Name,
                Status = ResultStatus.Created,
                CreateAt = DateTime.UtcNow,
                SessionId = request.SessionId,
                Data = rc.Data.ToByteArray(),
            };
            _storage._Results[resultId] = current;
            return current;
        });

        return Task.FromResult(new CreateResultsResponse
        {
            CommunicationToken = request.CommunicationToken,
            Results =
            {
                results.Select(result => new ResultMetaData
                {
                    CreatedAt = Timestamp.FromDateTime(result.CreateAt),
                    Name = result.Name,
                    SessionId = result.SessionId,
                    Status = result.Status,
                    ResultId = result.ResultId,
                })
            }
        });
    }

    public override Task<CreateResultsMetaDataResponse> CreateResultsMetaData(CreateResultsMetaDataRequest request,
        ServerCallContext context)
    {
        Console.ForegroundColor = ConsoleColor.Blue;
        Console.WriteLine("[INFO] Entered in CreateResultsMetaData");
        Console.ResetColor();


        var results = request.Results.Select(rc =>
        {
            var resultId = Guid.NewGuid().ToString();

            return new Result
            {
                ResultId = resultId,
                Name = rc.Name,
                Status = ResultStatus.Created,
                CreateAt = DateTime.UtcNow,
                SessionId = request.SessionId,
                Data = null,
            };
        });

        return Task.FromResult(new CreateResultsMetaDataResponse
        {
            CommunicationToken = request.CommunicationToken,
            Results =
            {
                results.Select(result => new ResultMetaData
                {
                    CreatedAt = Timestamp.FromDateTime(result.CreateAt),
                    Name = result.Name,
                    SessionId = result.SessionId,
                    Status = result.Status,
                    ResultId = result.ResultId,
                })
            }
        });
    }


    public override Task<NotifyResultDataResponse> NotifyResultData(NotifyResultDataRequest request,
        ServerCallContext context)
    {
        Console.ForegroundColor = ConsoleColor.Blue;
        Console.WriteLine("[INFO] Entered in NotifyResultData");
        Console.ResetColor();
        foreach (var result in request.Ids)
        {
            _storage._notifiedResults.Add(result.ResultId);
        }

        return Task.FromResult(new NotifyResultDataResponse
        {
            ResultIds =
            {
                request.Ids.Select(identifier => identifier.ResultId)
            }
        });
    }

    public override Task<SubmitTasksResponse> SubmitTasks(SubmitTasksRequest request, ServerCallContext context)
    {
        Console.ForegroundColor = ConsoleColor.Blue;
        Console.WriteLine("[INFO] Entered in SubmitTasks");
        Console.ResetColor();

        var createdTasks = request.TaskCreations.Select(rc =>
        {
            var taskId = Guid.NewGuid().ToString();
            var current = new TaskData
            {
                DataDependencies = rc.DataDependencies,
                ExpectedOutputKeys = rc.ExpectedOutputKeys,
                PayloadId = rc.PayloadId,
                TaskId = taskId,
            };
            _storage._Tasks[taskId] = current;
            return current;
        });
        return Task.FromResult(new SubmitTasksResponse
        {
            CommunicationToken = request.CommunicationToken,
            TaskInfos =
            {
                createdTasks.Select(creationRequest => new SubmitTasksResponse.Types.TaskInfo
                {
                    DataDependencies =
                    {
                        creationRequest.DataDependencies,
                    },
                    ExpectedOutputIds =
                    {
                        creationRequest.ExpectedOutputKeys,
                    },
                    PayloadId = creationRequest.PayloadId,
                    TaskId    = creationRequest.TaskId,
                }),
            },
        });
    }
}

internal class Server : IDisposable
{
    private readonly Task _runningApp;
    private readonly WebApplication _app;

    public Server(string socket, AgentStorage storage, Logger loggerConfiguration)
    {
        var builder = WebApplication.CreateBuilder();

        builder.WebHost.ConfigureKestrel(options => options.ListenUnixSocket(socket,
            listenOptions =>
            {
                if (File.Exists(socket))
                {
                    File.Delete(socket);
                }

                listenOptions.Protocols = HttpProtocols.Http2;
            }));

        builder.Host.UseSerilog(loggerConfiguration);

        builder.Services
            .AddSingleton(storage)
            .AddGrpcReflection()
            .AddGrpc(options => options.MaxReceiveMessageSize = null);

        _app = builder.Build();

        if (_app.Environment.IsDevelopment())
        {
            _app.UseDeveloperExceptionPage();
            _app.MapGrpcReflectionService();
        }

        _app.UseRouting();
        _app.MapGrpcService<MyAgent>();
        _runningApp = _app.RunAsync();
    }

    public void Dispose()
    {
        _app.StopAsync().Wait();
        _runningApp.Wait();
    }
}


/// afficher appel a l'agent
/// j'envoie du bullshit en argument pour le moment
/// je ne fait rien de mon resultat pour le moment
/// mes signaux ne sont pas bloquant
///  lire \\wsl.localhost\Ubuntu-22.04\home\lara\ArmoniK.Core\Common\tests\Pollster\AgentTest.cs
internal static class Program
{
    public static void Main(string[] arg)
    {
        var loggerConfiguration_ = new LoggerConfiguration()
            .WriteTo.Console()
            .Enrich.FromLogContext()
            .CreateLogger();

        var logger_ = LoggerFactory.Create(builder => builder.AddSerilog(loggerConfiguration_))
            .CreateLogger("root");

        /// channel as a client to my worker 
        var channel = new GrpcChannelProvider(new GrpcChannel { Address = "/tmp/worker.sock" },
            new NullLogger<GrpcChannelProvider>()).Get();
        var client = new Worker.WorkerClient(channel);

        var payloadId = Guid.NewGuid()
            .ToString();
        var taskId = Guid.NewGuid()
            .ToString();
        var token = Guid.NewGuid()
            .ToString();
        var sessionId = Guid.NewGuid()
            .ToString();
        var dd1 = Guid.NewGuid()
            .ToString();
        var eok1 = Guid.NewGuid()
            .ToString();
        var eok2 = Guid.NewGuid()
            .ToString();

        var Dir = Directory.CreateTempSubdirectory().FullName;
        var payloadBytes = BitConverter.GetBytes(8);
        //var payloadBytes = Encoding.ASCII.GetBytes("Hello");
        var dd1Bytes = Encoding.ASCII.GetBytes("DataDependency1");


        File.WriteAllBytesAsync(Path.Combine(Dir,
                payloadId),
            payloadBytes);
        File.WriteAllBytesAsync(Path.Combine(Dir,
                dd1),
            dd1Bytes);


        var storage = new AgentStorage();
        {
            using var server = new Server("/tmp/agent.sock", storage, loggerConfiguration_);

            var taskOptions = new TaskOptions();
            taskOptions.Options["UseCase"] = "Launch";

            client.Process(new ProcessRequest
            {
                CommunicationToken = token,
                PayloadId = payloadId,
                SessionId = sessionId,
                Configuration = new Configuration
                {
                    DataChunkMaxSize = 84,
                },
                DataDependencies =
                {
                    dd1,
                },
                DataFolder = Dir,
                ExpectedOutputKeys =
                {
                    eok1,
                    //eok2, // a commenter en fonction de multiple result 
                },
                TaskId = taskId,
                TaskOptions = taskOptions
            });
        }

        logger_.LogInformation("resultsIds : {results}", storage._notifiedResults);
        logger_.LogInformation("results : {results}", storage._Results);
                foreach (var result in storage._Results)
        {
            var str = Encoding.ASCII.GetString(result.Value.Data);
            logger_.LogInformation("Result Data : ", str);
        }
        logger_.LogInformation("Tasks Data : {results}", storage._Tasks);

        //Console.WriteLine(storage._Results.Select(i => i.ResultId));

        var i = 0;
        foreach (var result in storage._notifiedResults)
        {
            var stringArray = Encoding.ASCII.GetString(File.ReadAllBytes(Path.Combine(Dir,
                    result)))
                .Split(new[]
                    {
                        '\n',
                    },
                    StringSplitOptions.RemoveEmptyEntries);
            foreach (var res in stringArray)
            {
                logger_.LogInformation("result{i}: {res}", i, res);
            }

            logger_.LogInformation("resultId{i}: {res}", i, result);
            i++;
        }


    }
    //WorkerServer.Create<>() // faux car utilise celui deja creer 
}
// test agents tromper d'agent

/*

     private class MyAgent : Agent.AgentClient
     {
         private readonly MyClientStreamWriter<CreateTaskRequest> taskStream_ = new();


         public override AsyncClientStreamingCall<CreateTaskRequest, CreateTaskReply> CreateTask(Metadata headers = null,
             DateTime? deadline = null,
             CancellationToken cancellationToken = default)
             => new(taskStream_,
                 Task.FromResult(new CreateTaskReply()),
                 Task.FromResult(new Metadata()),
                 () => Status.DefaultSuccess,
                 () => new Metadata(),
                 () =>
                 {
                 });

     }
     public async Task NewTaskHandlerShouldSucceed()
     {
         var agent = new MyAgent();

     var payloadId = Guid.NewGuid()
                         .ToString();
     var taskId = Guid.NewGuid()
                      .ToString();
     var token = Guid.NewGuid()
                     .ToString();
     var sessionId = Guid.NewGuid()
                         .ToString();
     var dd1 = Guid.NewGuid()
                   .ToString();
     var eok1 = Guid.NewGuid()
                    .ToString();

     var folder = Path.Combine(Path.GetTempPath(),
                               token);

     Directory.CreateDirectory(folder);

     var payloadBytes = Encoding.ASCII.GetBytes("payload");
     var dd1Bytes = Encoding.ASCII.GetBytes("DataDependency1");
     var eok1Bytes = Encoding.ASCII.GetBytes("ExpectedOutput1");


     var handler = new TaskHandler(new ProcessRequest
     {
         CommunicationToken = token,
         DataFolder = folder,
         PayloadId = payloadId,
         SessionId = sessionId,
         Configuration = new Configuration
         {
             DataChunkMaxSize = 84,
         },
         DataDependencies =
                                     {
                                       dd1,
                                     },
         ExpectedOutputKeys =
                                     {
                                       eok1,
                                     },
         TaskId = taskId,
     },
                                   agent,
                                   new LoggerFactory(),
                                   CancellationToken.None);

     Assert.ThrowsAsync<NotImplementedException>(() => handler.SendResult(eok1,
                                                                          eok1Bytes));

     Assert.Multiple(() =>
                     {
                       Assert.AreEqual(payloadBytes,
                                       handler.Payload);
                       Assert.AreEqual(sessionId,
                                       handler.SessionId);
                       Assert.AreEqual(taskId,
                                       handler.TaskId);
                       Assert.AreEqual(dd1Bytes,
                                       handler.DataDependencies[dd1]);
                       Assert.AreEqual(eok1Bytes,
                                       File.ReadAllBytes(Path.Combine(folder,
                                                                      eok1)));
                     });
   }

*/