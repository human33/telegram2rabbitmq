using System.Text.Json.Serialization;

namespace TelegramBridge.Telegram
{
    public record Update
    (
        [property: JsonPropertyName("update_id")] long UpdateId,
        [property: JsonPropertyName("message")] Message Message
    );
}