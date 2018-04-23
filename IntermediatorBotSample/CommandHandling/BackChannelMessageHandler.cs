using IntermediatorBot.ManagerMethods;
using IntermediatorBot.Strings;
using IntermediatorBot.Utils;
using IntermediatorBotSample.Settings;
using Microsoft.Bot.Connector;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using Underscore.Bot.MessageRouting;
using Underscore.Bot.MessageRouting.DataStore;
using Underscore.Bot.Models;
using Underscore.Bot.Models.Azure;
using Underscore.Bot.Utils;

namespace IntermediatorBotSample.CommandHandling
{
    /// <summary>
    /// Handler for back channel messages.
    /// </summary>
    public class BackChannelMessageHandler
    {
        public const string DefaultBackChannelId = "backchannel";
        public const string DefaultPartyKey = "conversationId";

        /// <summary>
        /// The ID for back channel messages.
        /// </summary>
        public string BackChannelId
        {
            get;
            protected set;
        }

        /// <summary>
        /// The key identifying the serialized party data in the back channel message.
        /// </summary>
        public string PartyKey
        {
            get;
            protected set;
        }

        private IRoutingDataManager _routingDataManager;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="routingDataManager">The routing data manager instance.</param>
        /// <param name="backchannelId">The ID for back channel messages. If null, the default value is used.</param>
        /// <param name="partyKey">The key identifying the serialized party data. If null, the default value is used.</param>
        public BackChannelMessageHandler(IRoutingDataManager routingDataManager, string backChannelId = null, string partyKey = null)
        {
            _routingDataManager = routingDataManager
                ?? throw new ArgumentNullException("Routing data manager instance must be given");

            BackChannelId = string.IsNullOrEmpty(backChannelId) ? DefaultBackChannelId : backChannelId;
            PartyKey = string.IsNullOrEmpty(partyKey) ? DefaultPartyKey : partyKey;
        }

