using Bot.Library.Services;
using CliHelper;
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
        // URL used to authorize my account for this discord bot and the permissions it requires
        // https://discord.com/api/oauth2/authorize?client_id=999429756019818506&permissions=2153778176&scope=bot

        public async Task RunAsync(string[] args)
        {
            var discordClient = new DiscordSocketClient(new DiscordSocketConfig
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

            var cliClient = Client.Create()
                .AddControllers()
                .AddServices(services =>
                {
                    services.AddSingleton(discordClient);
                    services.AddSingleton(configuration);
                    services.AddSingleton<IDiscordService, DiscordService>();
                });

            var assembly = Assembly.GetExecutingAssembly();

            ConfigureDiscordSocketClient(discordClient, cliClient);

            await discordClient.LoginAsync(TokenType.Bot, configuration.GetValue<string>("Discord:Token"));
            await discordClient.StartAsync();

            await Task.Delay(-1);
        }

        private void ConfigureDiscordSocketClient(DiscordSocketClient client, Client cliClient)
        {
            client.Log += (message) =>
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

            client.MessageReceived += async (socketMessage) =>
            {
                if (socketMessage is null)
                    return;

                var socketUserMessage = socketMessage as SocketUserMessage;

                // Determine if the message is a command based on the prefix and make sure no bots trigger commands
                _logger.Trace("Message Received. User: {User}, Channel: {Channel}, Message: {Message}", socketUserMessage.Author.Username, socketUserMessage.Channel.Name, socketUserMessage.Content);

                var notUsed = 0;
                if (
                    socketUserMessage.HasMentionPrefix(client.CurrentUser, ref notUsed) ||
                    socketMessage.Author.IsBot
                )
                {
                    _logger.Debug("Message should NOT be treated as a command");
                    return;
                }

                _logger.Debug("Message should be treated as a command");

                var response = cliClient.Run(new string[] { socketMessage.Content });

                return;
            };
        }

        private void GetCliClient()
        {
            var client = Client.Create()
                .AddControllers();


        }
    }
}
