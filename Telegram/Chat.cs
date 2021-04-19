using System.Text.Json.Serialization;

namespace TelegramBridge.Telegram
{
    public record Chat(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("username")] string Username
    );
}