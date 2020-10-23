namespace TelegramBridge
{
    class InMessage 
    {
        public string Text {get;set;}
        public string ChatId {get;set;}
        public string UserLogin {get;set;}

        public InMessage(string text, string chatId, string userLogin)
        {
            Text = text;
            ChatId = chatId;
            UserLogin = userLogin;
        }
    }
}