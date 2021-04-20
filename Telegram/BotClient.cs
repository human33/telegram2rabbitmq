using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
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
        public async Task<Update> GetAndConfirmUpdateAsync(long? offset = null, long? timeout = null)
        {
            byte argumentsLength = 1;
            if (offset != null)
            {
                argumentsLength += 1;
            }
            if (timeout != null)
            {
                argumentsLength += 1;
            }

            var requestBody = new KeyValuePair<string, string>[argumentsLength];
            argumentsLength -= 1;
            requestBody[argumentsLength] = new KeyValuePair<string, string>("limit", "1");
            
            if (offset != null)
            {
                argumentsLength -= 1;
                requestBody[argumentsLength] = new KeyValuePair<string, string>("offset", offset.ToString());
            }
            if (timeout != null)
            {
                argumentsLength -= 1;
                requestBody[argumentsLength] = new KeyValuePair<string, string>("timeout", timeout.ToString());
            }
            
            var content = new FormUrlEncodedContent(requestBody);
            var result = await _client.PostAsync($"/bot{_token}/getUpdates", content);
            string resultContent = await result.Content.ReadAsStringAsync();

            var answer = System.Text.Json.JsonSerializer.Deserialize<ApiAnswer<List<Update>>>(resultContent);

            if (answer != null && answer.Ok)
            {
                List<Update> updates = answer.Result;
                
                if (updates is {Count: > 0})
                {
                    return updates.First();
                }
                else
                {
                    return null;
                }
            }
            else
            {
                throw new ExternalException("Telegram api returned error: " + resultContent);
            }
        }
        
        

        /// <summary>
        /// Sends the message to the Telegram
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public async Task<Message> SendMessageAsync(MessageToSend message)
        {
            var content = JsonContent.Create<MessageToSend>(message);
            var result = await _client.PostAsync($"/bot{_token}/sendMessage", content);
            string resultContent = await result.Content.ReadAsStringAsync();

            Message sentMessage = System.Text.Json.JsonSerializer.Deserialize<Message>(resultContent);
            return sentMessage;
        }
    }
}