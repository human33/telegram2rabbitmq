using System.Text.Json.Serialization;

namespace TelegramBridge.Telegram
{
    public record User
    {
        [JsonPropertyName("id")]
        public string Id { get; }
        
        [JsonPropertyName("username")]
        public string Username { get; }
    }
}