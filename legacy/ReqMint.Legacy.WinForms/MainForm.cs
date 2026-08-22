using ReqMint.Legacy.Core;

namespace ReqMint.Legacy.WinForms
{
    public partial class MainForm : Form
    {
        private readonly IApiAccess _apiAccess = new ApiAccess();

        public MainForm()
        {
            InitializeComponent();
            apiBar.Minimum = 0;
            apiBar.Maximum = 100;
            apiBar.Step = 1;
            apiBar.Value = 0;
            httpsBox.SelectedItem = "GET";
            apiText.PlaceholderText = "Enter URL or paste text";
            resultText.PlaceholderText = "Results will be displayed here";
        }

        private async void callApi_Click(object sender, EventArgs e)
        {
            systemStatus.Text = "Calling API";
            resultText.Text = "";
            apiBar.Value = 0;

            if (_apiAccess.IsValidUrl(apiText.Text) == false)
            {
                systemStatus.Text = "Invalid Url.!";
                return;
            }

            HttpAction action;
            if (Enum.TryParse(httpsBox.SelectedItem!.ToString(), out action) == false)
            {
                systemStatus.Text = "Invalid HTTP Verb";
                return;
            }

            try
            {
                var progressTask = Task.Run(() =>
                {
                    for (int i = 0; i <= 100; i++)
                    {
                        Invoke(new Action(() =>
                        {
                            apiBar.Value = i;
                        }));
                        Task.Delay(10).Wait();
                    }
                });

                await progressTask;
                var (jsonResponse, isDownloadable) = await _apiAccess.CallApiAsync(apiText.Text, bodyTextBox.Text, action);

                resultText.Text = jsonResponse;
                downloadButton.Visible = isDownloadable;
                copyButton.Visible = isDownloadable;

                tabControl1.SelectedTab = resultTabPage;
                tabControl1.Focus();

                systemStatus.Text = "Ready";
            }
            catch (Exception ex)
            {
                resultText.Text = $"Error: {ex.Message}";
                systemStatus.Text = "Error";
            }
        }

        private void downloadButton_Click(object sender, EventArgs e)
        {
            Helper.DownloadDataTypeJson(resultText.Text);
        }

        private void copyButton_Click(object sender, EventArgs e)
        {
            TextBox textBox = resultTabPage.Controls.OfType<TextBox>().FirstOrDefault();
            Clipboard.SetText(textBox!.Text);
            MessageBox.Show("Text copied to clipboard!", "Copy", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void bodyTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Tab)
            {
                int selectionStart = bodyTextBox.SelectionStart;
                bodyTextBox.Text = bodyTextBox.Text.Insert(selectionStart, "    ");
                bodyTextBox.SelectionStart = selectionStart + 4;
                e.SuppressKeyPress = true;
            }
        }
    }
}

file class Helper
{
    #region Download Data Type Json
    public static void DownloadDataTypeJson(string jsonData)
    {
        string guid = Guid.NewGuid().ToString();
        string fileName = $"{guid}.json";
        string filePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + @"\Downloads\" + fileName;

        File.WriteAllText(filePath, jsonData);
        MessageBox.Show($"JSON file success download: {filePath}", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
    #endregion
}
