﻿using Discord.Commands;
using Discord.Voice;
using Leaf.xNet;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WebSocketSharp;

namespace Discord.Gateway
{
    /// <summary>
    /// <see cref="DiscordClient"/> with Gateway support
    /// </summary>
    public class DiscordSocketClient : DiscordClient
    {
        #region events
        public delegate void UserHandler(DiscordSocketClient client, UserEventArgs args);
        public delegate void GuildHandler(DiscordSocketClient client, GuildEventArgs args);
        public delegate void GuildMemberUpdateHandler(DiscordSocketClient client, GuildMemberEventArgs args);
        public delegate void ChannelHandler(DiscordSocketClient client, ChannelEventArgs args);
        public delegate void VoiceStateHandler(DiscordSocketClient client, VoiceStateEventArgs args);
        public delegate void MessageHandler(DiscordSocketClient client, MessageEventArgs args);
        public delegate void ReactionHandler(DiscordSocketClient client, ReactionEventArgs args);
        public delegate void RoleHandler(DiscordSocketClient client, RoleEventArgs args);
        public delegate void BanUpdateHandler(DiscordSocketClient client, BanUpdateEventArgs args);
        public delegate void RelationshipHandler(DiscordSocketClient client, RelationshipEventArgs args);

        public delegate void LoginHandler(DiscordSocketClient client, LoginEventArgs args);
        public event LoginHandler OnLoggedIn;
        public event UserHandler OnLoggedOut;

        public delegate void SettingsHandler(DiscordSocketClient client, SettingsUpdatedEventArgs args);
        public event SettingsHandler OnSettingsUpdated;


        public delegate void SessionsHandler(DiscordSocketClient client, DiscordSessionsEventArgs args);
        public event SessionsHandler OnSessionsUpdated;

        public delegate void ProfileHandler(DiscordSocketClient client, ProfileUpdatedEventArgs args);
        public event ProfileHandler OnProfileUpdated;

        public event UserHandler OnUserUpdated;

        public delegate void SocketGuildHandler(DiscordSocketClient client, SocketGuildEventArgs args);
        public event SocketGuildHandler OnJoinedGuild;
        public event GuildHandler OnGuildUpdated;
        public event GuildHandler OnLeftGuild;

        public event GuildMemberUpdateHandler OnUserJoinedGuild;
        public event GuildMemberUpdateHandler OnUserLeftGuild;

        public delegate void GuildMemberHandler(DiscordSocketClient client, GuildMemberEventArgs args);
        public event GuildMemberHandler OnGuildMemberUpdated;
        public delegate void GuildMembersHandler(DiscordSocketClient client, GuildMembersEventArgs args);
        public event GuildMembersHandler OnGuildMembersReceived;

        public delegate void PresenceUpdateHandler(DiscordSocketClient client, PresenceUpdatedEventArgs args);
        public event PresenceUpdateHandler OnUserPresenceUpdated;

        public event RoleHandler OnRoleCreated;
        public event RoleHandler OnRoleUpdated;

        public event ChannelHandler OnChannelCreated;
        public event ChannelHandler OnChannelUpdated;
        public event ChannelHandler OnChannelDeleted;

#pragma warning disable CS0067
        [Obsolete("OnUserJoinedVoiceChannel is obsolete. Use OnVoiceStateUpdated instead.", true)]
        public event VoiceStateHandler OnUserJoinedVoiceChannel;
        [Obsolete("OnUserLeftVoiceChannel is obsolete. Use OnVoiceStateUpdated instead.", true)]
        public event VoiceStateHandler OnUserLeftVoiceChannel;
#pragma warning restore CS0067

        public event VoiceStateHandler OnVoiceStateUpdated;

        internal delegate void VoiceServerHandler(DiscordSocketClient client, DiscordVoiceServer server);
        internal event VoiceServerHandler OnVoiceServer;

        public delegate void EmojisUpdatedHandler(DiscordSocketClient client, EmojisUpdatedEventArgs args);
        public event EmojisUpdatedHandler OnEmojisUpdated;

        public delegate void UserTypingHandler(DiscordSocketClient client, UserTypingEventArgs args);
        public event UserTypingHandler OnUserTyping;

        public event MessageHandler OnMessageReceived;
        public event MessageHandler OnMessageEdited;
        public delegate void MessageDeletedHandler(DiscordSocketClient client, MessageDeletedEventArgs args);
        public event MessageDeletedHandler OnMessageDeleted;

        public event ReactionHandler OnMessageReactionAdded;
        public event ReactionHandler OnMessageReactionRemoved;

        public event BanUpdateHandler OnUserBanned;
        public event BanUpdateHandler OnUserUnbanned;

        public event RelationshipHandler OnRelationshipAdded;
        public event RelationshipHandler OnRelationshipRemoved;
        #endregion
        
