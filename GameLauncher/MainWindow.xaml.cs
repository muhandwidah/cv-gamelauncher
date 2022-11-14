using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Windows;
using ICSharpCode.SharpZipLib.Zip;


namespace GameLauncher
{
    enum LauncherStatus
    {
        ready,
        failed,
        downloadingGame,
        downloadingUpdate
    }
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string rootPath;
        private string versionFile;
        private string gameZip;
        private string gameExe;
        private string newVersionFile;

        private LauncherStatus _status;

        internal LauncherStatus Status
        {
            get => _status;
            set
            {
                _status = value;
                switch (_status)
                {
                    case LauncherStatus.ready:
                        PlayButton.Content = "Play";
                        progressBar.Visibility = Visibility.Hidden;
                        break;
                    case LauncherStatus.failed:
                        PlayButton.Content = "Update Failed - Retry";
                        progressBar.Visibility = Visibility.Hidden;
                        break;
                    case LauncherStatus.downloadingGame:
                        PlayButton.Content = "Downloading Game";
                        break;
                    case LauncherStatus.downloadingUpdate:
                        PlayButton.Content = "Downloading Update";
                        break;
                    default:
                        break;

                }
            }
        }

        public MainWindow()
        {
            InitializeComponent();

            rootPath = Directory.GetCurrentDirectory();
            versionFile = System.IO.Path.Combine(rootPath, "Version.txt");
            gameZip = System.IO.Path.Combine(rootPath, "Build.zip");
            gameExe = System.IO.Path.Combine(rootPath, "Alpha", "CronosVerse.exe");
            newVersionFile = System.IO.Path.Combine(rootPath, "NewVersion.txt");


        }

        private void CheckForUpdates()
        {
            if (File.Exists(versionFile))
            {
                WebClient webClient = new WebClient();
                Version localVersion = new Version(File.ReadAllText(versionFile));
                VersionText.Text = localVersion.ToString();
                webClient.DownloadFileCompleted += new AsyncCompletedEventHandler(DownloadVersionCompletedCallback);
                webClient.DownloadFileAsync(new Uri("https://versionholder.s3.us-east-2.amazonaws.com/Version.txt"), newVersionFile);
            }
            else
            {
                if (MessageBox.Show("No game files found on device, install The CronosVerse?", "No Game Found", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                {
                    InstallGameFiles(false, Version.zero);
                }
                else 
                {
                    Close();
                }
            }
        }
        private void DownloadVersionCompletedCallback(object sender, AsyncCompletedEventArgs e)
        {
            Version localVersion = new Version(File.ReadAllText(versionFile));
            try
            {
                Version onlineVersion = new Version(File.ReadAllText(newVersionFile));
                if (onlineVersion.IsDifferentThan(localVersion))
                {
                    if (MessageBox.Show("New game version found, install the latest version of The CronosVerse?", "New Update Found", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                    {
                        InstallGameFiles(true, onlineVersion);
                    }
                    else
                    {
                        Close();
                    }
                }
                else
                {
                    Status = LauncherStatus.ready;
                }
            }
            catch (Exception ex)
            {
                Status = LauncherStatus.failed;
                MessageBox.Show($"Error checking for game updates: {ex}");
            }
            File.Delete(versionFile);
            File.Move(newVersionFile, versionFile);
        }

            private void InstallGameFiles(bool _isUpdate, Version _onlineVersion)
        {
            progressBar.Visibility = Visibility.Visible;
            gameProgress.Visibility = Visibility.Visible;
            try
            {
                WebClient webClient = new WebClient();
                if (_isUpdate)
                {
                    Status = LauncherStatus.downloadingUpdate;
                }
                else
                {
                    Status = LauncherStatus.downloadingGame;
                    _onlineVersion = new Version(webClient.DownloadString("https://versionholder.s3.us-east-2.amazonaws.com/Version.txt"));
                }
                webClient.DownloadFileCompleted += new AsyncCompletedEventHandler(DownloadGameCompletedCallback);
                webClient.DownloadProgressChanged += (s, e) =>
                {
                    progressBar.Value = e.ProgressPercentage;
                    gameProgress.Text = e.ProgressPercentage.ToString() + "%";
                };
                webClient.DownloadFileCompleted += (s, e) =>
                {
                    progressBar.Visibility = Visibility.Hidden;
                    gameProgress.Visibility = Visibility.Hidden;
                    // any other code to process the file
                };

                webClient.DownloadFileAsync(new Uri("https://currentversion.s3.amazonaws.com/build.zip"), gameZip, _onlineVersion);
            }
            catch (Exception ex)
            {
                Status = LauncherStatus.failed;
                MessageBox.Show($"Error installing game files: {ex}");
            }
        }

        private void DownloadGameCompletedCallback(object sender, AsyncCompletedEventArgs e)
        {
            try
            {
                FastZip fastZip = new FastZip();
                string fileFilter = null;

                string onlineVersion = ((Version)e.UserState).ToString();
                fastZip.ExtractZip(gameZip, rootPath, fileFilter);
                
                File.Delete(gameZip);

                File.WriteAllText(versionFile, onlineVersion);

                VersionText.Text = onlineVersion;
                progressBar.Visibility = Visibility.Hidden;
                Status = LauncherStatus.ready;
            }
            catch (Exception ex)
            {
                Status = LauncherStatus.failed;
                MessageBox.Show($"Error finishing download: {ex}");
            }
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            CheckForUpdates();
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (File.Exists(gameExe) && Status == LauncherStatus.ready)
            {
                ProcessStartInfo startInfo = new ProcessStartInfo(gameExe);
                startInfo.WorkingDirectory = Path.Combine(rootPath, "Build");
                Process.Start(startInfo);

                Close();
            }
            else if (Status == LauncherStatus.failed)
            {
                CheckForUpdates();
            }
        }

    }
    
    struct Version
    {
        internal static Version zero = new Version(0, 0, 0);

        private short major;
        private short minor;
        private short subMinor;

        internal Version(short _major, short _minor, short _subMinor)
        {
            major = _major;
            minor = _minor;
            subMinor = _subMinor;
        }
        internal Version(string _version)
        {
            string[] _versionStrings = _version.Split('.');
            if (_versionStrings.Length != 3)
            {
                major = 0;
                minor = 0;
                subMinor = 0;
                return;
            }
            major = short.Parse(_versionStrings[0]);
            minor = short.Parse(_versionStrings[1]);
            subMinor = short.Parse(_versionStrings[2]);

        }

        internal bool IsDifferentThan(Version _otherVersion)
        {
            if (major != _otherVersion.major)
            {
                return true;
            }
            else
            {
                if (minor != _otherVersion.minor)
                {
                    return true;
                }
                else
                {
                    if (subMinor != _otherVersion.subMinor)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public override string ToString()
        {
            return $"{major}.{minor}.{subMinor}";
        }

    }

}
