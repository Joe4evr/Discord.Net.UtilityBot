﻿using Discord;
using Discord.Addons.EmojiTools;
using Discord.Addons.InteractiveCommands;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UtilityBot.Services.Tags;

namespace UtilityBot.Modules.Tags
{
    public class TagModule : ModuleBase<SocketCommandContext>
    {
        private readonly TagService _service;
        private readonly InteractiveService _interactive;

        public TagModule(TagService service, InteractiveService interactive)
        {
            _service = service;
            _interactive = interactive;
        }

        [Command("tag create", RunMode = RunMode.Async)]
        [Priority(1000)]
        public async Task AddTag()
        {
            await ReplyAsync("**What is the name of your tag?** _'cancel' to cancel_");
            var nameResponse = await _interactive.WaitForMessage(Context.User, Context.Channel, TimeSpan.FromSeconds(180));
            if (nameResponse.Content == "cancel") return;
            string name = nameResponse.Content;

            await ReplyAsync("**List any semicolon separated aliases:** _'cancel' to cancel; 'none' for no aliases_");
            var aliasResponse = await _interactive.WaitForMessage(Context.User, Context.Channel, TimeSpan.FromSeconds(180));
            if (aliasResponse.Content == "cancel") return;
            string[] aliases = new string[0];
            if (aliasResponse.Content != "none")
                aliases = aliasResponse.Content.Split(';');

            await ReplyAsync("**Enter the tag body:** _'cancel' to cancel_");
            var contentResponse = await _interactive.WaitForMessage(Context.User, Context.Channel, TimeSpan.FromSeconds(180));
            if (contentResponse.Content == "cancel") return;
            string content = contentResponse.Content;

            var tag = new Tag
            {
                Name = name,
                Aliases = aliases.ToList(),
                Content = content,
                OwnerId = Context.User.Id,
            };

            var embed = new EmbedBuilder()
                .WithTitle(tag.Name)
                .WithDescription(tag.Content)
                .WithFooter(x => { x.Text = $"Aliases: {string.Join(", ", aliases)}"; })
                .WithAuthor(x => { x.IconUrl = Context.User.AvatarUrl; x.Name = Context.User.Username; })
                .Build();
            await ReplyAsync("**Is This OK?**", embed: embed);
            var okResponse = await _interactive.WaitForMessage(Context.User, Context.Channel, TimeSpan.FromSeconds(30));
            if (okResponse.Content.Trim().ToLower() == "yes")
                await _service.AddTag(tag);
            else
                await ReplyAsync($"{UnicodeEmoji.FromText(":put_litter_in_its_place:")} **Tag disposed**");
        }

        [Command("tag remove", RunMode = RunMode.Async)]
        [Priority(1000)]
        public async Task RemoveTag([Remainder] string name)
        {
            var tag = _service.db.Tags.FirstOrDefault(t => t.Name == name || t.Aliases.Contains(name));
            if (tag == null)
            {
                await ReplyAsync("**No tags found.**");
                return;
            }

            var confirmation = await _interactive.WaitForMessage(Context.User, Context.Channel, TimeSpan.FromSeconds(30));
            if (confirmation.Content.Contains("y"))
            {
                await _service.RemoveTag(tag);
                await ReplyAsync($"{UnicodeEmoji.FromText(":put_litter_in_its_place:")}");
            }
            else
                await ReplyAsync($"{UnicodeEmoji.FromText(":wheelchair:")}");
        }

        [Command("tag modify", RunMode = RunMode.Async)]
        [Priority(1000)]
        public async Task ModifyTag(ModifyType modify, [Remainder] string name)
        {
            var tag = _service.db.Tags.FirstOrDefault(t => t.Name == name || t.Aliases.Contains(name));
            if (tag == null)
            {
                await ReplyAsync("**No tags found.**");
                return;
            }

            switch (modify)
            {
                case ModifyType.Name:
                    {
                        await ReplyAsync("**What is the name of your tag?** _'cancel' to cancel_");
                        var nameResponse = await _interactive.WaitForMessage(Context.User, Context.Channel, TimeSpan.FromSeconds(180));
                        if (nameResponse.Content == "cancel") return;
                        tag.Name = nameResponse.Content;
                        break;
                    }
                case ModifyType.Alias:
                    {
                        await ReplyAsync("**List any semicolon separated aliases:** _'cancel' to cancel; 'none' for no aliases_");
                        var aliasResponse = await _interactive.WaitForMessage(Context.User, Context.Channel, TimeSpan.FromSeconds(180));
                        if (aliasResponse.Content == "cancel") return;
                        string[] aliases = new string[0];
                        if (aliasResponse.Content != "none")
                            aliases = aliasResponse.Content.Split(';');
                        tag.Aliases = aliases.ToList();
                        break;
                    }
                case ModifyType.Body:
                    {
                        await ReplyAsync("**Enter the modified body:** _'cancel' to cancel_");
                        await ReplyAsync($"```\n{tag.Content}```");
                        var contentResponse = await _interactive.WaitForMessage(Context.User, Context.Channel, TimeSpan.FromSeconds(180));
                        if (contentResponse.Content == "cancel") return;
                        tag.Content = contentResponse.Content;
                        break;
                    }
            }

            _service.db.Save();
            await _service.BuildCommands();
            await ReplyAsync(UnicodeEmoji.FromText(":ok:"));
        }

        [Command("tag list")]
        [Alias("tags")]
        [Priority(1000)]
        public async Task List()
        {
            await ReplyAsync($"**Tags:** {string.Join(", ", _service.db.Tags.Select(t => t.Name))}");
        }

        public enum ModifyType
        {
            Name,
            Alias,
            Body
        }
    }
}