        internal Dictionary<ulong, DiscordVoiceClient> VoiceClients { get; private set; }
        internal WebSocket Socket { get; private set; }
        public bool LoggedIn { get; private set; }
        internal uint? Sequence { get; set; }
        public string SessionId { get; set; }
        public bool ConnectToVoiceChannels { get; set; } = true;
        public CommandHandler CommandHandler { get; private set; }


        public DiscordSocketClient() : base() { }
        public DiscordSocketClient(string proxy, ProxyType proxyType) : base(proxy, proxyType) { }
        ~DiscordSocketClient()
        {
            Logout();
        }


        public void Login(string token)
        {
            Token = token;

            VoiceClients = new Dictionary<ulong, DiscordVoiceClient>();

            Socket = new WebSocket("wss://gateway.discord.gg/?v=6&encoding=json");
            if (HttpClient.Proxy != null)
            {
                if (HttpClient.Proxy.Type == ProxyType.HTTP) //WebSocketSharp only supports HTTP proxies :(
                    Socket.SetProxy($"http://{HttpClient.Proxy.Host}:{HttpClient.Proxy.Port}", "", "");
            }
            Socket.OnMessage += SocketDataReceived;
            Socket.OnClose += (sender, e) => 
            {
                if (LoggedIn)
                {
                    while (true)
                    {
                        try
                        {
                            Login(Token);

                            return;
                        }
                        catch
                        {
                            Thread.Sleep(1000);
                        }
                    }
                }
            };
            Socket.Connect();
        }


        public void Logout()
        {
            if (LoggedIn)
            {
                SessionId = null;
                LoggedIn = false;
                Socket.Close();

                OnLoggedOut?.Invoke(this, new UserEventArgs(User));
            }
        }


        public void CreateCommandHandler(string prefix)
        {
            CommandHandler = new CommandHandler(prefix, this);
        }


