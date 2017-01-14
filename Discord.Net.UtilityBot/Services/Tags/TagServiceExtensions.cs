﻿using Discord.Commands;
using System.Threading.Tasks;

namespace UtilityBot.Services.Tags
{
    public static class TagServiceExtensions
    {
        public async static Task UsingTagService(this IDependencyMap map)
        {
            var service = new TagService(map);
            await service.BuildCommands();
            map.Add(service);
        }
    }
}
