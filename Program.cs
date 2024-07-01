using System;
using ArmoniK.Debogage;

using ArmoniK.Api.Worker.Utils;
using ArmoniK.Api.Common.Channel.Utils;
using Microsoft.Extensions.Logging.Abstractions;
using GrpcChannel = ArmoniK.Api.Common.Options.GrpcChannel;
using ArmoniK.Api.gRPC.V1.Worker;
using ArmoniK.Api.gRPC.V1;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

using Agent = ArmoniK.Api.gRPC.V1.Agent.Agent;

/// POur tester use HelloWorld
/// creer channel avec provider et non factoriel
/// afficher appel a l'agent
/// mon programme ne check pas les subtask pour le moment 
/// j'envoie du bullshit en argument pour le moment
/// je ne fait rien de mon resultat pour le moment
/// j'ai 2 channel afin que le worker et l'agent soit clients et server
/// mes signaux ne sont pas bloquant
/// regarder Api agent service
/// regarder api worker service
/// le programme ne fonctionne pas
internal static class Program
{
    public static void Main(string[] arg)
    {
        Console.WriteLine("Hello, World!");
        /// channel as a client to my worker 
        var server = new GrpcChannelProvider(new GrpcChannel(), new NullLogger<GrpcChannelProvider>());
        var client = new GrpcChannelProvider(new GrpcChannel(), new NullLogger<GrpcChannelProvider>());
        Agent.SubmitTasks();
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

         await File.WriteAllBytesAsync(Path.Combine(folder,
                                                    payloadId),
                                       payloadBytes);
         await File.WriteAllBytesAsync(Path.Combine(folder,
                                                    dd1),
                                       dd1Bytes);

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