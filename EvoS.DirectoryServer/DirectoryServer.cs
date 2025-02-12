using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using EvoS.Framework.Constants.Enums;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Net;
using Newtonsoft.Json;
using EvoS.Framework.Logging;
using EvoS.Framework.DataAccess;

namespace EvoS.DirectoryServer
{
    public class Program
    {
        public static void Main(string[] args = null)
        {
            var host = WebHost.CreateDefaultBuilder()
                .SuppressStatusMessages(true)
                .UseKestrel(koptions => koptions.Listen(IPAddress.Parse("127.0.0.1"), 6050))
                .UseStartup<DirectoryServer>()
                .Build();

            Console.CancelKeyPress += async (sender, @event) =>
            {
                await host.StopAsync();
                host.Dispose();
            };

            host.Run();
        }
    }

    public class DirectoryServer
    {
        public void Configure(IApplicationBuilder app)
        {
            var serverAddressesFeature = app.ServerFeatures.Get<IServerAddressesFeature>();
            Log.Print(LogType.Server, "Started DirectoryServer on '0.0.0.0:6050'");

            app.Run((context) =>
            {
                context.Response.ContentType = "application/json";
                MemoryStream ms = new MemoryStream();
                context.Request.Body.CopyTo(ms);
                ms.Position = 0;
                string requestBody = new StreamReader(ms).ReadToEnd(); ;
                ms.Dispose();

                AssignGameClientRequest request = JsonConvert.DeserializeObject<AssignGameClientRequest>(requestBody);
                AssignGameClientResponse response = new AssignGameClientResponse();
                response.RequestId = request.RequestId;
                response.ResponseId = request.ResponseId;
                response.Success = true;
                response.ErrorMessage = "";

                PlayerData.Player p = PlayerData.GetPlayer(request.AuthInfo.Handle);
                if (p == null)
                {
                    Log.Print(LogType.Warning, $"Player {request.AuthInfo.Handle} doesnt exists");
                    PlayerData.CreatePlayer(request.AuthInfo.Handle);
                    p = PlayerData.GetPlayer(request.AuthInfo.Handle);
                    if (p != null)
                    {
                        Log.Print(LogType.Debug, $"Succesfully Registered {p.UserName}");
                    }
                    else
                    {
                        Log.Print(LogType.Error, $"Error creating a new account for player '{request.AuthInfo.UserName}'");
                    }
                }

                response.SessionInfo = request.SessionInfo;
                response.SessionInfo.AccountId = p.AccountId;
                response.SessionInfo.Handle = p.UserName;
                response.SessionInfo.ConnectionAddress = "127.0.0.1";
                response.SessionInfo.ProcessCode = "";
                response.SessionInfo.FakeEntitlements = "";
                response.SessionInfo.LanguageCode = "EN"; // Needs to be uppercase

                response.LobbyServerAddress = "127.0.0.1";

                LobbyGameClientProxyInfo proxyInfo = new LobbyGameClientProxyInfo();
                proxyInfo.AccountId = response.SessionInfo.AccountId;
                proxyInfo.SessionToken = request.SessionInfo.SessionToken;
                proxyInfo.AssignmentTime = 1565574095;
                proxyInfo.Handle = request.SessionInfo.Handle;
                proxyInfo.Status = ClientProxyStatus.Assigned;

                response.ProxyInfo = proxyInfo;

                return context.Response.WriteAsync(JsonConvert.SerializeObject(response));
            });
        }
    }
}
