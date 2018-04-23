using IntermediatorBotSample.Settings;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace IntermediatorBot.Utils
{
    public class LogsManager
    {
        static BotSettings Settings = new BotSettings();
        static CloudStorageAccount storageAccount = CloudStorageAccount.Parse(Settings[BotSettings.KeyRoutingDataStorageConnectionString]);

        public static bool SaveUserLogs(UserLogs userLogs)
        {
            bool save = false;
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
            CloudTable table = tableClient.GetTableReference("botdata");
            TableOperation insertOrReplaceOperation = TableOperation.InsertOrReplace(userLogs);
            TableResult result = table.Execute(insertOrReplaceOperation);

            if (result != null)
                save = true;

            return save;
        }

        public static List<UserLogs> RetrieveUserLogs(string idConversation)
        {
            List<UserLogs> logs = new List<UserLogs>();
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
            CloudTable table = tableClient.GetTableReference("botdata");
            TableQuery<UserLogs> query = new TableQuery<UserLogs>().Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, idConversation));

            foreach (UserLogs entity in table.ExecuteQuery(query))
            {
                logs.Add(entity);
            }

            return logs;
        }

        public static void DeleteUserLogs(string idConversation)
        {
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
            CloudTable table = tableClient.GetTableReference("botdata");
            TableQuery<UserLogs> query = new TableQuery<UserLogs>().Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, idConversation));

            foreach (UserLogs entity in table.ExecuteQuery(query))
            {
                TableOperation deleteOperation = TableOperation.Delete(entity);
                TableResult result = table.Execute(deleteOperation);

            }
        }
    }
}