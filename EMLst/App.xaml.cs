using Microsoft.UI.Xaml;
using System.IO;
using System.Text;
using System;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace EMLst
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private static Window m_window;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            m_window = new MainWindow();
            m_window.Activate();
        }
        public static void log(string message)
        {
            using (var writer = new StreamWriter("error.log", append: true, encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
            {
                writer.WriteLine(DateTime.Now.ToString()+": "+message);
            }
        }
    }
}
