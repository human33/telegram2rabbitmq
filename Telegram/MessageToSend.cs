using System.Text.Json.Serialization;

namespace TelegramBridge.Telegram
{
    // public record MessageToSend
    // {
    //     [JsonPropertyName("chat_id")]
    //     public string ChatId { get; }
    //     
    //     [JsonPropertyName("text")]
    //     public string Text { get; }
    // }
    
    public record MessageToSend(
        [property: JsonPropertyName("chat_id")]
        string ChatId,
        
        [property: JsonPropertyName("text")]
        string Text
    );
}