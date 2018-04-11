using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace IntermediatorBot.Utils
{
    public class RequestsStorage : TableEntity
    {
        public string Data { get; set; }

        public RequestsStorage()
        {
        }

        public RequestsStorage(string data, string conversationId)
        {
            Data = data;
            PartitionKey = DateTime.Now.Date.ToShortDateString().Replace('/', '-');
            RowKey = conversationId;
        }
    }
}