using NetworkService.Model;
using NetworkService.Model.NetworkService.Model;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace NetworkService.ViewModel
{
    public class MainWindowViewModel
    {
        // Centralna lista svih entiteta — deli se između ViewModela
        public static ObservableCollection<ServerEntity> Entities { get; set; }
            = new ObservableCollection<ServerEntity>();

        // Unapred definisani tipovi (T6)
        public static ServerType WebServer = new ServerType("Web server", "/Images/web_server.png");
        public static ServerType FileServer = new ServerType("File server", "/Images/file_server.png");
        public static ServerType DatabaseServer = new ServerType("Database server", "/Images/database_server.png");

        public static string LogFilePath = "Log.txt";

        public MainWindowViewModel()
        {
            CreateListener();
        }

        private void CreateListener()
        {
            var tcp = new TcpListener(IPAddress.Any, 25675);
            tcp.Start();

            var listeningThread = new Thread(() =>
            {
                while (true)
                {
                    var tcpClient = tcp.AcceptTcpClient();
                    ThreadPool.QueueUserWorkItem(param =>
                    {
                        NetworkStream stream = tcpClient.GetStream();
                        byte[] bytes = new byte[1024];
                        int i = stream.Read(bytes, 0, bytes.Length);
                        string incoming = System.Text.Encoding.ASCII.GetString(bytes, 0, i);

                        if (incoming.Equals("Need object count"))
                        {
                            // Šaljemo stvaran broj entiteta
                            byte[] data = System.Text.Encoding.ASCII.GetBytes(
                                Entities.Count.ToString());
                            stream.Write(data, 0, data.Length);
                        }
                        else
                        {
                            // Format: "Entitet_N:vrednost"
                            ProcessMeasurement(incoming);
                        }
                    }, null);
                }
            });

            listeningThread.IsBackground = true;
            listeningThread.Start();
        }

        private void ProcessMeasurement(string message)
        {
            try
            {
                // Parsiranje "Entitet_N:vrednost"
                var parts = message.Split(':');
                if (parts.Length != 2) return;

                int index = int.Parse(parts[0].Replace("Entitet_", ""));
                double value = double.Parse(parts[1],
                    System.Globalization.CultureInfo.InvariantCulture);

                App.Current.Dispatcher.Invoke(() =>
                {
                    if (index < 0 || index >= Entities.Count) return;

                    var entity = Entities[index];
                    entity.LastMeasuredValue = value;

                    // Upisivanje u Log.txt
                    string logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | " +
                                      $"ID: {entity.Id} | " +
                                      $"Name: {entity.Name} | " +
                                      $"Value: {value}%";
                    File.AppendAllText(LogFilePath, logEntry + Environment.NewLine);
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing measurement: {ex.Message}");
            }
        }
    }
}