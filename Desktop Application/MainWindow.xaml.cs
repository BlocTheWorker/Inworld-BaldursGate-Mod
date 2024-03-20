using BaldursGateInworld.Manager;
using BaldursGateInworld.Overlay;
using BaldursGateInworld.Util;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Timer = System.Timers.Timer;

namespace BaldursGateInworld
{
    public class ApplicationViewModel : INotifyPropertyChanged
    {
        private string _key;
        public string Key
        {
            get { return _key; }
            set
            {
                if (_key != value)
                {
                    _key = value;
                    OnPropertyChanged(nameof(ApplicationViewModel.Key));
                    TextChanged?.Invoke(true, _key);
                }
            }
        }

        private string _secret;
        public string Secret
        {
            get { return _secret; }
            set
            {
                if (_secret != value)
                {
                    _secret = value;
                    OnPropertyChanged(nameof(ApplicationViewModel.Secret));
                    TextChanged?.Invoke(false, _secret);
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public delegate void TextChangedEventHandler(bool isKey, string data);
        public event TextChangedEventHandler? TextChanged;
    }
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly string PROJECT_NAME = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
        private readonly Style unhideStyle = (Style)Application.Current.FindResource("UnhideStyle");
        private readonly Style hideStyle = (Style)Application.Current.FindResource("HideStyle");
        private readonly int port = 3001;
        private Timer timer;
        private Process process;
        private ApplicationViewModel context;
        private InworldSocket socket;
        private OverlayLogic _activeOverlay;
        private bool isKeyHidden = false, isSecretHidden = false;
        private string _screenText;
        private DateTime _lastWriteTime = DateTime.Now;
        public MainWindow()
        {
            timer = new Timer(10000);
            timer.AutoReset = false;
            timer.Elapsed += OnTimedEvent;
            socket = new InworldSocket(port);
            context = new ApplicationViewModel();
            context.TextChanged += OnPasswordVisiblyChanged;
            DataContext = context;
            InitializeComponent();
            ReadOldValues();
            RunUITasks();
        }

        private void OnPasswordVisiblyChanged(bool isKey, string data)
        {
            if (isKey)
            {
                APIKeyPassBox.Password = data;
            }
            else
            {
                APISecretPassBox.Password = data;
            }
        }

        private void ReadOldValues()
        {
            var credentials = CredentialUtil.GetCredential();
            if (string.IsNullOrEmpty(credentials.Secret))
            {
                context.Key = "Your Inworld API Key";
                context.Secret = "Your Inworld API Secret";
                isSecretHidden = false;
                isKeyHidden = false;
            }
            else
            {
                context.Key = credentials.Key;
                context.Secret = credentials.Secret;
                isSecretHidden = true;
                isKeyHidden = true;
            }

            UpdateKeyValue(isSecretHidden, false, APIKeyEye);
            UpdateKeyValue(isKeyHidden, true, APISecretEye);
        }

        private void RunUITasks()
        {
            new Task(() => { StartOverlayCheck(); }).Start();
            new Task(() => { CheckWorld(); }).Start();
            new Task(() =>
            {
                while (true)
                {
                    if (_activeOverlay == null) continue;

                    double x = 0; double y = 0;
                    var pos = InputManager.Instance.GetMousePosition();
                    x = pos.X;
                    y = pos.Y;
                    _activeOverlay.SetMousePos(x, y);

                    InputManager.Instance.PollKeyData();

                    if (InputManager.Instance.IsKeyDown(SharpDX.DirectInput.Key.F))
                    {
                        _activeOverlay.TriggerPanel();
                    }

                    _activeOverlay.SetText(_screenText);
                    AudioManager.Instance.TickSoundManager();
                    Thread.Sleep(10);
                }
            }).Start();
        }

        private void CheckWorld()
        {
            while (true)
            {
                WorldManager.Instance.ReadWorld();
                Thread.Sleep(500);
                if (socket != null)
                {
                    socket.TriggerLocationIfNeeded();
                }
            }
        }

        private void StartOverlayCheck()
        {
            _activeOverlay = new OverlayLogic("bg3_dx11");
            _activeOverlay.OnGameProcessChange += OnGameProcessChange;
            _activeOverlay.OnCharacterAction += OnCharacterAction;
            _activeOverlay.OnHookedToGame += OnOverlayHookedToGame;
            _activeOverlay.StartOverlay();
        }

        private void OnCharacterAction(string type, string id)
        {
            if (type == "connect")
            {
                Logger.Instance.Log("Requesting to connect. Id: " + id);
                _activeOverlay.TriggerPanel();
                socket.OnReceivedText += Socket_OnReceivedText;
                socket.ConnectToCharacter(id);
            }
            else if (type == "disconnect")
            {
                Logger.Instance.Log("Requesting to disconnect.");
                _activeOverlay.TriggerPanel();
                socket.OnReceivedText -= Socket_OnReceivedText;
                socket.Disconnect();
            }
        }

        private void OnOverlayHookedToGame()
        {
            new Task(() => { StartServer(); }).Start();
            socket.Connect();
            Application.Current.Dispatcher.Invoke(() =>
            {
                ServerStatus.Source = new BitmapImage(new Uri("pack://application:,,,/Resource/Images/UI/running.png"));
            });
        }

        private void Socket_OnReceivedText(string message)
        {
            timer.Stop();
            if (DateTime.Now.Subtract(_lastWriteTime).TotalSeconds >= 3)
            {
                _screenText = "";
            }
            _lastWriteTime = DateTime.Now;
            _screenText += message;
            timer.Interval = 3000 + (_screenText.Length * 100);
            timer.Start();
        }

        private void OnTimedEvent(Object source, ElapsedEventArgs e)
        {
            _screenText = "";
        }

        private void StartServer()
        {
            try
            {
                KillNodeJsProcesses();
                Logger.Instance.Log("[CONSOLE] Starting the connector..");
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = "cmd.exe";
                startInfo.Arguments = "/C cd " + System.AppDomain.CurrentDomain.BaseDirectory + "Connector && .\\node\\node.exe client.js" + " --port=" + port + " --key=" + context.Key + " --secret=" + context.Secret;
                startInfo.UseShellExecute = false;
                startInfo.RedirectStandardError = true;
                startInfo.RedirectStandardOutput = true;
                startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                startInfo.CreateNoWindow = true;
                process = Process.Start(startInfo);

                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        string outputStr = e.Data;
                        Logger.Instance.Log("[CONSOLE] " + e.Data);
                    }
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        string outputStr = e.Data;
                        Logger.Instance.Log("[CONSOLE] " + e.Data);
                    }
                };

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
            }
            catch (Exception ex)
            {
                Logger.Instance.Log("[ERROR] " + ex.Message);
                Socket_OnReceivedText("ERROR:" + ex.Message);
            }
        }

