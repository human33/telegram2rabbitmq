using System.Text.Json.Serialization;

namespace TelegramBridge.Telegram
{
    public record Message
    (
        [property: JsonPropertyName("message_id")] long MessageId,
        [property: JsonPropertyName("from")] User From,
        [property: JsonPropertyName("chat")] Chat Chat,
        [property: JsonPropertyName("date")] long DateUnixTime,
        [property: JsonPropertyName("text")] string Text
    );
}