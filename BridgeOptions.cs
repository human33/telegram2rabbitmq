namespace TelegramBridge 
{
    public class BridgeOptions
    {
        public string TelegramToken {get;set;}
        public string RabbitHost {get;set;}
        public string RabbitQueueIn {get;set;}
        public string RabbitQueueOut {get;set;}
        public string MongoConnection {get;set;}
    } 
}