namespace ReqMint.Legacy.WinForms
{
    partial class MainForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            label1 = new Label();
            label2 = new Label();
            apiText = new TextBox();
            resultLabel = new Label();
            resultText = new TextBox();
            apiBar = new ProgressBar();
            callApi = new Button();
            statusStrip1 = new StatusStrip();
            systemStatus = new ToolStripStatusLabel();
            httpsBox = new ComboBox();
            tabControl1 = new TabControl();
            tabPage1 = new TabPage();
            bodyTextBox = new TextBox();
            resultTabPage = new TabPage();
            downloadButton = new Button();
            copyButton = new Button();
            statusStrip1.SuspendLayout();
            tabControl1.SuspendLayout();
            tabPage1.SuspendLayout();
            resultTabPage.SuspendLayout();
            SuspendLayout();
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Font = new Font("Segoe UI", 14.25F, FontStyle.Bold, GraphicsUnit.Point, 162);
            label1.Location = new Point(7, -20);
            label1.Name = "label1";
            label1.Size = new Size(0, 25);
            label1.TabIndex = 0;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Font = new Font("Segoe UI", 14.25F, FontStyle.Bold, GraphicsUnit.Point, 162);
            label2.Location = new Point(6, 9);
            label2.Name = "label2";
            label2.Size = new Size(121, 25);
            label2.TabIndex = 1;
            label2.Text = "Your Api Url";
            // 
            // apiText
            // 
            apiText.Font = new Font("Segoe UI", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 162);
            apiText.Location = new Point(133, 37);
            apiText.Multiline = true;
            apiText.Name = "apiText";
            apiText.PlaceholderText = "Enter URL or paste text";
            apiText.ScrollBars = ScrollBars.Vertical;
            apiText.Size = new Size(714, 56);
            apiText.TabIndex = 10;
            apiText.TabStop = false;
            // 
            // resultLabel
            // 
            resultLabel.AutoSize = true;
            resultLabel.Font = new Font("Segoe UI", 14.25F, FontStyle.Bold, GraphicsUnit.Point, 162);
            resultLabel.Location = new Point(12, 140);
            resultLabel.Name = "resultLabel";
            resultLabel.Size = new Size(66, 25);
            resultLabel.TabIndex = 3;
            resultLabel.Text = "Result";
            // 
            // resultText
            // 
            resultText.Dock = DockStyle.Fill;
            resultText.Location = new Point(3, 3);
            resultText.Multiline = true;
            resultText.Name = "resultText";
            resultText.ReadOnly = true;
            resultText.ScrollBars = ScrollBars.Vertical;
            resultText.Size = new Size(956, 409);
            resultText.TabIndex = 4;
            // 
            // apiBar
            // 
            apiBar.Location = new Point(12, 115);
            apiBar.Name = "apiBar";
            apiBar.Size = new Size(958, 22);
            apiBar.Style = ProgressBarStyle.Continuous;
            apiBar.TabIndex = 5;
            // 
            // callApi
            // 
            callApi.Font = new Font("Segoe UI", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 162);
            callApi.Location = new Point(6, 66);
            callApi.Name = "callApi";
            callApi.Size = new Size(121, 27);
            callApi.TabIndex = 6;
            callApi.TabStop = false;
            callApi.Text = "Send Request";
            callApi.UseVisualStyleBackColor = true;
            callApi.Click += callApi_Click;
            // 
            // statusStrip1
            // 
            statusStrip1.Items.AddRange(new ToolStripItem[] { systemStatus });
            statusStrip1.Location = new Point(0, 614);
            statusStrip1.Name = "statusStrip1";
            statusStrip1.Size = new Size(982, 25);
            statusStrip1.TabIndex = 7;
            statusStrip1.Text = "statusStrip1";
            // 
            // systemStatus
            // 
            systemStatus.Font = new Font("Segoe UI", 11.25F, FontStyle.Regular, GraphicsUnit.Point, 162);
            systemStatus.Name = "systemStatus";
            systemStatus.Size = new Size(50, 20);
            systemStatus.Text = "Ready";
            // 
            // httpsBox
            // 
            httpsBox.DropDownStyle = ComboBoxStyle.DropDownList;
            httpsBox.FlatStyle = FlatStyle.System;
            httpsBox.FormattingEnabled = true;
            httpsBox.Items.AddRange(new object[] { "GET", "POST", "PUT", "PATCH", "DELETE" });
            httpsBox.Location = new Point(6, 37);
            httpsBox.Name = "httpsBox";
            httpsBox.Size = new Size(121, 23);
            httpsBox.TabIndex = 8;
            httpsBox.TabStop = false;
            // 
            // tabControl1
            // 
            tabControl1.Controls.Add(tabPage1);
            tabControl1.Controls.Add(resultTabPage);
            tabControl1.Location = new Point(12, 168);
            tabControl1.Name = "tabControl1";
            tabControl1.SelectedIndex = 0;
            tabControl1.Size = new Size(970, 443);
            tabControl1.TabIndex = 9;
            tabControl1.TabStop = false;
            // 
            // tabPage1
            // 
            tabPage1.Controls.Add(bodyTextBox);
            tabPage1.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 162);
            tabPage1.Location = new Point(4, 24);
            tabPage1.Name = "tabPage1";
            tabPage1.Padding = new Padding(3);
            tabPage1.Size = new Size(962, 415);
            tabPage1.TabIndex = 0;
            tabPage1.Text = "Body";
            tabPage1.UseVisualStyleBackColor = true;
            // 
            // bodyTextBox
            // 
            bodyTextBox.Dock = DockStyle.Fill;
            bodyTextBox.Location = new Point(3, 3);
            bodyTextBox.Multiline = true;
            bodyTextBox.Name = "bodyTextBox";
            bodyTextBox.ScrollBars = ScrollBars.Vertical;
            bodyTextBox.Size = new Size(956, 409);
            bodyTextBox.TabIndex = 11;
            bodyTextBox.TabStop = false;
            bodyTextBox.KeyDown += bodyTextBox_KeyDown;
            // 
            // resultTabPage
            // 
            resultTabPage.Controls.Add(resultText);
            resultTabPage.Location = new Point(4, 24);
            resultTabPage.Name = "resultTabPage";
            resultTabPage.Padding = new Padding(3);
            resultTabPage.Size = new Size(962, 415);
            resultTabPage.TabIndex = 1;
            resultTabPage.Text = "Result";
            resultTabPage.UseVisualStyleBackColor = true;
            // 
            // downloadButton
            // 
            downloadButton.Location = new Point(854, 36);
            downloadButton.Name = "downloadButton";
            downloadButton.Size = new Size(121, 29);
            downloadButton.TabIndex = 11;
            downloadButton.TabStop = false;
            downloadButton.Text = "Download";
            downloadButton.UseVisualStyleBackColor = true;
            downloadButton.Visible = false;
            downloadButton.Click += downloadButton_Click;
            // 
            // copyButton
            // 
            copyButton.Location = new Point(854, 66);
            copyButton.Name = "copyButton";
            copyButton.Size = new Size(121, 27);
            copyButton.TabIndex = 12;
            copyButton.TabStop = false;
            copyButton.Text = "Copy";
            copyButton.UseVisualStyleBackColor = true;
            copyButton.Visible = false;
            copyButton.Click += copyButton_Click;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = SystemColors.Control;
            ClientSize = new Size(982, 639);
            Controls.Add(copyButton);
            Controls.Add(downloadButton);
            Controls.Add(tabControl1);
            Controls.Add(httpsBox);
            Controls.Add(statusStrip1);
            Controls.Add(callApi);
            Controls.Add(apiBar);
            Controls.Add(resultLabel);
            Controls.Add(apiText);
            Controls.Add(label2);
            Controls.Add(label1);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            Icon = (Icon)resources.GetObject("$this.Icon");
            MaximizeBox = false;
            Name = "MainForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "ReqMint Legacy — Alparslan Akbas";
            statusStrip1.ResumeLayout(false);
            statusStrip1.PerformLayout();
            tabControl1.ResumeLayout(false);
            tabPage1.ResumeLayout(false);
            tabPage1.PerformLayout();
            resultTabPage.ResumeLayout(false);
            resultTabPage.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label label1;
        private Label label2;
        private TextBox apiText;
        private Label resultLabel;
        private TextBox resultText;
        private ProgressBar apiBar;
        private Button callApi;
        private StatusStrip statusStrip1;
        private ToolStripStatusLabel systemStatus;
        private ComboBox httpsBox;
        private TabControl tabControl1;
        private TabPage tabPage1;
        private TabPage resultTabPage;
        private TextBox bodyTextBox;
        private Button downloadButton;
        private Button copyButton;
    }
}
