using System.Text.Json.Serialization;

namespace TelegramBridge.Telegram
{
    public record ApiAnswer<T>
    (
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("result")] T Result
    );
}