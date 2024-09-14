using System;
using System.Collections.Generic;
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
            List<Conversation> conversations = await apiService.GetAllConversationsAsync();

            foreach (var conversation in conversations)
            {
                Console.WriteLine($"ID: {conversation.Id}, Nombre: {conversation.FullName}, Mensaje: {conversation.LastMessageBody}");
            }

            Console.WriteLine("Todas las conversaciones han sido obtenidas.");
            Console.ReadLine(); // Para que la consola no se cierre inmediatamente
        }
    }

    // Clase para manejar la API
    public class MyApiService
    {
        private static readonly string apiUrl = "https://services.leadconnectorhq.com/conversations/search";
        private static readonly string bearerToken = "pit-c434716d-9fad-42fa-9a74-14554928401f"; // Reemplaza por tu token
        private static readonly string locationId = "7gcRvmSzndyAWZHzYU01"; // Reemplaza con el ID de tu localización

        public async Task<List<Conversation>> GetAllConversationsAsync()
        {
            DateTime today = DateTime.Now.Date;
            DateTime fourAmToday = today.AddHours(4);
            long timestamp = new DateTimeOffset(fourAmToday).ToUnixTimeMilliseconds();

            List<Conversation> allConversations = new List<Conversation>();
            int limit = 100; // Cantidad de conversaciones por página
            long? lastMessageDate = null; // Para ir paginando

            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {bearerToken}");
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.DefaultRequestHeaders.Add("Version", "2021-04-15");

                bool hasMorePages = true;
                while (hasMorePages)
                {
                    // Construir la URL con los parámetros de paginación
                    string requestUrl = $"{apiUrl}" +
                        $"?locationId={locationId}" +
                        $"&limit={limit}" +
                        $"&lastMessageAction=manual" +
                        $"&lastMessageDirection=outbound" +
                        $"&lastMessageType=TYPE_WHATSAPP" +
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

                        // Procesar las conversaciones
                        JArray conversations = (JArray)jsonResponse["conversations"];
                        foreach (var conv in conversations)
                        {

                            if(timestamp<= (long)conv["lastMessageDate"])
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

                        // Actualizar la fecha del último mensaje para la siguiente página
                        if (conversations.Count > 0)
                        {
                            lastMessageDate = (long)conversations[conversations.Count - 1]["lastMessageDate"];
                        }
                        else
                        {
                            hasMorePages = false; // No hay más páginas
                        }

                        // Verificar si hemos alcanzado el total de conversaciones
                        int total = (int)jsonResponse["total"];
                        if (allConversations.Count >= total)
                        {
                            hasMorePages = false;
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Error en la solicitud: {response.StatusCode}");
                        hasMorePages = false;
                    }
                }
            }

            return allConversations;
        }
    }

    // Clase para representar una conversación
    public class Conversation
    {
        public string Id { get; set; }
        public string FullName { get; set; }
        public string Phone { get; set; }
        public string LastMessageBody { get; set; }
        public long LastMessageDate { get; set; }
        // Añade más propiedades según la respuesta de la API si es necesario
    }
}
