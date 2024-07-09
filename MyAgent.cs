using ArmoniK.Api.gRPC.V1.Agent;
using ArmoniK.Api.gRPC.V1;
using System.Threading.Tasks;
using ArmoniK.Task.ReRunner.Utils;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ArmoniK.Task.ReRunner.MyAgent
{
    /// <summary>
    /// A storage class to keep Tasks and Result data.
    /// </summary>
    internal class AgentStorage
    {
        public readonly HashSet<string> _notifiedResults = new();
        public ConcurrentDictionary<string, Result> _Results = new();
        public ConcurrentDictionary<string, TaskData> _Tasks = new();
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
        public override Task<CreateResultsResponse> CreateResults(CreateResultsRequest request,
            ServerCallContext context)
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

            return System.Threading.Tasks.Task.FromResult(new CreateResultsResponse
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

            return System.Threading.Tasks.Task.FromResult(new CreateResultsMetaDataResponse
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

            return System.Threading.Tasks.Task.FromResult(new NotifyResultDataResponse
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
            return System.Threading.Tasks.Task.FromResult(new SubmitTasksResponse
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
                        TaskId = creationRequest.TaskId,
                    }),
                },
            });
        }
    }
}