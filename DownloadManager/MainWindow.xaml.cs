using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net;
using System.Windows;
using System.Windows.Media;
using System.IO;
using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Controls;
using Newtonsoft.Json;

namespace DownloadManager
{
    public partial class MainWindow : Window
    {
        /// <summary>
        /// https://letsenhance.io/static/8f5e523ee6b2479e26ecc91b9c25261e/1015f/MainAfter.jpg
        /// https://ash-speed.hetzner.com/100MB.bin
        ///https://h5p.org/sites/default/files/h5p/content/1209180/images/file-6113d5f8845dc.jpeg
        /// </summary>

        private TaskCompletionSource<bool> pauseTcs = new TaskCompletionSource<bool>();
        private ObservableCollection<DownloadItem> downloads = new ObservableCollection<DownloadItem>();
        private ObservableCollection<DownloadItem> findings = new ObservableCollection<DownloadItem>();
        private WebClient client = new WebClient();
        private bool isDownloading = false;


        private string report = "downloads.json";
        public MainWindow()
        {
            InitializeComponent();

            if (File.Exists(report))
            {
                string json = File.ReadAllText(report);
                downloads = JsonConvert.DeserializeObject<ObservableCollection<DownloadItem>>(json);
            }

            PauseResumeBtn.IsEnabled = false;

            downloadsListBox.ItemsSource = downloads;
            searchListBox.ItemsSource = findings;

            client.DownloadFileCompleted += ClientDownloadFileCompleted;
        }

        private void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            string url = urlTextBox.Text;
            string dest = destTextBox.Text;

            if (string.IsNullOrEmpty(url) && string.IsNullOrEmpty(dest))
            {
                MessageBox.Show("Please fill empty fields");
                return;
            }
            try
            {
                DownloadFileAsync(url, dest);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Oops..{ex.Message}");
            }
        }


        private int counter = 1;
        private async Task DownloadFileAsync(string url, string dest)
        {
            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(dest))
            {
                MessageBox.Show("URL and destination path cannot be empty.");
                return;
            }

            PauseResumeBtn.IsEnabled = true;
            if (isDownloading)
            {
                MessageBox.Show("Download is already in progress");
                return;
            }

            isDownloading = true;
            PauseResumeBtn.Content = "⏸️";

            var item = new DownloadItem
            {
                Url = url,
                Destination = dest,
                Status = "Downloading",
                Time = DateTime.Now,
                Progress = 0
            };

            string file = Path.GetFileName(url);
            string newDest = Path.Combine(dest, file);

            if (File.Exists(newDest))
            {
                string fname = Path.GetFileNameWithoutExtension(file);
                string ext = Path.GetExtension(file);
                file = NewFileName(fname, ext, dest);
                newDest = Path.Combine(dest, file);
            }

            item.Name = file;
            downloads.Add(item);
            client.DownloadProgressChanged += ClientDownloadProgressChanged;

            await client.DownloadFileTaskAsync(url, newDest);

