using IntermediatorBot.ManagerMethods;
using IntermediatorBot.Utils;
using IntermediatorBotSample.MessageRouting;
using IntermediatorBotSample.Settings;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Web.Http;
using System.Web.Http.Cors;
using Underscore.Bot.MessageRouting;
using Underscore.Bot.MessageRouting.DataStore;
using Underscore.Bot.Models;

namespace IntermediatorBotSample.Controllers
{
    /// <summary>
    /// This class handles the direct requests made by the Agent UI component.
    /// </summary>
    public class AgentController : ApiController
    {
        private const string ResponseNone = "None";
        List<Party> clientsParties = new List<Party>();

        /// <summary>
        /// Handles requests sent by the Agent UI.
        /// If there are no aggregation channels set and one or more pending requests exist,
        /// the oldest request is processed and sent to the Agent UI.
        /// </summary>
        /// <param name="id">Not used.</param>
        /// <returns>The details of the user who made the request or "None", if no pending requests
        /// or if one or more aggregation channels are set up.</returns>
        [EnableCors("*", "*", "*")]
        [HttpGet]
        public string GetAgentById(int id)
        {
            string response = ResponseNone;
            MessageRouterManager messageRouterManager = WebApiConfig.MessageRouterManager;
            IRoutingDataManager routingDataManager = messageRouterManager.RoutingDataManager;

            if (routingDataManager.GetAggregationParties().Count == 0
                && routingDataManager.GetPendingRequests().Count > 0)
            {
                try
                {
                    Party conversationClientParty = messageRouterManager.RoutingDataManager.GetPendingRequests().First();
                    var conversationId = conversationClientParty.ConversationAccount.Id;
                    messageRouterManager.RoutingDataManager.RemovePendingRequest(conversationClientParty);
                    response = conversationClientParty.ToJsonString();
                }
                catch (InvalidOperationException e)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to handle a pending request: {e.Message}");
                }
            }

            return response;
        }

        [EnableCors("*", "*", "*")]
        [HttpPost]
        public string RefreshAgent(int id)
        {
            string response = ResponseNone;
            MessageRouterManager messageRouterManager = WebApiConfig.MessageRouterManager;
            IRoutingDataManager routingDataManager = messageRouterManager.RoutingDataManager;
            var Settings = new Settings.BotSettings();
            Manager manager = new Manager(Settings[BotSettings.KeyRoutingDataStorageConnectionString]);

                try
                {
                    Dictionary<Party, Party> connectedParties = routingDataManager.GetConnectedParties();
                    Dictionary<Party, Party> waitingConnectedParties = manager.GetWaitingConnectedParties();

                    foreach (var connectedPartie in connectedParties)
                    {
                        clientsParties.Add(connectedPartie.Value);
                    }

                    foreach (var waitingConnectedPartie in waitingConnectedParties)
                    {
                        clientsParties.Add(waitingConnectedPartie.Value);
                    }

                    response = JsonConvert.SerializeObject(clientsParties);
                }
                catch (InvalidOperationException e)
                {
                    Debug.WriteLine($"{e.Message}");
                }

            Debug.WriteLine("refresh");

            return response;
        }

        

        [EnableCors("*", "*", "*")]
        [HttpPost]
        public string DisconnectConversations(string conversationId)
        {
           

            return "toto";
        }
    }
}