
using NetworkService.Model;
using NetworkService.Model.NetworkService.Model;
using NetworkService.Views;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Windows;
using System.Windows.Input;

namespace NetworkService.ViewModel
{
    public class MainWindowViewModel : BaseViewModel
    {
        // ── Centralna lista entiteta ──────────────────────────────────
        public static ObservableCollection<ServerEntity> Entities { get; set; }
            = new ObservableCollection<ServerEntity>();

        // ── Unapred definisani tipovi (T6) ────────────────────────────
        public static ServerType WebServer = new ServerType("Web server", "/Images/web_server.png");
        public static ServerType FileServer = new ServerType("File server", "/Images/file_server.png");
        public static ServerType DatabaseServer = new ServerType("Database server", "/Images/database_server.png");

        public static string LogFilePath = "Log.txt";

        // ── Callback za confirm dialog (postavlja MainWindow) ─────────
        //public static Action<ConfirmDialogViewModel> ShowConfirmDialog { get; set; }

        // ── Navigacija ───────────────────────────────────────────────
        private object _currentView;
        private string _currentViewTitle = "Home";
        private string _previousViewTitle = "";

        private readonly HomeView _homeView;
        private readonly NetworkEntitiesView _entitiesView;
        private readonly NetworkDisplayView _networkView;
        private readonly GraphView _graphView;

        public object CurrentView
        {
            get => _currentView;
            set { _currentView = value; OnPropertyChanged(nameof(CurrentView)); }
        }

        public string CurrentViewTitle
        {
            get => _currentViewTitle;
            set { _currentViewTitle = value; OnPropertyChanged(nameof(CurrentViewTitle)); }
        }

        public Visibility HomeButtonVisibility =>
            CurrentViewTitle == "Home" ? Visibility.Collapsed : Visibility.Visible;

        public string NetworkNavColor => CurrentViewTitle == "Network display" ? "#FFFFFF" : "#9CA3AF";
        public string EntitiesNavColor => CurrentViewTitle == "Entities list" ? "#FFFFFF" : "#9CA3AF";
        public string GraphNavColor => CurrentViewTitle == "Graph view" ? "#FFFFFF" : "#9CA3AF";

        // ── Komande ──────────────────────────────────────────────────
        public ICommand NavigateHomeCommand { get; }
        public ICommand NavigateNetworkCommand { get; }
        public ICommand NavigateEntitiesCommand { get; }
        public ICommand NavigateGraphCommand { get; }
        public ICommand UndoCommand { get; }

        public MainWindowViewModel()
        {
            // Kreiranje view instanci — čuvamo ih da sadržaj ne nestaje pri navigaciji
            _homeView = new HomeView();
            _entitiesView = new NetworkEntitiesView();
            _networkView = new NetworkDisplayView();
            _graphView = new GraphView();

            CurrentView = _homeView;

            NavigateHomeCommand = new RelayCommand(_ => NavigateTo("Home"));
            NavigateNetworkCommand = new RelayCommand(_ => NavigateTo("Network display"));
            NavigateEntitiesCommand = new RelayCommand(_ => NavigateTo("Entities list"));
            NavigateGraphCommand = new RelayCommand(_ => NavigateTo("Graph view"));
            UndoCommand = new RelayCommand(_ => ExecuteUndo());

            // TCP listener mora da se pokrene odmah pri startu aplikacije
            CreateListener();
        }

        // ── Navigaciona logika ────────────────────────────────────────
        private void NavigateTo(string viewName)
        {
            _previousViewTitle = CurrentViewTitle;
            CurrentViewTitle = viewName;

            switch (viewName)
            {
                case "Home":
                    CurrentView = _homeView;
                    break;
                case "Network display":
                    CurrentView = _networkView;
                    break;
                case "Entities list":
                    CurrentView = _entitiesView;
                    break;
                case "Graph view":
                    CurrentView = _graphView;
                    break;
                default:
                    CurrentView = _homeView;
                    break;
            }

            OnPropertyChanged(nameof(HomeButtonVisibility));
            OnPropertyChanged(nameof(NetworkNavColor));
            OnPropertyChanged(nameof(EntitiesNavColor));
            OnPropertyChanged(nameof(GraphNavColor));
        }

        private void ExecuteUndo()
        {
            // Delegiramo Undo aktivnom View-u
            // Svaki ViewModel ima svoj Undo stack
        }

        // ── TCP Listener (komunikacija sa MeteringSimulator-om) ───────
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
                            // Odgovaramo stvarnim brojem entiteta u listi
                            byte[] data = System.Text.Encoding.ASCII.GetBytes(
                                Entities.Count.ToString());
                            stream.Write(data, 0, data.Length);
                        }
                        else
                        {
                            // Format poruke: "Entitet_N:vrednost"
                            ProcessMeasurement(incoming);
                        }
                    }, null);
                }
            });

            listeningThread.IsBackground = true;
            listeningThread.Start();
        }

        // ── Obrada primljenog merenja ─────────────────────────────────
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