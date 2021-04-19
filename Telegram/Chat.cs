using System.Text.Json.Serialization;

namespace TelegramBridge.Telegram
{
    public record Chat
    {
        [JsonPropertyName("id")]
        public string Id { get; }
    }
}