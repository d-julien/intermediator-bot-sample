using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace IntermediatorBot.Utils
{
    public class UserLogs : TableEntity
    {
        public static string userLogs = "userLogs";

        public string question { get; set; }
        public string response { get; set; }

        public UserLogs()
        {
            
        }
        public UserLogs(string _idConversationUser, string nombre, string _question, string _response)
        {
            this.PartitionKey = _idConversationUser;
            this.RowKey = nombre;
            question = _question;
            response = _response;
        }

    }
}