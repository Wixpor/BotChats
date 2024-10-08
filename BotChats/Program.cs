﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace BotChats
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Obteniendo todas las conversaciones...");
            MyApiService apiService = new MyApiService();
            List<Conversation> conversationsWA = await apiService.GetAllConversationsAsync();
            var allMessagesWA = new List<(string userId, string messageId, string sDate)>();
            var cont = 0;
            foreach (var conversationWA in conversationsWA)
            {
                cont++;
                Console.WriteLine($"{cont} de {conversationsWA.Count}");
                var namecontact = conversationWA.FullName;
                var messegesWA = await apiService.GetAllMessagesAsync(conversationWA.Id);
                allMessagesWA.AddRange(messegesWA);
            }

            var countsByUserId = allMessagesWA
            .GroupBy(message => message.userId)
            .Select(group => new { UserId = group.Key, Count = group.Count() })
            .ToList();


            foreach (var user in countsByUserId)
            {
                var nameUser = await apiService.GetUserNameAsync(user.UserId);
                Console.WriteLine($"UserId: {nameUser}, Count: {user.Count}");

            }

            Console.WriteLine("Todas las conversaciones han sido obtenidas.");
            Console.ReadLine();
        }

    }
    public class MyApiService
    {
        private static readonly string apiUrlFindConversations = "https://services.leadconnectorhq.com/conversations/search";
        private static readonly string bearerToken = "pit-c434716d-9fad-42fa-9a74-14554928401f";
        private static readonly string locationId = "7gcRvmSzndyAWZHzYU01";
        private static readonly string apiUrlFindMesseges = "https://services.leadconnectorhq.com/conversations/{0}/messages";
        private static readonly string baseUrl = "https://services.leadconnectorhq.com";


        public async Task<List<Conversation>> GetAllConversationsAsync()
        {
            DateTime today = DateTime.Now.Date;
            DateTime fourAmToday = today.AddHours(4);
            long timestamp = new DateTimeOffset(fourAmToday).ToUnixTimeMilliseconds();

            List<Conversation> allConversations = new List<Conversation>();
            int limit = 100;
            long? lastMessageDate = null;
            int errorCount = 0;
            int maxErrorCount = 10;

            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {bearerToken}");
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.DefaultRequestHeaders.Add("Version", "2021-04-15");

                bool hasMorePages = true;
                while (hasMorePages)
                {
                    try
                    {
                        string requestUrl = $"{apiUrlFindConversations}" +
                            $"?locationId={locationId}" +
                            $"&limit={limit}" +
                            $"&sort=desc" +
                            $"&sortBy=last_message_date" +
                            $"&status=all";

                        if (lastMessageDate.HasValue)
                        {
                            requestUrl += $"&startAfterDate={lastMessageDate.Value}";
                        }

                        HttpResponseMessage response = await client.GetAsync(requestUrl);
                        if (response.IsSuccessStatusCode)
                        {
                            string responseBody = await response.Content.ReadAsStringAsync();
                            JObject jsonResponse = JObject.Parse(responseBody);

                            JArray conversations = (JArray)jsonResponse["conversations"];
                            foreach (var conv in conversations)
                            {
                                if (timestamp <= (long)conv["lastMessageDate"])
                                {
                                    Conversation conversation = new Conversation
                                    {
                                        Id = (string)conv["id"],
                                        FullName = (string)conv["fullName"],
                                        Phone = (string)conv["phone"],
                                        LastMessageBody = (string)conv["lastMessageBody"],
                                        LastMessageDate = (long)conv["lastMessageDate"]
                                    };
                                    allConversations.Add(conversation);
                                }
                                else
                                {
                                    hasMorePages = false;
                                }
                            }

                            if (conversations.Count > 0)
                            {
                                lastMessageDate = (long)conversations[conversations.Count - 1]["lastMessageDate"];
                            }
                            else
                            {
                                hasMorePages = false;
                            }

                            int total = (int)jsonResponse["total"];
                            if (allConversations.Count >= total)
                            {
                                hasMorePages = false;
                            }

                            errorCount = 0;
                        }
                        else
                        {
                            Console.WriteLine($"Error en la solicitud: {response.StatusCode}");
                            errorCount++;

                            if (errorCount >= maxErrorCount)
                            {
                                Console.WriteLine("Número máximo de intentos fallidos alcanzado. Finalizando.");
                                hasMorePages = false;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Excepción: {ex.Message}");
                        errorCount++;

                        if (errorCount >= maxErrorCount)
                        {
                            Console.WriteLine("Número máximo de intentos fallidos alcanzado debido a una excepción. Finalizando.");
                            hasMorePages = false;
                        }
                    }

                    if (errorCount > 0 && hasMorePages)
                    {
                        await Task.Delay(1000); 
                    }
                }
            }

            return allConversations;
        }

        public async Task<List<(string userId, string messageId, string sDate)>> GetAllMessagesAsync(string conversationId)
        {
            DateTime today = DateTime.Now.Date;
            DateTime fourAmToday = today.AddHours(4);

            var allMessages = new List<(string userId, string messageId, string sDate)>();
            int limit = 100;
            string lastMessageId = null;
            int errorCount = 0;
            int maxErrorCount = 10;

            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {bearerToken}");
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.DefaultRequestHeaders.Add("Version", "2021-04-15");

                bool hasMorePages = true;
                while (hasMorePages)
                {
                    try
                    {
                        string requestUrl = string.Format(apiUrlFindMesseges, conversationId);
                        requestUrl +=
                            $"?limit={limit}" +
                            $"&type=TYPE_SMS,TYPE_WHATSAPP" +
                            $"&sort=desc";

                        if (!string.IsNullOrEmpty(lastMessageId))
                        {
                            requestUrl += $"&startAfterDate={lastMessageId}";
                        }

                        HttpResponseMessage response = await client.GetAsync(requestUrl);
                        if (response.IsSuccessStatusCode)
                        {
                            string responseBody = await response.Content.ReadAsStringAsync();
                            JObject jsonResponse = JObject.Parse(responseBody);

                            JArray messages = (JArray)jsonResponse["messages"]["messages"];
                            JObject messagesObject = (JObject)jsonResponse["messages"];

                            if (messagesObject["nextPage"]?.ToString().ToLower() == "false")
                            {
                                hasMorePages = false;
                            }

                            foreach (var message in messages)
                            {
                                var sSource = (string)message["source"];
                                var sFecha = ((string)message["dateAdded"]).Replace("Z", "").Replace("T", " ");
                                DateTime messageDate = DateTime.MinValue;

                                try
                                {
                                    messageDate = DateTime.ParseExact(string.IsNullOrEmpty(sFecha) ? "01/01/2024 00:00:00" : sFecha, "MM/dd/yyyy HH:mm:ss", CultureInfo.InvariantCulture);
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine(ex.ToString());
                                }

                                //messageDate = DateTime.ParseExact(sFecha == "" ? "2024-01-01 00:0:00" : sFecha, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                                string userId = (string)message["userId"];

                                if (fourAmToday <= messageDate)
                                {
                                    if (!string.IsNullOrEmpty(userId) && sSource != "workflow")
                                    {
                                        string messageId = (string)message["id"];
                                        allMessages.Add((userId, messageId, sFecha));
                                    }
                                }
                                else
                                {
                                    hasMorePages = false;
                                    break;
                                }
                            }

                            if (messages.Count > 0)
                            {
                                lastMessageId = (string)messages[messages.Count - 1]["id"];
                            }
                            else
                            {
                                hasMorePages = false;
                            }

                            errorCount = 0;
                        }
                        else
                        {
                            Console.WriteLine($"Error en la solicitud: {response.StatusCode}");
                            errorCount++;

                            if (errorCount >= maxErrorCount)
                            {
                                Console.WriteLine("Número máximo de intentos fallidos alcanzado. Finalizando.");
                                hasMorePages = false;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Excepción: {ex.Message}");
                        errorCount++;

                        if (errorCount >= maxErrorCount)
                        {
                            Console.WriteLine("Número máximo de intentos fallidos alcanzado debido a una excepción. Finalizando.");
                            hasMorePages = false;
                        }
                    }

                    if (errorCount > 0 && hasMorePages)
                    {
                        await Task.Delay(2000);
                    }
                }
            }

            return allMessages;
        }

        public async Task<string> GetUserNameAsync(string userId)
        {
            string apiUrl = $"{baseUrl}/users/{userId}";
            int errorCount = 0;
            int maxErrorCount = 10;

            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {bearerToken}");
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.DefaultRequestHeaders.Add("Version", "2021-04-15");

                while (errorCount < maxErrorCount)
                {
                    try
                    {
                        HttpResponseMessage response = await client.GetAsync(apiUrl);

                        if (response.IsSuccessStatusCode)
                        {
                            string responseBody = await response.Content.ReadAsStringAsync();
                            JObject jsonResponse = JObject.Parse(responseBody);
                            string userName = jsonResponse["name"]?.ToString();

                            errorCount = 0;

                            return userName;
                        }
                        else
                        {
                            Console.WriteLine($"Error: {response.StatusCode}");
                            errorCount++;

                            if (errorCount >= maxErrorCount)
                            {
                                Console.WriteLine("Número máximo de intentos fallidos alcanzado. Finalizando.");
                                return null;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Excepción: {ex.Message}");
                        errorCount++;

                        if (errorCount >= maxErrorCount)
                        {
                            Console.WriteLine("Número máximo de intentos fallidos alcanzado debido a una excepción. Finalizando.");
                            return null;
                        }
                    }

                    await Task.Delay(1000); 
                }
            }

            return null;
        }

    }

    public class Conversation
    {
        public string Id { get; set; }
        public string FullName { get; set; }
        public string Phone { get; set; }
        public string LastMessageBody { get; set; }
        public long LastMessageDate { get; set; }
    }
}
