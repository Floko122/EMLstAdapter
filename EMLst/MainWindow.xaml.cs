using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage.Pickers;
using Microsoft.Win32;
using System.IO;
using System.Text;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace EMLst
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private const string FolderPathKey = "SelectedFolderPath";
        private const string ConfigPathKey = "SelectedConfigPath";
        private LogFileMonitor logFileMonitor;
        private MessageChecker messageChecker;
        private static readonly HttpClient httpClient = new HttpClient();
        private string baseurl = "";
        private string basepath = "";
        private string config_path = "";
        private Dictionary<string, object> config = null;
        private string token = "";
        private Boolean started = false;
        public MainWindow()
        {
            this.InitializeComponent();
            LoadSavedFolderPath();
            this.Closed += ClosedWindow;
            MainWindow_Loaded();
            if (config_path.Length > 0)
            {
                loadConfig();
            }
        }
        private void StartMonitoring()
        {
            StartLogFileMonitoring();
            StartRequestMonitoring();
            started = true;
        }
        private void StartLogFileMonitoring()
        {
            logFileMonitor = new LogFileMonitor();
            logFileMonitor.baseurl = baseurl;
            logFileMonitor.basepath = basepath;
            logFileMonitor.token = token;
            logFileMonitor.mod_id = config["mod_id"].ToString();
            logFileMonitor.StartMonitoring();
        }
        private void StartRequestMonitoring()
        {
            messageChecker = new MessageChecker(token,baseurl,basepath);
            if(config != null && config.ContainsKey("sync_path"))
            {
                MessageChecker.filePath = config.GetValueOrDefault("sync_path",MessageChecker.filePath).ToString();
            }
            messageChecker.StartMonitoring();
        }

        private void MainWindow_Loaded()
        {
            // Measure the minimum size required by the content
            Size desiredSize = MeasureContentSize(this.Content as FrameworkElement);
            this.AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(0, 0, (int)desiredSize.Width,(int)desiredSize.Height));
        }
        private void loadConfig()
        {
            try
            {
                config = JsonSerializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(config_path));
                baseurl = config["server_path"].ToString();
                System.Diagnostics.Debug.WriteLine($"loaded config url: {baseurl}");
            }
            catch (JsonException e)
            {
                // Handle JSON parsing errors
                Console.WriteLine($"JSON parsing error: {e.Message}");
                System.Diagnostics.Debug.WriteLine($"JSON parsing error: {e.Message}");
            }
            catch (IOException e)
            {
                // Handle JSON parsing errors
                Console.WriteLine($"IO parsing error: {e.Message}");
                System.Diagnostics.Debug.WriteLine($"IO error: {e.Message}");
            }
        }
        private Size MeasureContentSize(FrameworkElement element)
        {
            if (element == null)
                return new Size(0, 0);

            // Measure the element's size with no constraints
            element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            // Return the desired size
            return element.DesiredSize;
        }
        // Event handler for "Request Token" button click
        private async void RequestTokenButton_Click(object sender, RoutedEventArgs e)
        {
            if (!started && config!=null)
            {
                object response = await RequestAsync(baseurl + "?action=session_create", config["request_data"],"POST"); // Example placeholder
                if (response is Dictionary<string, object> dictionary && dictionary.ContainsKey("session_token"))
                {
                    SaveTokenButton.Foreground = new SolidColorBrush(Colors.Green);
                    TokenTextBox.Text = dictionary["session_token"].ToString();
                    TokenTextBox.IsReadOnly = true;
                    token = dictionary["session_token"].ToString();
                    StartMonitoring();
                }
            }
        }


        public static async Task<object> RequestAsync(string url, object data, string mode)
        {
            string debug_response = "";
            try
            {
                HttpResponseMessage response;
                if (mode == "POST")
                {
                    string jsonString = JsonSerializer.Serialize(data);
                    var content = new StringContent(jsonString, Encoding.UTF8, "application/json");

                    // Send the POST request
                    response = await httpClient.PostAsync(url, content);
                    //System.Diagnostics.Debug.WriteLine($"Response data: {jsonString}__{url}");
                }
                else
                {
                    // Example: Sending a POST request to the API
                    response = await httpClient.GetAsync(url);
                }
                //System.Diagnostics.Debug.WriteLine($"Response result: {url},{data}");
                // Ensure the request was successful
                response.EnsureSuccessStatusCode();

                // Read the response content
                string responseBody = await response.Content.ReadAsStringAsync();
                debug_response = responseBody;

                // Deserialize the JSON response to a general object (Dictionary/List)
                object jsonResponse = JsonSerializer.Deserialize<Dictionary<string, object>>(responseBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                //System.Diagnostics.Debug.WriteLine($"Response result: {responseBody},{jsonResponse}");
                return jsonResponse;
            }
            catch (HttpRequestException e)
            {
                // Handle any errors that occur during the request
                Console.WriteLine($"Request error: {e.Message}");
                System.Diagnostics.Debug.WriteLine($"Request error: {e.Message}, {url}");
                return null;
            }
            catch (JsonException e)
            {
                // Handle JSON parsing errors
                Console.WriteLine($"JSON parsing error: {e.Message}");
                System.Diagnostics.Debug.WriteLine($"JSON parsing error: {e.Message}, {debug_response}");
                return null;
            }
        }

        // Event handler for "Save Token" button click
        private async void SaveTokenButton_Click(object sender, RoutedEventArgs e)
        {
            if (!started)
            {
                var tok = TokenTextBox.Text;
                /*object response = await RequestJsonAsync(baseurl + "/checktoken?token=" + tok);
                if (response is Dictionary<string, object> dictionary && dictionary.ContainsKey("Status") 
                    && dictionary["Status"].ToString().Equals("OK", StringComparison.OrdinalIgnoreCase))
                {
                    SaveTokenButton.Foreground = new SolidColorBrush(Colors.Green);*/
                    TokenTextBox.IsReadOnly = true;
                    token = tok;
                    StartMonitoring();
                //TODO maybe readd token checking
                /*}
                else
                {
                    SaveTokenButton.Foreground = new SolidColorBrush(Colors.Red);
                }*/
            }
        }

        // Event handler for "Choose File" button click
        private async void ChooseFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (!started)
            {
                FolderPicker picker = new FolderPicker();
                picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                picker.FileTypeFilter.Add("*");
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

                var file = await picker.PickSingleFolderAsync();
                if (file != null)
                {
                    FilePathTextBlock.Text = file.Path;
                    SaveFolderPath(file.Path);
                }

            }
        }


        // Event handler for "Choose File" button click
        private async void ChooseConfigButton_Click(object sender, RoutedEventArgs e)
        {
            if (!started)
            {
                FileOpenPicker picker = new FileOpenPicker();
                picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                picker.FileTypeFilter.Add(".cfg");
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

                var file = await picker.PickSingleFileAsync();
                if (file != null)
                {
                    FilePathTextBlock.Text = file.Path;
                    SaveConfigPath(file.Path);
                }
                loadConfig();
            }
        }

        private void ClosedWindow(object sender,WindowEventArgs e)
        {
            logFileMonitor?.StopMonitoring();
        }


        private void SaveFolderPath(string path)
        {
            //var localSettings = ApplicationData.Current.LocalSettings;
            //localSettings.Values[FolderPathKey] = path;
            this.basepath = path;
            Registry.SetValue(@"HKEY_CURRENT_USER\Software\EMLst", FolderPathKey, path);
        }

        private void SaveConfigPath(string path)
        {
            //var localSettings = ApplicationData.Current.LocalSettings;
            //localSettings.Values[FolderPathKey] = path;
            this.basepath = path;
            Registry.SetValue(@"HKEY_CURRENT_USER\Software\EMLst", ConfigPathKey, path);
        }
        private void LoadSavedFolderPath()
        {
            string myPreference = (string)Registry.GetValue(@"HKEY_CURRENT_USER\Software\EMLst", 
                FolderPathKey, "C:\\Program Files\\sixteentons entertainment\\Emergency4");

            if (!string.IsNullOrEmpty(myPreference))
            {
                FilePathTextBlock.Text = myPreference;
                this.basepath = myPreference;
            }

            string configPreference = (string)Registry.GetValue(@"HKEY_CURRENT_USER\Software\EMLst",
                ConfigPathKey, "config.cfg");

            if (!string.IsNullOrEmpty(configPreference))
            {
                FileConfigTextBlock.Text = configPreference;
                this.config_path = configPreference;
            }
        }

    }
}
