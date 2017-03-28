﻿using Discord.Commands;
using Discord.WebSocket;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using UtilityBot.Services.Configuration;

namespace UtilityBot.Modules.Janitor
{
    public class RequireJanitorRangeAttribute : ParameterPreconditionAttribute
    {
        // TODO: dm-safety
        public override Task<PreconditionResult> CheckPermissions(ICommandContext context, ParameterInfo parameter, object value, IDependencyMap map)
        {
            int v = (int)value;
            if (v > 10)
            {
                var config = map.Get<Config>();

                if (!config.GuildRoleMap.TryGetValue(context.Guild.Id, out IEnumerable<ulong> roles))
                    return Task.FromResult(PreconditionResult.FromError("This guild does not have a whitelist."));

                if (!(context.User as SocketGuildUser).Roles.Any(id => roles.Contains(id.Id)))
                    return Task.FromResult(PreconditionResult.FromError("You do not have a whitelisted role."));

                return Task.FromResult(PreconditionResult.FromSuccess());
            }
            else return Task.FromResult(PreconditionResult.FromSuccess());
        }
    }
}