        /// <summary>
        /// Checks the given activity for back channel messages and handles them, if detected.
        /// Currently the only back channel message supported is for creating connections
        /// (establishing 1:1 conversations).
        /// </summary>
        /// <param name="activity">The activity to check for back channel messages.</param>
        /// <returns>
        /// The result:
        ///     * MessageRouterResultType.Connected: A connection (1:1 conversation) was created
        ///     * MessageRouterResultType.NoActionTaken: No back channel message detected
        ///     * MessageRouterResultType.Error: See the error message for details
        /// </returns>
        public virtual MessageRouterResult HandleBackChannelMessage(Activity activity)
        {
            MessageRouterResult messageRouterResultNoAction = new MessageRouterResult { Type = MessageRouterResultType.NoActionTaken };
            MessageRouterResult messageRouterResult = new MessageRouterResult();
            var Settings = new BotSettings();
            Manager manager = new Manager(Settings[BotSettings.KeyRoutingDataStorageConnectionString]);
            if (activity == null || string.IsNullOrEmpty(activity.Text))
            {
                messageRouterResult.Type = MessageRouterResultType.Error;
                messageRouterResult.ErrorMessage = $"The given activity ({nameof(activity)}) is either null or the message is missing";
            }
            else if (activity.Text.Equals(BackChannelId))
            {
                if (activity.ChannelData == null)
                {
                    messageRouterResult.Type = MessageRouterResultType.Error;
                    messageRouterResult.ErrorMessage = "No channel data";
                }
                else
                {
                    // Handle accepted request and start 1:1 conversation
                    Party conversationClientParty = null;

                    try
                    {
                        conversationClientParty = ParsePartyFromChannelData(activity.ChannelData);
                        Debug.WriteLine($"Client : {JsonConvert.SerializeObject(conversationClientParty)}");
                    }
                    catch (Exception e)
                    {
                        messageRouterResult.Type = MessageRouterResultType.Error;
                        messageRouterResult.ErrorMessage =
                            $"Failed to parse the party information from the back channel message: {e.Message}";
                    }

                    if (conversationClientParty != null)
                    {
                        Party conversationOwnerParty = MessagingUtils.CreateSenderParty(activity);
                        Debug.WriteLine($"Owner : {JsonConvert.SerializeObject(conversationOwnerParty)}");

                        MessageRouterManager messageRouterManager = WebApiConfig.MessageRouterManager;
                        IRoutingDataManager routingDataManager = messageRouterManager.RoutingDataManager;

                        bool isConnected = false;

                        Dictionary<Party, Party> connectedParties = routingDataManager.GetConnectedParties();

                        foreach (var connectedPartie in connectedParties)
                        {
                            if (connectedPartie.Value.ConversationAccount.Id == conversationClientParty.ConversationAccount.Id)
                            {
                                isConnected = true;
                                break;
                            }
                        }

                        if (isConnected)
                        {
                            bool deleteConnexion = manager.ExecuteRemoveConnexionByConversationClientId(conversationClientParty.ConversationAccount.Id);
                            messageRouterResult = deleteConnexion ? manager.Connect(conversationOwnerParty, conversationClientParty) : messageRouterResultNoAction;
                        }
                        else
                        {
                            Dictionary<Party, Party> waitingConnectedParties = manager.GetWaitingConnectedParties();
                            bool isWaitingConnected = false;

                            foreach (var waitingConnectedPartie in waitingConnectedParties)
                            {
                                if (waitingConnectedPartie.Value.ConversationAccount.Id == conversationClientParty.ConversationAccount.Id)
                                {
                                    isWaitingConnected = true;
                                    break;
                                }
                            }

                            if (isWaitingConnected)
                            {
                                bool deleteWaitingConnection = manager.ExecuteRemoveWaitingConnexionByConversationClientId(conversationClientParty.ConversationAccount.Id);
                                messageRouterResult = deleteWaitingConnection ? manager.WaitingConnectAndClearPendingRequest(conversationOwnerParty, conversationClientParty) : messageRouterResultNoAction;

                                messageRouterResult.Activity = activity;
                            }
                            else
                            {
                                messageRouterResult = manager.WaitingConnectAndClearPendingRequest(conversationOwnerParty, conversationClientParty);

                                //messageRouterResult = _routingDataManager.ConnectAndClearPendingRequest(
                                //    conversationOwnerParty, conversationClientParty);

                                messageRouterResult.Activity = activity;
                            }
                        }
                    }
                }
            }
            else
            {
                // No back channel message detected
                //messageRouterResult.Type = MessageRouterResultType.NoActionTaken;
                ConnectionEntity waitingConnection = manager.RetrieveWaitingConnectionByConversationIdOwner(activity.Conversation.Id);

                if (waitingConnection != null)
                {
                    Party conversationOwnerParty = JsonConvert.DeserializeObject<PartyEntity>(waitingConnection.Owner).ToParty();
                    Party conversationClientParty = JsonConvert.DeserializeObject<PartyEntity>(waitingConnection.Client).ToParty();

                    if (activity.Text == $"@{conversationOwnerParty.ChannelAccount.Name} accept {conversationClientParty.ChannelAccount.Id}" || activity.Text == $"@{conversationOwnerParty.ChannelAccount.Name} reject {conversationClientParty.ChannelAccount.Id}")
                    {
                        if (activity.Text.Contains("accept"))
                        {
                            messageRouterResult = manager.Connect(conversationOwnerParty, conversationClientParty);
                        }
                        else
                        {
                            messageRouterResult.Type = MessageRouterResultType.ConnectionRejected;
                            messageRouterResult.ConversationClientParty = conversationClientParty;
                            messageRouterResult.ConversationOwnerParty = conversationOwnerParty;
                        }

                        manager.RemoveWaitingConnection(conversationOwnerParty, conversationClientParty);
                    }
                    else
                    {
                        messageRouterResult.Type = MessageRouterResultType.ConnectionAlreadyRequested;

                        if (activity.Conversation.Id == conversationOwnerParty.ConversationAccount.Id)
                        {
                            messageRouterResult.ConversationOwnerParty = conversationOwnerParty;
                            messageRouterResult.ConversationClientParty = conversationClientParty;
                        }
                        else
                        {
                            messageRouterResult.ConversationClientParty = conversationClientParty;
                        }
                    }

                }
                else
                {
                    // No back channel message detected
                    messageRouterResult.Type = MessageRouterResultType.NoActionTaken;
                }
            }

            return messageRouterResult;
        }

        /// <summary>
        /// Tries to parse the party information and deserialize the party instance from the given
        /// channel data object.
        /// </summary>
        /// <param name="channelData">The channel data object to parse.</param>
        /// <returns>A deserialized party instance.</returns>
        protected Party ParsePartyFromChannelData(object channelData)
        {
            string partyAsJsonString = ((JObject)channelData)[BackChannelId][PartyKey].ToString();

            if (string.IsNullOrEmpty(partyAsJsonString))
            {
                throw new NullReferenceException("Failed to find the party information from the channel data");
            }

            partyAsJsonString = partyAsJsonString.Replace(StringAndCharConstants.EndOfLineInJsonResponse, string.Empty);
            partyAsJsonString = partyAsJsonString.Replace(StringAndCharConstants.BackslashInJsonResponse, StringAndCharConstants.QuotationMark);
            return Party.FromJsonString(partyAsJsonString);
        }
    }
}