using System;
using System.Collections.Concurrent;
using ArmoniK.Task.ReRunner.MyAgent;
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
using ArmoniK.Task.ReRunner.Utils;
using Google.Protobuf.Collections;
using Grpc.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Configuration;
using Google.Protobuf.WellKnownTypes;
using Grpc.AspNetCore.Server;
using Serilog;
using Serilog.Core;
using Serilog.Formatting.Compact;
using Task.ReRunner.Server;


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


        // Scope for the Task to run 
        {
            using var server = new Server("/tmp/agent.sock", storage, loggerConfiguration_);

            // To test subtasking partition
            var taskOptions = new TaskOptions();
            taskOptions.Options["UseCase"] = "Launch";
            var configuration = new Configuration
            {
                DataChunkMaxSize = 84,
            };

            // Register the parameters needed for processing : 
            // communication token, payload and session IDs, configuration settings, data dependencies, folder location, expected output keys, task ID, and task options.
            var toProcess = new ProcessData
            {
                CommunicationToken = token,
                PayloadId = payloadId,
                SessionId = sessionId,
                Configuration = configuration,
                DataDependencies = new RepeatedField<string> { dd1 },
                DataFolder = folder,
                ExpectedOutputKeys = new RepeatedField<string> { eok1 },
                TaskId = taskId,
                TaskOptions = taskOptions
            };

            // Call the Process method on the gRPC client `client` of type Worker.WorkerClient
            client.Process(new ProcessRequest
            {
                CommunicationToken = token,
                PayloadId = payloadId,
                SessionId = sessionId,
                Configuration = configuration,
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

            logger_.LogInformation("First Task Data: {toProcess}", toProcess);
        }

        // print everything in agent storage

        logger_.LogInformation("resultsIds : {results}", storage._notifiedResults);

        var i = 0;
        foreach (var result in storage._notifiedResults)
        {
            var str = File.ReadAllBytes(Path.Combine(folder,
                result));
            logger_.LogInformation("Notified result Data : {str}", str);
            logger_.LogInformation("Notified result Id{i}: {res}", i, result);
            i++;
        }

        logger_.LogInformation("results : {results}", storage._Results);
        foreach (var result in storage._Results)
        {
            var str = result.Value.Data;
            logger_.LogInformation("Create Result Data : {str}", str);
        }

        logger_.LogInformation("Submitted Tasks Data : {results}", storage._Tasks);
    }
}