using Microsoft.Azure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;
using Underscore.Bot.MessageRouting;
using Underscore.Bot.MessageRouting.DataStore.Azure;
using Underscore.Bot.Models;
using Underscore.Bot.Models.Azure;
using Underscore.Bot.Utils;

namespace IntermediatorBot.ManagerMethods
{
    public class Manager : AzureTableStorageRoutingDataManager
    {
        protected CloudTable _waitingConnectionsTable;
        protected const string TableWaitingConnections = "WaitingConnections";

        public Manager(string connectionString, GlobalTimeProvider globalTimeProvider = null) : base(connectionString, globalTimeProvider)
        {
            _waitingConnectionsTable = AzureStorageHelper.GetTable(connectionString, TableWaitingConnections);
        }

        // Connexion
        public MessageRouterResult RemoveConnection(Party conversationOwnerParty)
        {
            MessageRouterResult messageRouterResult = null;

            Dictionary<Party, Party> connectedParties = GetConnectedParties();

            bool remove = connectedParties.Remove(conversationOwnerParty);

            if (connectedParties != null && remove)
            {
                Party conversationClientParty = GetConnectedCounterpart(conversationOwnerParty);

                bool delete = DeleteEntry<ConnectionEntity>(_connectionsTable, conversationClientParty.ConversationAccount.Id, conversationOwnerParty.ConversationAccount.Id);

                if (delete)
                {
                    messageRouterResult = new MessageRouterResult();
                    messageRouterResult.Activity = null;
                    messageRouterResult.ErrorMessage = null;
                    messageRouterResult.Type = MessageRouterResultType.Disconnected;
                    messageRouterResult.ConversationClientParty = conversationClientParty;
                    messageRouterResult.ConversationOwnerParty = conversationOwnerParty;
                }
            }

            return messageRouterResult;
        }

        public MessageRouterResult Connect(Party conversationOwnerParty, Party conversationClientParty)
        {
            MessageRouterResult result = new MessageRouterResult()
            {
                ConversationOwnerParty = conversationOwnerParty,
                ConversationClientParty = conversationClientParty
            };

            if (conversationOwnerParty != null && conversationClientParty != null)
            {
                DateTime connectionStartedTime = GetCurrentGlobalTime();
                conversationClientParty.ResetConnectionRequestTime();
                conversationClientParty.ConnectionEstablishedTime = connectionStartedTime;

                bool wasConnectionAdded =
                    ExecuteConnection(conversationOwnerParty, conversationClientParty);

                if (wasConnectionAdded)
                {
                    result.Type = MessageRouterResultType.Connected;
                }
                else
                {
                    result.Type = MessageRouterResultType.Error;
                    result.ErrorMessage =
                        $"Failed to add connection between {conversationOwnerParty} and {conversationClientParty}";
                }
            }
            else
            {
                result.Type = MessageRouterResultType.Error;
                result.ErrorMessage = "Either the owner or the client is missing";
            }

            return result;
        }

        protected bool ExecuteConnection(Party conversationOwnerParty, Party conversationClientParty)
        {
            return AzureStorageHelper.Insert<ConnectionEntity>(_connectionsTable, new ConnectionEntity()
            {
                PartitionKey = conversationClientParty.ConversationAccount.Id,
                RowKey = conversationOwnerParty.ConversationAccount.Id,
                Client = JsonConvert.SerializeObject(new PartyEntity(conversationClientParty, PartyEntityType.Client)),
                Owner = JsonConvert.SerializeObject(new PartyEntity(conversationOwnerParty, PartyEntityType.Owner)),
            });
        }

        public bool ExecuteRemoveConnexion(string conversationClientId, string conversationOwnerId)
        {
            return DeleteEntry<ConnectionEntity>(_connectionsTable, conversationClientId, conversationOwnerId);
        }

        public bool ExecuteRemoveConnexionByConversationClientId(string conversationClientId)
        {
            bool delete = false;

            ConnectionEntity connection = null;
            TableQuery<ConnectionEntity> query = new TableQuery<ConnectionEntity>().Where(
                TableQuery.CombineFilters(
                TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, conversationClientId),
                TableOperators.Or,
                TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.Equal, conversationClientId)
                ));

            var result = _connectionsTable.ExecuteQuery(query);

            if (result.Count() != 0)
            {
                connection = _connectionsTable.ExecuteQuery(query).First();
                TableOperation deleteOperation = TableOperation.Delete(connection);
                _connectionsTable.Execute(deleteOperation);
                delete = true;
            }

