using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace TelegramBridge.Telegram
{
    public class BotClient
    {
        private readonly string _token;
        private readonly HttpClient _client;
        
        public BotClient(string telegramApiBaseAddress, string token)
        {
            _token = token;
            _client = new HttpClient();
            _client.BaseAddress = new Uri(telegramApiBaseAddress);
        }
        
        
        /// <summary>
        /// Gets new message and confirms the message with offset-1
        /// </summary>
        /// <returns></returns>
        public async Task<Update> GetMessage(int offset, int timeout)
        {
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("offset", offset.ToString()),
                new KeyValuePair<string, string>("limit", "1"),
                new KeyValuePair<string, string>("timeout", timeout.ToString()),
            });
            var result = await _client.PostAsync($"/bot{_token}/getUpdates", content);
            string resultContent = await result.Content.ReadAsStringAsync();

            List<Update> updates = System.Text.Json.JsonSerializer.Deserialize<List<Update>>(resultContent);

            if (updates != null && updates.Count > 0)
            {
                return updates.First();
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Sends the message to the Telegram
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public async Task<Message> SendMessage(MessageToSend message)
        {
            var content = JsonContent.Create<MessageToSend>(message);
            var result = await _client.PostAsync($"/bot{_token}/sendMessage", content);
            string resultContent = await result.Content.ReadAsStringAsync();

            Message sentMessage = System.Text.Json.JsonSerializer.Deserialize<Message>(resultContent);
            return sentMessage;
        }
    }
}