        private void SocketDataReceived(object sender, WebSocketSharp.MessageEventArgs result)
        {
            GatewayResponse payload = result.Data.Deserialize<GatewayResponse>();
            Sequence = payload.Sequence;

            Task.Run(() =>
            {
                switch (payload.Opcode)
                {
                    case GatewayOpcode.Event:
                        switch (payload.Title)
                        {
                            case "READY":
                                LoggedIn = true;
                                Login login = payload.Deserialize<Login>().SetJson(payload.Deserialize<JObject>()).SetClient(this);

                                this.User = login.User;
                                this.SessionId = login.SessionId;
                                OnLoggedIn?.Invoke(this, new LoginEventArgs(login));
                                break;
                            case "USER_SETTINGS_UPDATE":
                                OnSettingsUpdated?.Invoke(this, new SettingsUpdatedEventArgs(payload.Deserialize<Settings>()));
                                break;
                            case "USER_UPDATE":
                                User user = payload.Deserialize<User>().SetClient(this);

                                if (user.Id == User.Id)
                                    User.Update(user);

                                OnUserUpdated?.Invoke(this, new UserEventArgs(user));
                                break;
                            case "GUILD_MEMBER_LIST_UPDATE":
                                /*
                                var args = new GuildMembersEventArgs(payload.Deserialize<GatewayUserMemberQueryResponse>());

                                foreach (var member in args.Members)
                                {
                                    member.SetClient(this);
                                    member.GuildId = args.GuildId;
                                }

                                OnGuildMembersReceived?.Invoke(this, args);*/
                                break;
                            case "GUILD_CREATE":
                                OnJoinedGuild?.Invoke(this, new SocketGuildEventArgs(payload.Deserialize<SocketGuild>().SetClient(this)));
                                break;
                            case "GUILD_UPDATE":
                                OnGuildUpdated?.Invoke(this, new GuildEventArgs(payload.Deserialize<Guild>().SetClient(this)));
                                break;
                            case "GUILD_DELETE":
                                OnLeftGuild?.Invoke(this, new GuildEventArgs(payload.Deserialize<Guild>().SetClient(this)));
                                break;
                            case "GUILD_MEMBER_ADD":
                                OnUserJoinedGuild?.Invoke(this, new GuildMemberEventArgs(payload.Deserialize<GuildMemberUpdate>().SetClient(this).Member));
                                break;
                            case "GUILD_MEMBER_REMOVE":
                                OnUserLeftGuild?.Invoke(this, new GuildMemberEventArgs(payload.Deserialize<GuildMemberUpdate>().SetClient(this).Member));
                                break;
                            case "GUILD_MEMBER_UPDATE":
                                OnGuildMemberUpdated?.Invoke(this, new GuildMemberEventArgs(payload.Deserialize<GuildMember>().SetClient(this)));
                                break;
                            case "GUILD_MEMBERS_CHUNK":
                                OnGuildMembersReceived?.Invoke(this, new GuildMembersEventArgs(payload.Deserialize<GuildMemberList>().SetClient(this)));
                                break;
                            case "PRESENCE_UPDATE":
                                OnUserPresenceUpdated?.Invoke(this, new PresenceUpdatedEventArgs(payload.Deserialize<PresenceUpdate>()));
                                break;
                            case "VOICE_STATE_UPDATE":
                                OnVoiceStateUpdated?.Invoke(this, new VoiceStateEventArgs(payload.Deserialize<DiscordVoiceState>().SetClient(this)));
                                break;
                            case "VOICE_SERVER_UPDATE":
                                OnVoiceServer?.Invoke(this, payload.Deserialize<DiscordVoiceServer>());
                                break;
                            case "GUILD_ROLE_CREATE":
                                OnRoleCreated?.Invoke(this, new RoleEventArgs(payload.Deserialize<RoleContainer>().Role.SetClient(this)));
                                break;
                            case "GUILD_ROLE_UPDATE":
                                OnRoleUpdated?.Invoke(this, new RoleEventArgs(payload.Deserialize<RoleContainer>().Role.SetClient(this)));
                                break;
                            case "GUILD_EMOJIS_UPDATE":
                                OnEmojisUpdated?.Invoke(this, new EmojisUpdatedEventArgs(payload.Deserialize<EmojiContainer>().SetClient(this)));
                                break;
                            case "CHANNEL_CREATE":
                                OnChannelCreated?.Invoke(this, new ChannelEventArgs(payload.Deserialize<GuildChannel>().SetClient(this)));
                                break;
                            case "CHANNEL_UPDATE":
                                OnChannelUpdated?.Invoke(this, new ChannelEventArgs(payload.Deserialize<GuildChannel>().SetClient(this)));
                                break;
                            case "CHANNEL_DELETE":
                                OnChannelDeleted?.Invoke(this, new ChannelEventArgs(payload.Deserialize<GuildChannel>().SetClient(this)));
                                break;
                            case "TYPING_START":
                                OnUserTyping?.Invoke(this, new UserTypingEventArgs(payload.Deserialize<UserTyping>()));
                                break;
                            case "MESSAGE_CREATE":
                                OnMessageReceived?.Invoke(this, new MessageEventArgs(payload.Deserialize<Message>().SetClient(this)));
                                break;
                            case "MESSAGE_UPDATE":
                                OnMessageEdited?.Invoke(this, new MessageEventArgs(payload.Deserialize<Message>().SetClient(this)));
                                break;
                            case "MESSAGE_DELETE":
                                OnMessageDeleted?.Invoke(this, new MessageDeletedEventArgs(payload.Deserialize<DeletedMessage>()));
                                break;
                            case "MESSAGE_REACTION_ADD":
                                OnMessageReactionAdded?.Invoke(this, new ReactionEventArgs(payload.Deserialize<MessageReactionUpdate>().SetClient(this)));
                                break;
                            case "MESSAGE_REACTION_REMOVE":
                                OnMessageReactionRemoved?.Invoke(this, new ReactionEventArgs(payload.Deserialize<MessageReactionUpdate>().SetClient(this)));
                                break;
                            case "GUILD_BAN_ADD":
                                OnUserBanned?.Invoke(this, new BanUpdateEventArgs(payload.Deserialize<BanContainer>().SetClient(this)));
                                break;
                            case "GUILD_BAN_REMOVE":
                                OnUserUnbanned?.Invoke(this, new BanUpdateEventArgs(payload.Deserialize<BanContainer>().SetClient(this)));
                                break;
                            case "RELATIONSHIP_ADD":
                                OnRelationshipAdded?.Invoke(this, new RelationshipEventArgs(payload.Deserialize<Relationship>().SetClient(this)));
                                break;
                            case "RELATIONSHIP_REMOVE":
                                OnRelationshipRemoved?.Invoke(this, new RelationshipEventArgs(payload.Deserialize<Relationship>().SetClient(this)));
                                break;
                            case "USER_CONNECTIONS_UPDATE":
                                try
                                {
                                    DiscordProfile profile = this.User.GetProfile();
                                    OnProfileUpdated?.Invoke(this, new ProfileUpdatedEventArgs(profile));
                                }
                                catch { }
                                break;
                            case "MESSAGE_ACK": // triggered whenever another person logged into the account acknowledges a message
                                break;
                            case "SESSIONS_REPLACE":
                                OnSessionsUpdated?.Invoke(this, new DiscordSessionsEventArgs(payload.Deserialize<List<DiscordSession>>()));
                                break;
                        }
                        break;
                    case GatewayOpcode.InvalidSession:
                        this.LoginToGateway();
                        break;
                    case GatewayOpcode.Connected:
                        if (!LoggedIn)
                            this.LoginToGateway();
                        else
                            this.Resume();

                        int interval = payload.Deserialize<JObject>().GetValue("heartbeat_interval").ToObject<int>();

                        try
                        {
                            while (true)
                            {
                                Socket.Send(GatewayOpcode.Heartbeat, this.Sequence);
                                Thread.Sleep(interval);
                            }
                        }
                        catch { }
                        break;
                }
            });
        }
    }
}