        public static void KillNodeJsProcesses()
        {
            Logger.Instance.Log("Closing existing node.exe files");
            try
            {
                Process process = new Process();
                process.StartInfo.FileName = "cmd.exe";
                process.StartInfo.RedirectStandardInput = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.UseShellExecute = false;
                process.Start();

                process.StandardInput.WriteLine("taskkill /F /IM node.exe");
                process.StandardInput.Flush();
                process.StandardInput.Close();
                process.WaitForExit();
                process.Close();
            }
            catch
            {
                Logger.Instance.Log("Something happened during node.exe shutdown.");
            }
        }



        private void Button_Click(object sender, RoutedEventArgs e)
        {
            string secret = context.Secret;
            string key = context.Key;
            CredentialUtil.SetCredentials(key, secret);

            NotificationBar.Visibility = Visibility.Visible;
            Task.Delay(3000).ContinueWith(t =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    NotificationBar.Visibility = Visibility.Hidden;
                });
            });
        }

        private void APISecretPassBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            context.Secret = APISecretPassBox.Password;
        }

        private void APIKeyPassBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            context.Key = APIKeyPassBox.Password;
        }

        private void APIKeyEye_Click(object sender, RoutedEventArgs e)
        {
            UpdateKeyValue(!isKeyHidden, false, (Button)sender);
        }

        private void APISecretEye_Click(object sender, RoutedEventArgs e)
        {
            UpdateKeyValue(!isSecretHidden, true, (Button)sender);
        }

        private void UpdateKeyValue(bool newState, bool isSecret, Button button)
        {
            if (isSecret)
            {
                isSecretHidden = newState;
                if (!isSecretHidden)
                {
                    button.Style = hideStyle;
                    APISecretBox.Visibility = Visibility.Visible;
                    APISecretPassBox.Visibility = Visibility.Hidden;
                }
                else
                {
                    button.Style = unhideStyle;
                    APISecretBox.Visibility = Visibility.Hidden;
                    APISecretPassBox.Visibility = Visibility.Visible;
                }
            }
            else
            {
                isKeyHidden = newState;
                if (!isKeyHidden)
                {
                    button.Style = hideStyle;
                    APIKeyBox.Visibility = Visibility.Visible;
                    APIKeyPassBox.Visibility = Visibility.Hidden;
                }
                else
                {
                    button.Style = unhideStyle;
                    APIKeyBox.Visibility = Visibility.Hidden;
                    APIKeyPassBox.Visibility = Visibility.Visible;
                }
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            KillNodeJsProcesses();

            if (process != null)
            {
                Logger.Instance.Log("Closing the console process..");
                process.Close();
            }
        }

        private void OnGameProcessChange()
        {
            Logger.Instance.Log("Closing the window..");
            Window_Closing(null, null);
        }
    }
}
