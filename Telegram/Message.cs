using System.Text.Json.Serialization;

namespace TelegramBridge.Telegram
{
    public record Message
    {
        [JsonPropertyName("message_id")]
        public string MessageId { get; }
        
        [JsonPropertyName("from")]
        public User From { get; }
        
        [JsonPropertyName("chat")]
        public Chat Chat { get; }
        
        [JsonPropertyName("date")]
        public long DateUnixTime { get; }
        
        [JsonPropertyName("text")]
        public string Text { get; }
    }
}