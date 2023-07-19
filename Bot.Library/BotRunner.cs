using Bot.Library.Services;
using CommandSurfacer;
using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using NLog.Config;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace Bot.Library
{
    public class BotRunner
    {
        private static readonly ILogger _logger = LogManager.GetCurrentClassLogger();

        public async Task RunAsync(string[] args)
        {
            var discord = new DiscordSocketClient(new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.All
            });

#if DEBUG
            var environment = "Development";
#else
            var environment = "Production";
#endif

            _logger.Debug("Environment: {Environment}", environment);

            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", true, true)
                .AddJsonFile($"appsettings.{environment}.json", true, true)
                .AddEnvironmentVariables()
                .AddCommandLine(args)
                .Build() as IConfiguration;

            var cli = Client.Create()
                .AddServices(services =>
                {
                    services.AddSingleton(discord);
                    services.AddSingleton(configuration);
                    services.AddSingleton<IDiscordService, DiscordService>();
                });

            //var assembly = Assembly.GetExecutingAssembly();

            ConfigureDiscordSocketClient(discord, cli);

            await discord.LoginAsync(TokenType.Bot, configuration.GetValue<string>("Discord:Token"));
            await discord.StartAsync();

            await Task.Delay(-1);
        }

        private void ConfigureDiscordSocketClient(DiscordSocketClient discord, Client cli)
        {
            discord.Log += (message) =>
            {
                switch (message.Severity)
                {
                    case LogSeverity.Critical:
                        _logger.Fatal(message.ToString());
                        break;
                    case LogSeverity.Error:
                        _logger.Error(message.ToString());
                        break;
                    case LogSeverity.Warning:
                        _logger.Warn(message.ToString());
                        break;
                    case LogSeverity.Info:
                        _logger.Info(message.ToString());
                        break;
                    case LogSeverity.Debug:
                        _logger.Debug(message.ToString());
                        break;
                    case LogSeverity.Verbose:
                        _logger.Trace(message.ToString());
                        break;

                    default:
                        break;
                }

                return Task.CompletedTask;
            };

            discord.MessageReceived += async (socketMessage) =>
            {
                if (socketMessage is null)
                    return;

                var socketUserMessage = socketMessage as SocketUserMessage;

                // Determine if the message is a command based on the prefix and make sure no bots trigger commands
                _logger.Trace("Message Received. User: {User}, Channel: {Channel}, Message: {Message}", socketUserMessage.Author.Username, socketUserMessage.Channel.Name, socketUserMessage.Content);

                var notUsed = 0;
                if (
                    socketUserMessage.HasMentionPrefix(discord.CurrentUser, ref notUsed) ||
                    socketMessage.Author.IsBot
                )
                {
                    _logger.Debug("Message should NOT be treated as a command");
                    return;
                }

                _logger.Debug("Message should be treated as a command");

                await cli.RunAsync(socketMessage.Content);

                //var response = cli.Run<string>(new string[] { socketMessage.Content }); 

                //var split = response.Split("|");
                //var question = split[0];
                //var answer = split[1];

                //await socketUserMessage.Channel.SendMessageAsync(question);
                //await socketUserMessage.Channel.SendMessageAsync("Answer in..");
                //for (var second = 5; second > 0; second--)
                //{
                //    await socketUserMessage.Channel.SendMessageAsync(second.ToString());
                //    await Task.Delay(750);
                //}
                //await socketUserMessage.Channel.SendMessageAsync(answer);
                //return;
            };
        }
    }
}
