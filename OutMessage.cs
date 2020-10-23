namespace TelegramBridge
{
    public class OutMessage
    {
        public string Text {get;set;}
        public string ChatId {get;set;}

        public OutMessage(string text, string chatId)
        {
            Text = text;
            ChatId = chatId;
        }

        public OutMessage() {}
    }
}