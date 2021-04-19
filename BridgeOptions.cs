namespace TelegramBridge 
{
    public record BridgeOptions(
        string TelegramToken,
        string RabbitUri, 
        string RabbitQueueIn, 
        string RabbitQueueOut, 
        string MongoConnection,
        string MongoDatabase,
        long TelegramUpdateFrequencySec 
    )
    {
    } 
}