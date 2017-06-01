using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Discord;
using Discord.Addons.EmojiTools;
using Discord.Addons.InteractiveCommands;
using Discord.Commands;
using Discord.WebSocket;
using Serilog.Extensions.Logging;
using UtilityBot.Services.Configuration;
using UtilityBot.Services.Data;
using UtilityBot.Services.GitHub;
using UtilityBot.Services.Logging;
using UtilityBot.Services.Tags;

namespace UtilityBot
{
    internal class Program
    {
        private static void Main(string[] args) =>
            new Program().RunAsync().GetAwaiter().GetResult();

        private readonly CommandService _commands = new CommandService();
        private readonly Serilog.ILogger _logger = LogAdaptor.CreateLogger();
        private readonly DiscordSocketClient _client;
        private readonly Config _config;
        private IServiceProvider _provider;
        private IEnumerable<ulong> Whitelist => _config.ChannelWhitelist;

        public Program()
        {
            _client = new DiscordSocketClient(new DiscordSocketConfig
            {
#if DEBUG
                LogLevel = LogSeverity.Debug,
#else
                LogLevel = LogSeverity.Verbose,
#endif
            });
            _config = Config.Load();
        }

        private async Task RunAsync()
        {
            _provider = ConfigureServices();
            await ConfigureAsync();

            await _client.LoginAsync(TokenType.Bot, _config.Token);
            await _client.StartAsync();

            await Task.Delay(-1);
        }

        private IServiceProvider ConfigureServices()
        {
            // Configure logging
            var loggerFactory = new LoggerFactory();
            loggerFactory.AddProvider(new SerilogLoggerProvider(_logger));
            // Configure services
            var services = new ServiceCollection()
                .AddSingleton(_config)
                .AddSingleton(new CommandService(new CommandServiceConfig { CaseSensitiveCommands = false, ThrowOnError = false}))
                .AddSingleton(_logger)
                .AddSingleton<LogAdaptor>()
                .AddSingleton<InteractiveService>()
                .AddSingleton<TagService>()
                .AddSingleton<GitHubService>()
                .AddDbContext<TagContext>(options =>
                {
                    options
                        .UseNpgsql(_config.Database.ConnectionString)
                        .UseLoggerFactory(loggerFactory);
                });
            var provider = services.BuildServiceProvider();
            // Autowire and create these dependencies now
            provider.GetService<LogAdaptor>();
            provider.GetService<TagService>();
            provider.GetService<GitHubService>();
            return provider;
        }

        public async Task ConfigureAsync()
        {
            _client.MessageReceived += ProcessCommandAsync;
            var log = _provider.GetService<LogAdaptor>();
            _commands.Log += log.LogCommand;
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly());
        }

        private async Task ProcessCommandAsync(SocketMessage pMsg)
        {
            var message = pMsg as SocketUserMessage;
            if (message == null) return;
            if (message.Content.StartsWith("##")) return;

            int argPos = 0;
            if (!ParseTriggers(message, ref argPos)) return;

            var context = new SocketCommandContext(_client, message);
            var result = await _commands.ExecuteAsync(context, argPos, _provider);
            if (result is SearchResult search && !search.IsSuccess)
            {
                await message.AddReactionAsync(EmojiExtensions.FromText(":mag_right:"));
            }
            else if (result is PreconditionResult precondition && !precondition.IsSuccess)
                await message.AddReactionAsync(EmojiExtensions.FromText(":no_entry:"));
            else if (result is ParseResult parse && !parse.IsSuccess)
                await message.Channel.SendMessageAsync($"**Parse Error:** {parse.ErrorReason}");
            else if (result is TypeReaderResult reader && !reader.IsSuccess)
                await message.Channel.SendMessageAsync($"**Read Error:** {reader.ErrorReason}");
            else if (!result.IsSuccess)
                await message.AddReactionAsync(EmojiExtensions.FromText(":rage:"));
            _logger.Debug("Invoked {Command} in {Context} with {Result}", message, context.Channel, result);
        }

        private bool ParseTriggers(SocketUserMessage message, ref int argPos)
        {
            bool flag = false;
            if (message.HasMentionPrefix(_client.CurrentUser, ref argPos)) flag = true;
            else
            {
                foreach (var prefix in _config.CommandStrings)
                {
                    if (message.HasStringPrefix(prefix, ref argPos))
                    {
                        flag = true;
                        break;
                    }
                }
            }
            return flag ? Whitelist.Any(id => id == message.Channel.Id) : false;
        }
    }
}