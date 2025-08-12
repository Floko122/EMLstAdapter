using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace EMLst
{
    internal class MessageChecker
    {
        private static readonly HttpClient httpClient = new HttpClient();
        private string token;
        private int timestamp;
        private string baseurl;
        public static string filePath = "input.txt";
        public string basepath { get; set; }
        private CancellationTokenSource cancellationTokenSource;

        public MessageChecker(string token,string baseurl,string basepath)
        {
            this.token = token;
            this.timestamp = 0;
            this.baseurl = baseurl;
            this.basepath = basepath; 
        }

        public void StartMonitoring()
        {
            // Initialize the cancellation token source
            cancellationTokenSource = new CancellationTokenSource();
            System.IO.File.WriteAllText(basepath + "\\" + filePath, string.Empty);

            // Run the monitoring task on a separate thread
            Task.Run(() => CheckMessages(cancellationTokenSource.Token), cancellationTokenSource.Token);
        }

        public void StopMonitoring()
        {
            // Cancel the monitoring task
            cancellationTokenSource.Cancel();
        }

        public async Task CheckMessages(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested) // This loop can be controlled externally, e.g., with a cancellation token
            {
                System.Diagnostics.Debug.WriteLine($"Try get Message:");
                try
                {
                    object jsonResponse = await MainWindow.RequestAsync($"{baseurl}?action=commands_pending&session_token={token}", null, "GET");

                    if (jsonResponse != null && jsonResponse is Dictionary<string, object> dictionary && dictionary.ContainsKey("commands"))
                    {
                        List<object> commands = ((JsonElement)dictionary["commands"]).Deserialize<List<object>>();
                        List<string> acknowledge = new List<string>();
                        List<string> messages = new List<string>();

                        foreach (object com in commands)
                        {
                            Dictionary<string, object> command = ((JsonElement)com).Deserialize<Dictionary<string, object>>();
                            if (command["type"].ToString().Equals("assign"))
                            {
                                Dictionary<string, object> payload = JsonSerializer.Deserialize<Dictionary<string, object>>(command["payload"].ToString());
                                string player_id = "-1";
                                if (payload["assign_to_player_id"]!=null && payload["assign_to_player_id"].ToString().Length > 0)
                                {
                                    player_id = payload["assign_to_player_id"].ToString();
                                    //Create Message
                                }
                                System.Diagnostics.Debug.WriteLine(command["payload"].ToString());
                                Dictionary<string, object> target = ((JsonElement)payload["target"]).Deserialize<Dictionary<string, object>>();
                                string message = $"203|{payload["game_vehicle_id"]}|{payload["event_game_id"]}|{target["x"]}|{target["y"]}|{player_id}||";
                                messages.Add(message);
                                acknowledge.Add(command["id"].ToString());
                            }
                        }
                        if (messages.Count > 0)
                            {
                            // Write messages to file
                            await WriteMessagesToFileAsync(messages, basepath, filePath);
                            Dictionary<string, object> data = new Dictionary<string,object>();
                            data["session_token"] = token;
                            data["command_ids"] = acknowledge;
                            await MainWindow.RequestAsync($"{baseurl}?action=commands_ack", data, "POST");
                        }
                    }
                }
                catch (HttpRequestException e)
                {
                    Console.WriteLine($"Request error: {e.Message}");
                    // Handle request errors, possibly retry after a delay
                }
                catch (JsonException e)
                {
                    Console.WriteLine($"JSON parsing error: {e.Message}");
                    // Handle JSON parsing errors
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Unexpected error: {e.Message}");
                }

                // Wait before retrying, or implement a retry mechanism
                await Task.Delay(5000); // Wait 5 seconds before the next attempt
            }
        }

        public static async Task WriteMessagesToFileAsync(List<string> messages,string basepath,string filePath)
        {
            bool written = false;

            while (!written)
            {
                try
                {
                    using (StreamWriter writer = new StreamWriter(basepath+"\\"+filePath, append: true))
                    {
                        foreach (var message in messages)
                        {
                            await writer.WriteLineAsync(message);
                        }
                    }
                    written = true; // Successfully written to the file
                }
                catch (IOException)
                {
                    Console.WriteLine("File is not available, retrying...");
                    await Task.Delay(100);
                }
            }
        }
    }
}
