namespace TelegramBridge 
{
    public record BridgeOptions(
        string TelegramToken,
        string RabbitHost, 
        string RabbitQueueIn, 
        string RabbitQueueOut, 
        string MongoConnection
    )
    {
        // public string TelegramToken {get;set;}
        // public string RabbitHost {get;set;}
        // public string RabbitQueueIn {get;set;}
        // public string RabbitQueueOut {get;set;}
        // public string MongoConnection {get;set;}
    } 
}