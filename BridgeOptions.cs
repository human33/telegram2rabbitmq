namespace TelegramBridge 
{
    public record BridgeOptions(
        string TelegramToken,
        string RabbitUri, 
        string RabbitQueueIn, 
        string RabbitQueueOut, 
        string MongoConnection,
        string MongoDatabase
    )
    {
        // public string TelegramToken {get;set;}
        // public string RabbitHost {get;set;}
        // public string RabbitQueueIn {get;set;}
        // public string RabbitQueueOut {get;set;}
        // public string MongoConnection {get;set;}
    } 
}