            return delete;
        }

        // Waiting connexion
        public bool RemoveWaitingConnection(Party conversationOwnerParty, Party conversationClientParty)
        {
            return DeleteEntry<ConnectionEntity>(_waitingConnectionsTable, conversationClientParty.ConversationAccount.Id, conversationOwnerParty.ConversationAccount.Id);
        }

        public bool ExecuteRemoveWaitingConnexionByConversationClientId(string conversationClientId)
        {
            bool delete = false;

            ConnectionEntity connection = null;
            TableQuery<ConnectionEntity> query = new TableQuery<ConnectionEntity>().Where(
                TableQuery.CombineFilters(
                TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, conversationClientId),
                TableOperators.Or,
                TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.Equal, conversationClientId)
                ));

            var result = _waitingConnectionsTable.ExecuteQuery(query);

            if (result.Count() != 0)
            {
                connection = _waitingConnectionsTable.ExecuteQuery(query).First();
                TableOperation deleteOperation = TableOperation.Delete(connection);
                _waitingConnectionsTable.Execute(deleteOperation);
                delete = true;
            }

            return delete;
        }

        public Dictionary<Party, Party> GetWaitingConnectedParties()
        {
            Dictionary<Party, Party> waitingConnectedParties = new Dictionary<Party, Party>();
            List<ConnectionEntity> waitingConnectionEntities = null;

            try
            {
                waitingConnectionEntities =
                    _waitingConnectionsTable.ExecuteQuery(new TableQuery<ConnectionEntity>()).ToList();
            }
            catch (StorageException e)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to retrieve the connected parties: {e.Message}");
                return waitingConnectedParties; // Return empty dictionary
            }

            foreach (var connectionEntity in waitingConnectionEntities)
            {
                waitingConnectedParties.Add(
                    JsonConvert.DeserializeObject<PartyEntity>(connectionEntity.Owner).ToParty(),
                    JsonConvert.DeserializeObject<PartyEntity>(connectionEntity.Client).ToParty());
            }

            return waitingConnectedParties;
        }

        public bool ExecuteAddWaitingConnection(Party conversationOwnerParty, Party conversationClientParty)
        {
            return AzureStorageHelper.Insert<ConnectionEntity>(_waitingConnectionsTable, new ConnectionEntity()
            {
                PartitionKey = conversationClientParty.ConversationAccount.Id,
                RowKey = conversationOwnerParty.ConversationAccount.Id,
                Client = JsonConvert.SerializeObject(new PartyEntity(conversationClientParty, PartyEntityType.Client)),
                Owner = JsonConvert.SerializeObject(new PartyEntity(conversationOwnerParty, PartyEntityType.Owner)),
            });
        }

        public ConnectionEntity RetrieveWaitingConnectionByConversationIdOwner(string conversationId)
        {
            ConnectionEntity waitingConnection = null;
            TableQuery<ConnectionEntity> query = new TableQuery<ConnectionEntity>().Where(
                TableQuery.CombineFilters(
                TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, conversationId),
                TableOperators.Or,
                TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.Equal, conversationId)
                ));

            var result = _waitingConnectionsTable.ExecuteQuery(query);

            if (result.Count() != 0)
            {
                waitingConnection = _waitingConnectionsTable.ExecuteQuery(query).First();
            }

            return waitingConnection;
        }

        public ConnectionEntity retrieveWaitingConnectionEntity(string partitionKey, string rowKey)
        {
            ConnectionEntity waitingConnection = null;
            TableOperation retrieveOperation = TableOperation.Retrieve<ConnectionEntity>(partitionKey, rowKey);
            TableResult retrieveResult = _waitingConnectionsTable.Execute(retrieveOperation);

            if (retrieveResult.Result != null)
                waitingConnection = (ConnectionEntity)retrieveResult.Result;

            return waitingConnection;
        }

        public MessageRouterResult WaitingConnectAndClearPendingRequest(Party conversationOwnerParty, Party conversationClientParty)
        {
            MessageRouterResult result = new MessageRouterResult()
            {
                ConversationOwnerParty = conversationOwnerParty,
                ConversationClientParty = conversationClientParty
            };

            if (conversationOwnerParty != null && conversationClientParty != null)
            {
                DateTime connectionStartedTime = GetCurrentGlobalTime();
                conversationClientParty.ResetConnectionRequestTime();
                conversationClientParty.ConnectionEstablishedTime = connectionStartedTime;

                bool wasConnectionAdded =
                    ExecuteAddWaitingConnection(conversationOwnerParty, conversationClientParty);

                if (wasConnectionAdded)
                {
                    ExecuteRemovePendingRequest(conversationClientParty);
                    result.Type = MessageRouterResultType.ConnectionRequested;
                }
                else
                {
                    result.Type = MessageRouterResultType.Error;
                    result.ErrorMessage =
                        $"Failed to add connection between {conversationOwnerParty} and {conversationClientParty}";
                }
            }
            else
            {
                result.Type = MessageRouterResultType.Error;
                result.ErrorMessage = "Either the owner or the client is missing";
            }

            return result;
        }



        public static bool DeleteEntry<T>(CloudTable cloudTable, string partitionKey, string rowKey) where T : TableEntity
        {
            TableOperation retrieveOperation = TableOperation.Retrieve<T>(partitionKey, rowKey);
            TableResult retrieveResult = cloudTable.Execute(retrieveOperation);

            if (retrieveResult.Result is T entityToDelete)
            {
                TableOperation deleteOperation = TableOperation.Delete(entityToDelete);
                cloudTable.Execute(deleteOperation);
                return true;
            }

            return false;
        }
    }
}
