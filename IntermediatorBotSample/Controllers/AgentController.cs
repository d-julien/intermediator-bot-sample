using IntermediatorBot.Utils;
using IntermediatorBotSample.MessageRouting;
using IntermediatorBotSample.Settings;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
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
                    AddConversationToTableStorage(response, conversationId);
                }
                catch (InvalidOperationException e)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to handle a pending request: {e.Message}");
                }
            }

            return response;
        }

        [EnableCors("*", "*", "*")]
        [HttpGet]
        public List<string> GetOpenConversations()
        {
            var Settings = new BotSettings();
            var pendingrequestList = new List<string>();

            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
                Settings[BotSettings.KeyRoutingDataStorageConnectionString]);

            // Create the table client.
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();

            // Create the CloudTable object that represents the "people" table.
            CloudTable table = tableClient.GetTableReference("pendingRequest");

            var TodayPartitionKey = DateTime.Now.Date.ToShortDateString().Replace('/', '-');
            // Construct the query operation for all customer entities where PartitionKey="Smith".
            TableQuery<RequestsStorage> query = new TableQuery<RequestsStorage>().Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, TodayPartitionKey));

            // Print the fields for each customer.
            foreach (RequestsStorage entity in table.ExecuteQuery(query))
            {
                pendingrequestList.Add(entity.RowKey);
            }
            return pendingrequestList;
        }

        

        [EnableCors("*", "*", "*")]
        [HttpPost]
        public string DisconnectConversations(string conversationId)
        {
            var Settings = new BotSettings();
            MessageRouterManager messageRouterManager = WebApiConfig.MessageRouterManager;
            MessageRouterResultHandler messageRouterResultHandler = new MessageRouterResultHandler(messageRouterManager);


            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
                Settings[BotSettings.KeyRoutingDataStorageConnectionString]);

            // Create the table client.
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();

            // Create the CloudTable object that represents the "people" table.
            CloudTable table = tableClient.GetTableReference("pendingRequest");

            var TodayPartitionKey = DateTime.Now.Date.ToShortDateString().Replace('/', '-');
            // Create a retrieve operation that takes a customer entity.
            TableOperation retrieveOperation = TableOperation.Retrieve<RequestsStorage>(TodayPartitionKey, conversationId);

            // Execute the retrieve operation.
            TableResult retrievedResult = table.Execute(retrieveOperation);

            // Print the phone number of the result.
            if (retrievedResult.Result != null)
            {
                var toto = ((RequestsStorage)retrievedResult.Result).Data;

                Party senderParty = JsonConvert.DeserializeObject<Party>(toto);

                IList<MessageRouterResult> messageRouterResults = messageRouterManager.Disconnect(senderParty);

                foreach (MessageRouterResult messageRouterResult in messageRouterResults)
                {
                    messageRouterResultHandler.HandleResultAsync(messageRouterResult);
                }
            }
            else
            {
                Console.WriteLine("Nous n'avons pas trouvé l'enregistrement à deconnecter");
            }

            return "toto";
        }

        private void AddConversationToTableStorage(string response, string id)
        {
            var Settings = new BotSettings();


            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
                Settings[BotSettings.KeyRoutingDataStorageConnectionString]);

            // Create the table client.
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();

            // Create the CloudTable object that represents the "people" table.
            CloudTable table = tableClient.GetTableReference("pendingRequest");

            // Create a new customer entity.
            RequestsStorage storeLine = new RequestsStorage(response, id);
            storeLine.Data = response;

            // Create the TableOperation object that inserts the customer entity.
            TableOperation insertOperation = TableOperation.Insert(storeLine);

            // Execute the insert operation.
            table.Execute(insertOperation);
        }
    }
}