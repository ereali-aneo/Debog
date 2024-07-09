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

/// <summary>
/// Represents a result with its creation date, name, status, session ID, result ID, and optional data.
/// </summary>
public record Result
{
    public DateTime CreatedAt { get; set; }
    public string Name { get; set; }
    public ResultStatus Status { get; init; }
    public string SessionId { get; set; }
    public string ResultId { get; set; }
    public byte[]? Data { get; set; }
}

/// <summary>
/// Represents task data, including its data dependencies, expected output keys, payload ID, and task ID.
/// </summary>
public record TaskData
{
    public ICollection<string> DataDependencies { get; set; }
    public ICollection<string> ExpectedOutputKeys { get; set; }
    public string PayloadId { get; set; }
    public string TaskId { get; set; }
}

/// <summary>
/// A storage class to keep Tasks and Result data.
/// </summary>
internal class AgentStorage
{
    public readonly HashSet<string> _notifiedResults = new();
    public  ConcurrentDictionary<string, Result> _Results = new();
    public  ConcurrentDictionary<string, TaskData> _Tasks = new();
}

internal class MyAgent : Agent.AgentBase
{
    private readonly AgentStorage _storage;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="storage"></param>
    public MyAgent(AgentStorage storage)
    {
        _storage = storage;
    }

    /// <summary>
    /// Creates a Result with its MetaData: generates a result ID, retrieves its name, sets its status to created, adds the creation date, and retrieves the session ID.
    /// Registers the created result and its data in Results.
    /// </summary>
    /// <param name="request">Data related to the CreateResults request</param>
    /// <param name="context">Data related to the server</param>
    /// <returns>A response containing the created Result</returns>
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
                CreatedAt = DateTime.UtcNow,
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
                    CreatedAt = Timestamp.FromDateTime(result.CreatedAt),
                    Name = result.Name,
                    SessionId = result.SessionId,
                    Status = result.Status,
                    ResultId = result.ResultId,
                })
            }
        });
    }

    /// <summary>
    /// Creates Result MetaData: generates a result ID, retrieves its name, sets its status to created, adds the creation date, and retrieves the session ID.
    /// Registers the created result metadata without any data in Results.
    /// </summary>
    /// <param name="request">Data related to CreateResultsMetaData the request</param>
    /// <param name="context">Data related to the server</param>
    /// <returns>A response containing the created Result MetaData</returns>
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
                CreatedAt = DateTime.UtcNow,
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
                    CreatedAt = Timestamp.FromDateTime(result.CreatedAt),
                    Name = result.Name,
                    SessionId = result.SessionId,
                    Status = result.Status,
                    ResultId = result.ResultId,
                })
            }
        });
    }

    /// <summary>
    /// Notifies result data: adds result IDs from the request to the notified results list in storage.
    /// </summary>
    /// <param name="request">Data related to the NotifyResultData request</param>
    /// <param name="context">Data related to the server</param>
    /// <returns>A response containing the notified result IDs</returns>
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

    /// <summary>
    /// Submits tasks: generates task IDs, retrieves their data dependencies, expected output keys, and payload IDs.
    /// Registers the created tasks in Tasks.
    /// </summary>
    /// <param name="request">Data related to the  SubmitTasks request</param>
    /// <param name="context">Data related to the server</param>
    /// <returns>A response containing information about the submitted tasks</returns>
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

    /// <summary>
    /// 
    /// </summary>
    /// <param name="socket"></param>
    /// <param name="storage"></param>
    /// <param name="loggerConfiguration"></param>
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

    /// <summary>
    /// Disposes resources used by the application, stopping the application and waiting for it to finish.
    /// </summary>
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
    /// <summary>
    /// 
    /// </summary>
    /// <param name="arg"></param>
    public static void Main(string[] arg)
    {
        // Create a logger configuration to write output to the console with contextual information.
        var loggerConfiguration_ = new LoggerConfiguration()
            .WriteTo.Console()
            .Enrich.FromLogContext()
            .CreateLogger();

        // Create a logger using the configured logger settings
        var logger_ = LoggerFactory.Create(builder => builder.AddSerilog(loggerConfiguration_))
            .CreateLogger("root");

        // Set up the gRPC channel with the specified address and a null logger for the provider
        var channel = new GrpcChannelProvider(new GrpcChannel { Address = "/tmp/worker.sock" },
            new NullLogger<GrpcChannelProvider>()).Get();

        // Create a gRPC client for the Worker service
        var client = new Worker.WorkerClient(channel);

        // Generate a unique identifier for the payload
        var payloadId = Guid.NewGuid().ToString();

        // Generate a unique identifier for the task
        var taskId = Guid.NewGuid().ToString();

        // Generate a unique identifier for the communication token
        var token = Guid.NewGuid().ToString();

        // Generate a unique identifier for the session
        var sessionId = Guid.NewGuid().ToString();

        // Generate a unique identifier for the first data dependency
        var dd1 = Guid.NewGuid().ToString();

        // Generate a unique identifier for the first expected output key
        var eok1 = Guid.NewGuid().ToString();

        // Generate a unique identifier for the second expected output key
        var eok2 = Guid.NewGuid().ToString();

        // Create a temporary directory and get its full path
        var folder = Directory.CreateTempSubdirectory().FullName;

        // Convert the integer 8 to a byte array for the payload
        var payloadBytes = BitConverter.GetBytes(8);

        // Convert the string "DataDependency1" to a byte array using ASCII encoding
        var dd1Bytes = Encoding.ASCII.GetBytes("DataDependency1");

        // Write payloadBytes in the corresponding file
        File.WriteAllBytesAsync(Path.Combine(folder,
                payloadId),
            payloadBytes);

        // Write payloadBytes in the corresponding file
        File.WriteAllBytesAsync(Path.Combine(folder,
                dd1),
            dd1Bytes);
        // Create an AgentStorage to keep the Agent Data After Process
        var storage = new AgentStorage();
        
        // Rerun a task scope
        {

            using var server = new Server("/tmp/agent.sock", storage, loggerConfiguration_);

            // To test subtasking partition
            var taskOptions = new TaskOptions();
            taskOptions.Options["UseCase"] = "Launch";

            // Call the Process method on the gRPC client `client` of type Worker.WorkerClient
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
                DataFolder = folder,
                ExpectedOutputKeys =
                {
                    eok1,
                    //eok2, // Uncomment to test multiple expected output keys (results)
                },
                TaskId = taskId,
                TaskOptions = taskOptions
            });
        }

        // print everything in agent storage

        logger_.LogInformation("resultsIds : {results}", storage._notifiedResults);
        foreach (var result in storage._notifiedResults)
        {
            var str = File.ReadAllBytes(Path.Combine(folder,
                result));
            logger_.LogInformation("Notify Result Data : {str}", str);
        }
        logger_.LogInformation("results : {results}", storage._Results);
        foreach (var result in storage._Results)
        {
            var str = result.Value.Data;
            logger_.LogInformation("Create Result Data : {str}", str);
        }
        logger_.LogInformation("Tasks Data : {results}", storage._Tasks);

        var i = 0;
        foreach (var result in storage._notifiedResults)
        {
            var byteArray = File.ReadAllBytes(Path.Combine(folder,
                result));
            logger_.LogInformation("result{i}: {res}", i, byteArray);
            logger_.LogInformation("resultId{i}: {res}", i, result);
            i++;
        }
    }
}