using System.Text.Json.Serialization;

namespace TelegramBridge.Telegram
{
    public class MessageToSend
    {
        [JsonPropertyName("chat_id")]
        public string ChatId { get; }
        
        [JsonPropertyName("text")]
        public string Text { get; }
    }
}