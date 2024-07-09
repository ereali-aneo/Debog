using ArmoniK.Task.ReRunner.MyAgent;
using System;
using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Core;

namespace Task.ReRunner.Server
{
    internal class Server : IDisposable
    {
        private readonly System.Threading.Tasks.Task _runningApp;
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
}