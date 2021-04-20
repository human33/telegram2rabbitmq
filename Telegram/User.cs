using System.Text.Json.Serialization;

namespace TelegramBridge.Telegram
{
    public record User
    (
        [property: JsonPropertyName("id")] long Id,
        [property: JsonPropertyName("username")] string Username
    );
}