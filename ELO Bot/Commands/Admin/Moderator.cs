﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using ELO_Bot.PreConditions;

namespace ELO_Bot.Commands.Admin
{
    [CheckModerator]
    public class Moderator : ModuleBase
    {
        [Command("Win")]
        [Summary("Win <users>")]
        [Remarks("Add a win + win points for the specified users")]
        public async Task Win(params IUser[] userlist)
        {
            var embed = new EmbedBuilder();
            var server = ServerList.Load(Context.Guild);
            var points = server.Winamount;
            if (!(server.Winamount > 0))
            {
                embed.AddField("ERROR", "You have not setup the server's win modifier yet");
                await ReplyAsync("", false, embed.Build());
                return;
            }

            await WinLossPoints(server, userlist.ToList(), true, points);
        }

        [Command("Lose")]
        [Summary("Lose <users>")]
        [Remarks("Add a loss to the specified users")]
        public async Task Lose(params IUser[] userlist)
        {
            var embed = new EmbedBuilder();
            var server = ServerList.Load(Context.Guild);
            var points = server.Lossamount;

            if (!(server.Lossamount > 0))
            {
                embed.AddField("ERROR", "You have not setup the server's loss modifier yet");
                await ReplyAsync("", false, embed.Build());
                return;
            }

            await WinLossPoints(server, userlist.ToList(), false, points);
        }

        [Command("Game")]
        [Summary("Game <lobbyname> <gamenumber> <winningteam>")]
        [Remarks("Automatically update wins/losses for the selected team")]
        public async Task Win(string lobbyname, int gamenumber, string team)
        {
            try
            {
                var server = ServerList.Load(Context.Guild);
                IMessageChannel channel = null;
                foreach (var chan in (Context.Guild as SocketGuild).Channels)
                    if (string.Equals(chan.Name, lobbyname, StringComparison.CurrentCultureIgnoreCase))
                        channel = chan as IMessageChannel;

                if (channel == null)
                {
                    var queuechannels = "";
                    foreach (var chan in server.Queue)
                    {
                        var getqueuechannels = await Context.Guild.GetChannelAsync(chan.ChannelId);
                        queuechannels += $"{getqueuechannels.Name}\n";
                    }
                    await ReplyAsync("**ERROR:** Please specify the channel in which queue was created\n" +
                                     "Here are a list:\n" +
                                     $"{queuechannels}");
                    return;
                }

                var game = server.Gamelist.FirstOrDefault(x => x.LobbyId == channel.Id
                                                               && x.GameNumber == gamenumber);
                var team1 = new List<IUser>();
                var team2 = new List<IUser>();
                foreach (var user in game.Team1)
                    team1.Add(await Context.Guild.GetUserAsync(user));

                foreach (var user in game.Team2)
                    team2.Add(await Context.Guild.GetUserAsync(user));

                switch (team.ToLower())
                {
                    case "team1":
                        await WinLossPoints(server, team1, true, server.Winamount);
                        await WinLossPoints(server, team2, false, server.Lossamount);
                        break;
                    case "team2":
                        await WinLossPoints(server, team2, true, server.Winamount);
                        await WinLossPoints(server, team1, false, server.Lossamount);
                        break;
                    default:
                        await ReplyAsync(
                            "Please specify a team in the following format `=game <lobby> <number> team1` or `=game <lobby> <number> team2`");
                        break;
                }
            }
            catch (Exception e)
            {
                await ReplyAsync(e.ToString());
            }
        }

        public async Task WinLossPoints(ServerList.Server server, List<IUser> users, bool win, int points)
        {
            var embed = new EmbedBuilder();
            foreach (var user in users)
            {
                var usr = server.UserList.FirstOrDefault(x => x.UserId == user.Id);
                if (user.Id == usr.UserId)
                {
                    if (win)
                    {
                        usr.Points = usr.Points + points;
                        usr.Wins++;
                        embed.AddField($"{usr.Username} WON", $"Points: **{usr.Points}**\n" +
                                                              $"W/L: **[{usr.Wins}/{usr.Losses}]**");
                        embed.Color = Color.Green;
                    }
                    else
                    {
                        points = Math.Abs(points);
                        usr.Points = usr.Points - points;
                        usr.Losses++;
                        if (usr.Points < 0)
                            usr.Points = 0;
                        embed.AddField($"{usr.Username} LOST", $"Points: **{usr.Points}**\n" +
                                                               $"W/L: **[{usr.Wins}/{usr.Losses}]**");
                        embed.Color = Color.Red;
                    }
                    try
                    {
                        await (user as IGuildUser).ModifyAsync(x => { x.Nickname = $"{usr.Points} ~ {usr.Username}"; });
                    }
                    catch
                    {
                        //
                    }
                    await CheckRank(server, user, usr);
                }
            }

            ServerList.Saveserver(server);
            await ReplyAsync("", false, embed.Build());
        }

        public async Task CheckRank(ServerList.Server server, IUser user, ServerList.Server.User subject)
        {
            foreach (var role in server.Ranks)
            {
                var u = user as IGuildUser;
                var r = Context.Guild.GetRole(role.RoleId);
                if (u.RoleIds.Contains(role.RoleId))
                    await u.RemoveRoleAsync(r);
            }
            try
            {
                var toprole = server.Ranks.Where(x => x.Points <= subject.Points).Max(x => x.Points);
                var top = server.Ranks.Where(x => x.Points == toprole);
                try
                {
                    var newrole = Context.Guild.GetRole(top.FirstOrDefault().RoleId);
                    await (user as IGuildUser).AddRoleAsync(newrole);
                }
                catch
                {
                    //role has been deleted
                }
            }
            catch
            {
                // No available roles
            }
        }
    }
}