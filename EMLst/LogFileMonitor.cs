using Microsoft.Windows.Security.AccessControl;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace EMLst
{
    internal class LogFileMonitor
    {
        private static readonly HttpClient httpClient = new HttpClient();
        private string logFilePath = "logfile.txt";
        private long lastPosition = 0; // Keep track of the last read position
        private string marker = "!LSTcmd::";
        private string player_marker = "advertising game info: players=";
        private CancellationTokenSource cancellationTokenSource;
        private List<Dictionary<string, object>> vehicles= new List<Dictionary<string, object>>();
        private List<Dictionary<string, object>> hospitals = new List<Dictionary<string, object>>();
        private List<Dictionary<string, object>> events = new List<Dictionary<string,object>>();
        private Dictionary<string, string> players = new Dictionary<string, string>();

        public string token { get; set; }
        public string basepath { get; set; }
        public string baseurl { get; set; }
        public string mod_id { get; set; }
        public void StartMonitoring()
        {
            // Initialize the cancellation token source
            cancellationTokenSource = new CancellationTokenSource();
            System.IO.File.WriteAllText(basepath + "\\" + logFilePath, string.Empty);

            // Run the monitoring task on a separate thread
            Task.Run(() => MonitorLogFile(cancellationTokenSource.Token), cancellationTokenSource.Token);
        }

        public void StopMonitoring()
        {
            // Cancel the monitoring task
            cancellationTokenSource.Cancel();
        }

        private async Task MonitorLogFile(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Open the file in read mode
                    using (FileStream fs = new FileStream(basepath+"\\"+logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        // Set the position to the last saved position
                        fs.Seek(lastPosition, SeekOrigin.Begin);

                        using (StreamReader sr = new StreamReader(fs, Encoding.UTF8))
                        {
                            string line;
                            while ((line = await sr.ReadLineAsync()) != null)
                            {
                                if (line.StartsWith(marker))
                                {
                                    // Process the line that starts with "SendPosition:"
                                    ProcessLine(line);
                                }

                                if (line.StartsWith(player_marker))
                                {
                                    // Process the line that starts with "SendPosition:"
                                    ProcessPlayerLine(line);
                                }
                            }

                            // Update the last position after reading all lines
                            lastPosition = fs.Position;
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    // Handle exceptions, such as file access issues
                    Console.WriteLine($"An error occurred: {ex.Message}");
                }
                await trySendToServer();//We need to wait for it to avoid new entries being deleted after sending
                // Wait for a short time before checking again
                await Task.Delay(1000, cancellationToken);
            }
        }

        private void ProcessPlayerLine(string line)
        {
            // Add your logic here to process the line
            string message = line.Substring(player_marker.Length);
            string[] playerstr = message.Split(",");
            Dictionary<string,string> playerDict = new Dictionary<string,string>();
            for (int i = 0; i < playerstr.Length; i++)
            {
                playerDict[i.ToString()] = playerstr[i].Trim();
            }
            players = playerDict;
        }

        private void ProcessLine(string line)
        {
            // Add your logic here to process the line
            string message = line.Substring(marker.Length);
            char type = message[0];
            Dictionary<string, object> data = null;
            try
            {
                data = JsonSerializer.Deserialize<Dictionary<string, object>>(message.Substring(1));
            }
            catch (JsonException e)
            { }
            System.Diagnostics.Debug.WriteLine($"{type}--{message}");
            switch (type)
            {
                case 'v':
                    {
                        if (data != null)
                            vehicles.Add(data);
                        break;
                    }
                case 'e':
                    {
                        if (data != null)
                            events.Add(data);
                        break;
                    }
                case 'h':
                    {
                        if (data != null)
                            hospitals.Add(data);
                        break;
                    }
                case 'i':
                    {
                        //TODO send code back
                        //Anlyse players
                        MessageChecker.WriteMessagesToFileAsync(["42|" + message.Substring(1)], basepath, MessageChecker.filePath);
                        break;
                    }
            }
        }
        private async Task trySendToServer()
        {
            if ((hospitals.Count>0 || events.Count>0 || vehicles.Count>0) && token.Length > 0)
            {
                try
                {
                    Dictionary<string,object> data= new Dictionary<string,object>();
                    data["session_token"] = token;
                    data["mod_id"] = mod_id;
                    if (vehicles.Count > 0)
                    {
                        data["vehicles"] = vehicles;
                    }
                    if (hospitals.Count > 0)
                    {
                        data["hospitals"] = hospitals;
                    }
                    if (events.Count > 0)
                    {
                        data["events"] = events;
                    }

                    if (players != null && players.Count>0)
                    {
                        data["players"] = players;
                    }

                    // Deserialize the JSON response to a general object (Dictionary/List)
                    //System.Diagnostics.Debug.WriteLine($"Presending json: {data}");
                    object jsonResponse = await MainWindow.RequestAsync(baseurl+"?action=sync", data, "POST");
                    if (jsonResponse != null && jsonResponse is Dictionary<string, object> dictionary && 
                        dictionary.ContainsKey("ok") && dictionary["ok"].ToString().Equals("True")
                            )
                    {
                        System.Diagnostics.Debug.WriteLine($"Successful sent {dictionary["ok"]}");
                        events.Clear();
                        hospitals.Clear();
                        vehicles.Clear();//Only delete if was sent
                        players.Clear();
                    }
                }
                catch (HttpRequestException e)
                {
                    System.Diagnostics.Debug.WriteLine($"Request error: {e.Message}");
                    Console.WriteLine($"Request error: {e.Message}");
                }
                catch (JsonException e)
                {
                    // Handle JSON parsing errors
                    Console.WriteLine($"JSON parsing error: {e.Message}");
                    System.Diagnostics.Debug.WriteLine($"JSON parsing error: {e.Message}");
                }
            }
            else
            {
                //System.Diagnostics.Debug.WriteLine("Nothing on the log");

            }
        }
    }
}
