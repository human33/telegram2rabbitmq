using System.Text.Json.Serialization;

namespace TelegramBridge.Telegram
{
    public record Update
    {
        [JsonPropertyName("update_id")]
        private long UpdateId { get; }
        
        [JsonPropertyName("message")]
        private long Message { get; }
    }
}