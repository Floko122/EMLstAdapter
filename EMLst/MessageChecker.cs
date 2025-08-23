using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
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
                                string message = $"203|{payload["game_vehicle_id"]}|{payload["event_game_id"]}|{target["x"]}|{target["y"]}|{player_id}|{payload["mode"]}|";
                                messages.Add(message);
                                acknowledge.Add(command["id"].ToString());
                            }else if (command["type"].ToString().Equals("unassign"))
                            {
                                Dictionary<string, object> payload = JsonSerializer.Deserialize<Dictionary<string, object>>(command["payload"].ToString());
                                string message = $"204|{payload["game_vehicle_id"]}|";
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

        public static async Task WriteMessagesToFileAsync(List<string> messages, string basepath, string filePath)
        {
            var path = Path.Combine(basepath, filePath);
            bool written = false;

            while (!written)
            {
                try
                {
                    // Ensure we don't start appending with two empty lines at the end already.
                    TrimOneTrailingNewLineIfDouble(path);

                    using (var writer = new StreamWriter(path, append: true, encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))//
                    {
                        foreach (var message in messages)
                        {
                            await writer.WriteLineAsync(message);
                        }

                        // Leave exactly one *blank* line at EOF.
                        await writer.WriteLineAsync("+");// equivalent to WriteLineAsync("")
                    }

                    written = true;
                }
                catch (IOException)
                {
                    Console.WriteLine("File is not available, retrying...");
                    await Task.Delay(100);
                }
            }
        }

        /// <summary>
        /// If the file currently ends with two newline sequences in a row (i.e., a blank last line),
        /// trim one newline sequence so we start appending cleanly. Uses Environment.NewLine.
        /// </summary>
        private static void TrimOneTrailingNewLineIfDouble(string path)
        {
            if (!File.Exists(path))
                return;

            var nl = Encoding.UTF8.GetBytes(Environment.NewLine);
            var plus = Encoding.UTF8.GetBytes("+");
            var need = nl.Length * 2+plus.Length;

            using (var fs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            {
                if (fs.Length < need)
                    return;

                // Read the last 2 newline sequences' worth of bytes.
                fs.Seek(-need, SeekOrigin.End);
                var tail = new byte[need];
                var read = fs.Read(tail, 0, tail.Length);
                if (read != tail.Length)
                    return;

                bool endsWithDouble =
                    StartsWithSlice(tail, 0, nl) &&
                    StartsWithSlice(tail, nl.Length, plus) &&
                    StartsWithSlice(tail, nl.Length+plus.Length, nl);
                Console.WriteLine($"Rewriting!: {endsWithDouble}");

                if (endsWithDouble)
                {
                    // Trim exactly one newline sequence (so EOF ends with a single newline).
                    fs.SetLength(fs.Length - nl.Length-plus.Length);
                }
            }
        }

        private static bool StartsWithSlice(byte[] buffer, int offset, byte[] slice)
        {
            if (offset + slice.Length > buffer.Length) return false;
            for (int i = 0; i < slice.Length; i++)
                if (buffer[offset + i] != slice[i]) return false;
            return true;
        }
    }
}
