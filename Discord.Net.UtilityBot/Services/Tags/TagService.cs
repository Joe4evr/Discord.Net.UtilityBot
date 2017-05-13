using Discord;
using Discord.Commands;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading.Tasks;
using UtilityBot.Services.Data;

namespace UtilityBot.Services.Tags
{
    public class TagService
    {
        private readonly CommandService _commands;
        private readonly IServiceProvider _provider;
        private TagContext _database => _provider.GetService<TagContext>();

        private ModuleInfo _tagModule;

        public TagService(CommandService commands, IServiceProvider provider)
        {
            _commands = commands;
            _provider = provider;
            BuildCommands().GetAwaiter().GetResult();
        }

        public async Task BuildCommands()
        {
            if (_tagModule != null)
                await _commands.RemoveModuleAsync(_tagModule);

            _tagModule = await _commands.CreateModuleAsync("", module =>
            {
                module.Name = "Tags";


                foreach (var tag in _database.Tags.Include(x => x.Aliases))
                {
                    module.AddCommand(tag.Name, async (context, args, map) =>
                    {
                        var builder = new EmbedBuilder()
                            .WithTitle(tag.Name)
                            .WithDescription(tag.Content);
                        var user = await context.Channel.GetUserAsync((ulong)tag.OwnerId);
                        if (user != null)
                            builder.Author = new EmbedAuthorBuilder()
                                .WithIconUrl(user.GetAvatarUrl())
                                .WithName(user.Username);
                        await context.Channel.SendMessageAsync("", embed: builder.Build());
                        var _ = IncrementTag(tag);
                    }, builder =>
                    {
                        builder.AddAliases(tag.Aliases.Select(x => x.Trigger).ToArray());
                    });
                }
            });
        }

        public async Task AddTag(TagInfo tag)
        {
            _database.Tags.Add(tag);
            await _database.SaveChangesAsync();
            await BuildCommands();
        }
        public async Task RemoveTag(TagInfo tag)
        {
            _database.Tags.Remove(tag);
            await _database.SaveChangesAsync();
            await BuildCommands();
        }
        public async Task IncrementTag(TagInfo tag)
        {
            tag.Uses++;
            await _database.SaveChangesAsync();
        }
    }
}