            isDownloading = false;
            PauseResumeBtn.Content = "▶️";
            Refresh();
        }

        private string NewFileName(string file, string ext, string dest)
        {
            string newName = $"{file}_{counter}{ext}";
            while (File.Exists(Path.Combine(dest, newName)))
            {
                counter++;
                newName = $"{file}_{counter}{ext}";
            }
            return newName;
        }

        private void ClientDownloadProgressChanged(object? sender, DownloadProgressChangedEventArgs e)
        {
            percent.Value = e.ProgressPercentage;
            progressTextBox.Text = $"{e.ProgressPercentage}%";
        }
        private void ClientDownloadFileCompleted(object? sender, AsyncCompletedEventArgs e)
        {
            var item = downloads.LastOrDefault();
            item.Status = "Downloaded";
            percent.Value = 0;
            PauseResumeBtn.Content = "▶️";
            PauseResumeBtn.IsEnabled = false;
            Refresh();
            MessageBox.Show("Download successful!", "Notification");
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog();
            if (dialog.ShowDialog() == true)
            {
                string dir = Path.GetFullPath(dialog.FolderName);
                destTextBox.Text = dir;
            }
        }

        private async void CancelDownloadBtn_Click(object sender, RoutedEventArgs e)
        {
            client.CancelAsync();
            client.DownloadProgressChanged -= ClientDownloadProgressChanged;
            client.DownloadFileCompleted -= ClientDownloadFileCompleted;

            if (!isDownloading)
            {
                MessageBox.Show("No download in progress to cancel");
                return;
            }

            var item = downloads.LastOrDefault();
            if (item != null) item.Status = "Cancelled";
            Refresh();
            percent.Value = 0;
            progressTextBox.Text = "0%";
            PauseResumeBtn.Content = "▶️";
            PauseResumeBtn.IsEnabled = false;
            isDownloading = false;
            MessageBox.Show("Download cancelled");
            await Task.CompletedTask;
        }

        private void ListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var dialog = new OpenFileDialog();
            if (dialog.ShowDialog() == true)
            {
                string path = dialog.FileName;
                string dir = Path.GetDirectoryName(path)!;
                Process.Start(path, dir);
            }
        }

        string placeholder = "Enter a file name to search";
        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (searchBox.Text == placeholder)
            {
                searchBox.Text = "";
                searchBox.Foreground = Brushes.Black;
                searchBox.FontStyle = FontStyles.Normal;
            }
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(searchBox.Text))
            {
                searchBox.Foreground = Brushes.Gray;
                searchBox.FontStyle = FontStyles.Italic;
                searchBox.Text = placeholder;
                searchListBox.ItemsSource = null;
                searchListBox.ItemsSource = findings;
            }
        }
        private void searchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                string filter = searchBox.Text;
                var filteredItems = downloads.Where(d => d.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

                findings.Clear();
                foreach (var item in filteredItems)
                    findings.Add(item);

                if (findings.Count == 0)
                    downloadsListBox.SelectedItem = null;
                else
                    searchListBox.SelectedIndex = 0;
            }
            catch (Exception)
            {
                return;
            }

        }

        private int progress = 0;
        private async void PauseResumeBtn_Click(object sender, RoutedEventArgs e)
        {
            var item = downloads.LastOrDefault();
            if (!pauseTcs.Task.IsCompleted)
            {
                progress = (int)percent.Value;
                PauseResumeBtn.Content = "▶️";
                if (item != null) item.Status = "Paused";
                Refresh();

                client.CancelAsync();
                client.DownloadProgressChanged -= ClientDownloadProgressChanged;
                client.DownloadFileCompleted -= ClientDownloadFileCompleted;
                pauseTcs.SetResult(true);
            }
            else if (pauseTcs.Task.IsCompleted)
            {
                percent.Value = progress;
                PauseResumeBtn.Content = "⏸️";
                if (item != null) item.Status = "Downloading";
                Refresh();
                client.DownloadProgressChanged += ClientDownloadProgressChanged;
                client.DownloadFileCompleted += ClientDownloadFileCompleted;
                pauseTcs = new TaskCompletionSource<bool>();
                await pauseTcs.Task;
            }
        }

        private void downloadsListBox_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            var contextMenu = FindResource("DownloadItemContextMenu") as ContextMenu;
            downloadsListBox.ContextMenu = contextMenu;
        }
        private void DeleteItem_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = downloadsListBox.SelectedItem as DownloadItem;
            if (selectedItem != null)
            {
                //string path = selectedItem.Destination;

                //MessageBoxResult result = MessageBox.Show($"Are you sure want to delete the file '{path}'?", "Deleting", MessageBoxButton.YesNo, MessageBoxImage.Question);
                //if (result == MessageBoxResult.Yes)
                //{
                //    DeleteFile(path);
                //}
                downloads.Remove(selectedItem);
            }
            else
            {
                MessageBox.Show("No file selected to delete");
            }
        }

        //private void DeleteFile(string file)
        //{
        //    try
        //    {
        //        File.Delete(file);
        //        downloads.Remove((DownloadItem)downloadsListBox.SelectedItem);
        //    }
        //    catch (Exception ex)
        //    {
        //        MessageBox.Show($"Error deleting the file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        //    }
        //}
        private void Refresh()
        {
            downloadsListBox.ItemsSource = null;
            downloadsListBox.ItemsSource = downloads;
            searchListBox.Items.Refresh();
        }
        private void Window_Closed(object sender, EventArgs e)
        {
            foreach (var item in downloads)
                if (item.Status == "Downloading")
                    item.Status = "Failed";

            string json = JsonConvert.SerializeObject(downloads);
            File.WriteAllText(report, json);
        }
    }

    public class DownloadItem
    {
        public string Name { get; set; }
        public string? Url { get; set; }
        public string? Destination { get; set; }
        public string Status { get; set; }
        public DateTime Time { get; set; }
        public int Progress { get; set; }
        public override string ToString()
        {
            return Name;
        }
    }

}
