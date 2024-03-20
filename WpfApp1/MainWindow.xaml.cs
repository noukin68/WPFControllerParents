using Hardcodet.Wpf.TaskbarNotification;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using WpfApp1.Classes;
using SocketIOClient;
using Newtonsoft.Json;
using System.Text;
using System.Windows.Markup;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace WpfApp1
{
    public partial class MainWindow : Window
    {
        private KeyboardLockManager keyboardLockManager = new KeyboardLockManager();

        private readonly List<Process> runningProcesses = new List<Process>();
        private readonly DispatcherTimer timerTask = new DispatcherTimer();

        private SocketIOClient.SocketIO socket;
        private DispatcherTimer timer;
        private Questionnaire questionnaire;
        private int currentQuestionIndex;
        private int correctAnswers;
        private TaskbarIcon taskbarIcon;
        private string UidClient;
        private string subject;
        private int grade;
        private int timeInSeconds;

        private void LockKeyboard() { keyboardLockManager.LockKeyboard(); }
        private void UnlockKeyboard() { keyboardLockManager.UnlockKeyboard(); }

        public MainWindow()
        {
            InitializeComponent();
            ShowSplashScreen();
            questionnaire = new Questionnaire("История", 5);
            timerTask.Interval = TimeSpan.FromSeconds(1);
            timerTask.Tick += Timer_Tick;
            timerTask.Start();
            InitializeTaskbarIcon();
            HideToTray();
            Closing += Window_Closing;
            UidTextBox = FindName("UidTextBox") as TextBlock;
            SetHighPriority();
            ScheduleSendingData();
            ConnectToServer();
        }

        private async void ConnectToServer()
        {
            try
            {
                socket = new SocketIOClient.SocketIO("http://62.217.182.138:3000");
                SetupSocketIOEvents(socket);
                await socket.ConnectAsync();
            }
            catch (System.StackOverflowException ex)
            {
                Console.WriteLine("Ошибка StackOverflowException: " + ex.Message);
            }
        }

        private void SetupSocketIOEvents(SocketIOClient.SocketIO socket)
        {
            socket.OnConnected += Socket_OnConnected;
            socket.OnDisconnected += (sender, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    AuthStatusText("Соединение разорвано", false);
                    UpdateConnectionStatusIcon(false);
                });
            };
            socket.On("uid", response => OnUidReceived(response));
            socket.On("time-received", (response) => OnTimeReceived(response));
            socket.On("stop-timer", (response) => OnStopTimeReceived(response));
            socket.On("continue-work", (response) => OnContinueWorkReceived(response));
            socket.On("finish-work", (response) => OnFinishWorkReceived(response));
            socket.On("selected-subject-and-class", (response) => OnSubjectAndClassReceived(response));
            socket.On("restart-time", (response) => OnRestartTimerReceived(response));
        }

        private void Socket_OnConnected(object sender, EventArgs e)
        {
            Console.WriteLine("Подключено к серверу");
        }

        private void OnSubjectAndClassReceived(SocketIOClient.SocketIOResponse response)
        {
            Console.WriteLine("OnSubjectAndClassReceived: " + response);

            var subjectAndClassResponse = JsonConvert.DeserializeObject<RequestResponse[]>(response.ToString());

            if (subjectAndClassResponse != null && subjectAndClassResponse.Length > 0)
            {
                string subject = subjectAndClassResponse[0].subject;
                int grade = subjectAndClassResponse[0].grade;
                string uid = subjectAndClassResponse[0].uid;

                Console.WriteLine("Subject: " + subject);
                Console.WriteLine("Grade: " + grade);

                if (uid == UidClient)
                {
                    GetDataFromServer(subject, grade);
                }
                else
                {
                    Console.WriteLine("UID не соответствует");
                }
            }
            else
            {
                Console.WriteLine("Ошибка десериализации ответа");
            }
        }

        private void OnTimeReceived(SocketIOClient.SocketIOResponse response)
        {
            Console.WriteLine("OnTimeReceived: " + response);

            var timeResponse = JsonConvert.DeserializeObject<RequestResponse[]>(response.ToString());

            if (timeResponse != null && timeResponse.Length > 0)
            {
                timeInSeconds = timeResponse[0].timeInSeconds;
                string uid = timeResponse[0].uid;

                Console.WriteLine("TimeInSeconds: " + timeInSeconds);

                if(uid == UidClient)
                {
                    Dispatcher.Invoke(() =>
                    {
                        StartTimer(timeInSeconds);
                    });
                }
                else
                {
                    Console.WriteLine("UID не соответствует");
                }
            }
            else
            {
                Console.WriteLine("Ошибка десериализации ответа");
            }
        }

        private void OnStopTimeReceived(SocketIOResponse response)
        {
            Console.WriteLine("OnStopTimeReceived: " + response);

            var stopTimeResponse = JsonConvert.DeserializeObject<RequestResponse[]>(response.ToString());

            if (stopTimeResponse != null && stopTimeResponse.Length > 0)
            {
                int totalSeconds = stopTimeResponse[0].totalSeconds;
                string uid = stopTimeResponse[0].uid;

                Console.WriteLine("TotalSeconds: " + totalSeconds);

                if (uid == UidClient)
                {
                    Dispatcher.Invoke(() =>
                    {
                        StopTimer(totalSeconds);
                    });
                }
                else
                {
                    Console.WriteLine("UID не соответствует");
                }
            }
            else
            {
                Console.WriteLine("Ошибка десериализации ответа");
            }
        }

        private void OnContinueWorkReceived(SocketIOClient.SocketIOResponse response)
        {
            Console.WriteLine("OnContinueWorkReceived: " + response);

            var continueWorkResponse = JsonConvert.DeserializeObject<RequestResponse[]>(response.ToString());

            if (continueWorkResponse != null && continueWorkResponse.Length > 0)
            {
                string uid = continueWorkResponse[0].uid;

                if (uid == UidClient)
                {
                    HandleAppMinimize();
                    UnlockKeyboard();
                }
                else
                {
                    Console.WriteLine("UID не соответствует");
                }
            }
            else
            {
                Console.WriteLine("Ошибка десериализации ответа");
            }
        }

        private void OnFinishWorkReceived(SocketIOClient.SocketIOResponse response)
        {
            Console.WriteLine("OnFinishWorkReceived: " + response);

            var finishWorkResponse = JsonConvert.DeserializeObject<RequestResponse[]>(response.ToString());

            if (finishWorkResponse != null && finishWorkResponse.Length > 0)
            {
                string uid = finishWorkResponse[0].uid;

                if (uid == UidClient)
                {
                    HandleAppFinish();
                    UnlockKeyboard();
                }
                else
                {
                    Console.WriteLine("UID не соответствует");
                }
            }
            else
            {
                Console.WriteLine("Ошибка десериализации ответа");
            }
        }

        private void OnUidReceived(SocketIOResponse response)
        {
            UidClient = response.GetValue<string>();
            Console.WriteLine("Received uid: " + UidClient);
            Dispatcher.Invoke(() => UidTextBox.Text = UidClient);
        }

        private void ScheduleSendingData()
        {
            DateTime targetTime = DateTime.Today.AddHours(21);
            //DateTime targetTime = DateTime.Today.AddHours(15).AddMinutes(50);

            if (DateTime.Now > targetTime)
            {
                targetTime = targetTime.AddDays(1);
            }

            TimeSpan timeUntilTarget = targetTime - DateTime.Now;

            DispatcherTimer timer = new DispatcherTimer();
            timer.Interval = timeUntilTarget;
            timer.Tick += (sender, e) =>
            {
                SendProcessDataToServer();

                timer.Stop();
            };

            timer.Start();
        }

        private void SetHighPriority()
        {
            Process currentProcess = Process.GetCurrentProcess();
            if (currentProcess != null) { currentProcess.PriorityClass = ProcessPriorityClass.High; }
        }

        private void ShowSplashScreen() { Pages.SplashScreen splashScreen = new Pages.SplashScreen(); splashScreen.ShowDialog(); }

        private void InitializeTaskbarIcon()
        {
            taskbarIcon = new TaskbarIcon { Icon = Properties.Resources.icon, ToolTipText = "Родительский контроль" };
            taskbarIcon.TrayMouseDoubleClick += TaskbarIcon_DoubleClick;
        }

        private void TaskbarIcon_DoubleClick(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Normal;
            this.Show();
            this.Activate();
            taskbarIcon.Visibility = Visibility.Collapsed;
        }

        private void HideToTray() { this.Hide(); taskbarIcon.Visibility = Visibility.Visible; }

        private async void ShowQuestion(int index)
        {
            if (index < questionnaire.Questions.Count)
            {
                Question question = questionnaire.Questions[index];
                textBlock.Text = question.Text;
                textBlock.TextWrapping = TextWrapping.Wrap;
                textBlock.TextAlignment = TextAlignment.Center;
                answerStackPanel.Children.Clear();
                StackPanel buttonStackPanel = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(0, 0, 0, 20)
                };
                foreach (string option in question.Options)
                {
                    Button button = new Button
                    {
                        Style = (Style)FindResource("ColoredButtonStyle"),
                        Tag = option,
                        Width = 450,
                        HorizontalContentAlignment = HorizontalAlignment.Center
                    };

                    TextBlock textBlock = new TextBlock
                    {
                        Text = option,
                        TextWrapping = TextWrapping.Wrap,
                        TextAlignment = TextAlignment.Center
                    };

                    button.Content = textBlock;
                    button.Click += AnswerButton_Click;
                    buttonStackPanel.Children.Add(button);
                }

                answerStackPanel.Children.Add(buttonStackPanel);
            }
            else
            {
                textBlock.Text = $"Тест завершен";
                HttpClient client = new HttpClient();
                var response = await client.GetAsync("http://62.217.182.138:3000/notify");
                answerStackPanel.Children.Clear();
                LockKeyboard();
            }
        }

        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == WindowState.Minimized) { HideToTray(); }
            else if (WindowState == WindowState.Normal) { taskbarIcon.Visibility = Visibility.Collapsed; }
            base.OnStateChanged(e);
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (timer != null && timer.IsEnabled) { e.Cancel = true; MessageBox.Show("Пожалуйста, дождитесь завершения таймера."); }
            else { e.Cancel = true; HideToTray(); }
            base.OnClosing(e);
        }

        private async void AnswerButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;

            if (currentQuestionIndex >= 0 && currentQuestionIndex < questionnaire.Questions.Count)
            {
                Question question = questionnaire.Questions[currentQuestionIndex];

                int selectedOptionIndex = question.Options.IndexOf(button.Tag.ToString());

                // Отключаем все кнопки

                foreach (UIElement child in answerStackPanel.Children)
                {

                    if (child is StackPanel buttonStackPanel)
                    {

                        foreach (UIElement innerChild in buttonStackPanel.Children)
                        {

                            if (innerChild is Button answerButton)

                            {

                                answerButton.IsEnabled = false;

                            }

                        }

                    }

                }

                bool isCorrect = selectedOptionIndex == question.CorrectIndex;

                if (isCorrect)
                {
                    button.Background = Brushes.Green;

                    correctAnswers++;

                    if (correctAnswers == 5)

                    {

                        correctAnswers = 0;

                        currentQuestionIndex = 0;

                        await socket.EmitAsync("restart-timer");
                        this.WindowState = WindowState.Normal;
                        this.HideToTray();
                        answerStackPanel.Children.Clear();
                        textBlock.Text = "";
                        currentQuestionIndex = 0;
                        UnlockKeyboard();

                        return;

                    }

                }

                else
                {
                    button.Background = Brushes.Red;

                    questionnaire.ShuffleQuestions();

                    correctAnswers = 0;
                }

                await Task.Delay(1500);

                button.Background = Brushes.Transparent;

                foreach (UIElement child in answerStackPanel.Children)
                {
                    if (child is StackPanel buttonStackPanel)
                    {

                        foreach (UIElement innerChild in buttonStackPanel.Children)
                        {

                            if (innerChild is Button answerButton)
                            {

                                answerButton.IsEnabled = true;

                            }
                        }
                    }
                }

                currentQuestionIndex++;

                if (currentQuestionIndex >= questionnaire.Questions.Count)
                {
                    currentQuestionIndex = 0;
                    await socket.EmitAsync("restart-timer");
                    this.WindowState = WindowState.Normal;
                    this.HideToTray();
                    answerStackPanel.Children.Clear();
                    textBlock.Text = "";
                    currentQuestionIndex = 0;
                    UnlockKeyboard();
                }

                ShowQuestion(currentQuestionIndex);
            }
        }

        private void OnRestartTimerReceived(SocketIOResponse response)
        {
            string uid = response.GetValue<string>();


            if (uid == UidClient)
            {
                RestartTimerUI();
            }
            else
            {
                Console.WriteLine("UID не соответствует");
            }

        }

        private void RestartTimerUI()
        {
            Dispatcher.Invoke(() =>
            {
                this.WindowState = WindowState.Normal;
                this.HideToTray();
                answerStackPanel.Children.Clear();
                textBlock.Text = "";
                currentQuestionIndex = 0;
                UnlockKeyboard();
            });
        }

        private void GetDataFromServer(string subject, int grade)
        {
            questionnaire = new Questionnaire(subject, grade);
        }

        private void HandleAppMinimize() { this.Dispatcher.Invoke(() => { UnlockKeyboard(); textBlock.Text = ""; HideToTray(); }); }
        private void HandleAppFinish() { this.Dispatcher.Invoke(() => { UnlockKeyboard(); System.Diagnostics.Process.Start("shutdown", "/s /t 0"); }); }

        private void AuthStatusText(string text, bool isConnected)
        {
            Dispatcher.Invoke(() =>
            {
                AuthText.Text = text;
                UpdateConnectionStatusIcon(isConnected);
            });
        }

        private void UpdateConnectionStatusIcon(bool isConnected)
        {
            if (isConnected)
            {
                ConnectionStatusIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.SmartphoneLink;
            }
            else
            {
                ConnectionStatusIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.SmartphoneLinkOff;
            }
        }

        private void StopTimer(int totalSeconds)
        {
            if (timer != null && timer.IsEnabled)
            {
                timer.Stop();
            }
        }

        private void StartTimer(int timeInSeconds)
        {
            if (timer != null && timer.IsEnabled) { timer.Stop(); }
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            int remainingTime = timeInSeconds;
            timer.Tick += (sender, e) =>
            {
                remainingTime--;
                if (remainingTime < 0)
                {
                    this.Show();
                    this.Activate();
                    timer.Stop();
                    UpdateTextBlock("Время вышло!");
                    LockKeyboard();
                    WindowState = WindowState.Maximized;
                    Topmost = true;
                    socket.EmitAsync("timer-finished");
                    currentQuestionIndex = 0;
                    correctAnswers = 0;
                    ShowQuestion(currentQuestionIndex);
                }
            };
            timer.Start();
        }

        private void UpdateTextBlock(string text) { textBlock.Text = text; }
        protected override void OnClosed(EventArgs e) { ConnectToServer(); base.OnClosed(e); }
        public MainWindow(IntPtr hWnd) : this() { WindowInteropHelper helper = new WindowInteropHelper(this); helper.Owner = hWnd; }
        private void Window_StateChanged(object sender, EventArgs e) { if (WindowState == WindowState.Minimized) { WindowState = WindowState.Normal; Topmost = true; } }
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) { e.Cancel = true; HideToTray(); }
        private void Window_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e) { try { DragMove(); } catch (Exception) { } }
        private void MinimizeBtn(object sender, RoutedEventArgs e) { WindowState = WindowState.Minimized; }
        private void CloseBtn(object sender, RoutedEventArgs e) { Close(); }

        private async void PackIcon_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Clipboard.SetText(UidClient);
            copiedTextBlock.Visibility = Visibility.Visible;
            await Task.Delay(1000);
            copiedTextBlock.Visibility = Visibility.Collapsed;
        }


        private void Timer_Tick(object sender, EventArgs e)
        {
            Process[] processes = Process.GetProcesses();
            var userProcesses = processes.Where(process => !string.IsNullOrEmpty(process.MainWindowTitle)).ToList();
            UpdateProcessList(userProcesses);
            UpdateProcessTimes();
        }

        private void UpdateProcessList(List<Process> userProcesses)
        {
            foreach (var process in userProcesses)
            {
                if (!runningProcesses.Contains(process))
                {
                    runningProcesses.Add(process);
                }
            }
            foreach (var process in runningProcesses.ToList())
            {
                if (!userProcesses.Contains(process))
                {
                    runningProcesses.Remove(process);
                }
            }
        }

        private void UpdateProcessTimes()
        {
            foreach (var process in runningProcesses)
            {
                TimeSpan elapsedTime = DateTime.Now - process.StartTime;
                //Console.WriteLine($"ProcessName: {process.ProcessName}, StartTime: {process.StartTime}, ElapsedTime: {elapsedTime.ToString(@"hh\:mm\:ss")}");
            }
        }

        private void SendProcessDataToServer()
        {
            Process[] processes = Process.GetProcesses();
            var userProcesses = processes.Where(process => !string.IsNullOrEmpty(process.MainWindowTitle)).ToList();
            List<ProcessInfo> processInfos = new List<ProcessInfo>();
            foreach (var process in userProcesses)
            {
                TimeSpan elapsedTime = DateTime.Now - process.StartTime;
                processInfos.Add(new ProcessInfo
                {
                    ProcessName = process.ProcessName,
                    StartTime = process.StartTime,
                    ElapsedTime = elapsedTime
                });
            }

            // Создайте объект для отправки данных
            var dataToSend = new
            {
                uid = UidClient,
                processes = processInfos
            };

            // Отправьте данные на сервер с помощью Socket.IO
            socket.EmitAsync("process-data", dataToSend);
        }

        private void TrackerClick(object sender, RoutedEventArgs e)
        {
            SendProcessDataToServer();
        }
    }
}
