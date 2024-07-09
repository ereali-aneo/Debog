using ArmoniK.Api.gRPC.V1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArmoniK.Task.ReRunner.Utils
{
    /// <summary>
    /// Represents all the parameters needed to launch a process : communication token, payload and session IDs, configuration settings, data dependencies, folder location, expected output keys, task ID, and task options.
    /// </summary>
    public record ProcessData
    {
        public string CommunicationToken { get; init; }
        public string PayloadId { get; init; }
        public string SessionId { get; init; }
        public Configuration Configuration { get; init; }
        public ICollection<string> DataDependencies { get; init; }
        public string DataFolder { get; init; }
        public ICollection<string> ExpectedOutputKeys { get; init; }
        public string TaskId { get; init; }
        public TaskOptions TaskOptions { get; init; }
    }


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
}