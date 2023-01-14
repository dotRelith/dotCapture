using Newtonsoft.Json;
using System;
using System.Resources;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using Tesseract;
using System.Speech.Synthesis;
using dotCapture.Resources;
using dotCapture.Properties;

using Clipboard = System.Windows.Forms.Clipboard;
using ImageFormat = System.Drawing.Imaging.ImageFormat;
using System.Globalization;
using System.Threading;
//using System.Configuration;
//using static System.Windows.Forms.VisualStyles.VisualStyleElement.TextBox;

namespace dotCapture
{
    class ScreenCapture : Form
    {
        const int WS_EX_TOOLWINDOW = 0x00000080;
        protected override CreateParams CreateParams
        {
            get
            {
                var Params = base.CreateParams;
                Params.ExStyle |= WS_EX_TOOLWINDOW;
                return Params;
            }
        }
        // The singleton instance
        private static ScreenCapture instance = null;
        public static ScreenCapture Instance { get { return instance; } }

        private string availableLanguages = "eng+ita+jpn+kor+por+spa";
        private ResourceManager resourceManager;
        private Form settingsMenu;
        private PictureBox captureBackground;
        private Panel captureMenu, languagePanel;
        private NotifyIcon trayIcon;
        private Button copyButton, saveButton, translateButton, copyTextButton, textToSpeechButton;
        private ToolStripItem exitStripItem, settingsStripItem;
        private Button playPauseButton;
        private List<string> textToSpeechTexts = new List<string>();
        private SpeechSynthesizer synthesizer;

        // Variables to store the starting and ending points of the rectangle
        private Point selectionBoxStartPoint;
        private Point selectionBoxEndPoint;
        private bool selectionEnded = false;

        private Image ResizeImage(string path, int width, int height)
        {
            using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(path);
            Image image = Image.FromStream(stream);
            image = new Bitmap(image, new Size(width, height));
            return image;
        }
        public ScreenCapture()
        {
            this.Hide();
            instance = this;
            synthesizer = new SpeechSynthesizer();
            settingsMenu = new SettingMenu();
            resourceManager = localization.ResourceManager;
            InitializeComponent();
            var supportedLanguages = SupportedLanguagesDictionary.dictionary;
            if (!supportedLanguages.ContainsKey(Settings.Default.InterfaceLanguage))
            {
                Settings.Default.InterfaceLanguage = "English";
                Settings.Default.Save();
            }

            // Updates Application Colors
            Settings.Default.SettingChanging += (sender, e) => { UpdateVisualsAndSettings(); };
            // Exits capture screen if user ALT Tabbed
            this.Deactivate += (sender, e) => { ExitScreenCapture(); };
            // Register the mouse event handlers
            captureBackground.MouseDown += new MouseEventHandler(pictureBox_MouseDown);
            captureBackground.MouseMove += new MouseEventHandler(pictureBox_MouseMove);
            captureBackground.Paint += new PaintEventHandler(DrawCaptureOverlay);
            UpdateVisualsAndSettings();
        }

        private Button CreateButton(string text, string imageFileName, EventHandler clickEvent)
        {
            int buttonHeight = captureMenu.Height / 5;
            Button button = new Button();
            button.Text = text;
            button.Size = new Size(captureMenu.Width, buttonHeight);
            button.Location = new Point(0, captureMenu.Height - (captureMenu.Controls.Count + 1) * buttonHeight);
            button.Dock = DockStyle.Bottom;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = Color.Transparent;
            button.ForeColor = Settings.Default.TextColor;
            button.Font = new Font("Century Gothic", 12, FontStyle.Regular);
            button.Image = ResizeImage(imageFileName, 24, 24);
            button.TextAlign = ContentAlignment.MiddleRight;
            button.ImageAlign = ContentAlignment.MiddleLeft;
            button.TextImageRelation = TextImageRelation.ImageBeforeText;
            button.Click += clickEvent;
            captureMenu.Controls.Add(button);
            return button;
        }
        private void InitializeComponent()
        {
            trayIcon = new();
            trayIcon.Text = "dotCapture";
            trayIcon.Icon = new Icon(Assembly.GetExecutingAssembly().GetManifestResourceStream("dotCapture.icons.program_icon.ico"));
            trayIcon.Visible = true;
            trayIcon.ContextMenuStrip = new ContextMenuStrip();
            trayIcon.ContextMenuStrip.BackColor = Settings.Default.BackgroundColor;
            settingsStripItem = trayIcon.ContextMenuStrip.Items.Add(resourceManager.GetString("settings.strip.menu"), null, openSettingsMenu_click);
            settingsStripItem.Image = null;
            exitStripItem = trayIcon.ContextMenuStrip.Items.Add(resourceManager.GetString("exit.strip.menu"), null, (object sender, EventArgs e) => { Application.Exit(); });
            exitStripItem.Image = null;

            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.ShowInTaskbar = false;

            captureBackground = new PictureBox();
            captureBackground.Size = Screen.PrimaryScreen.Bounds.Size;
            captureBackground.Location = new Point(0, 0);
            this.Controls.Add(captureBackground);

            // Create a new panel and add it to the form
            captureMenu = new Panel();
            captureMenu.BackColor = Settings.Default.BackgroundColor;
            captureMenu.Size = new Size(160, 150);
            //captureMenu.Focus();
            this.Controls.Add(captureMenu);

            playPauseButton = new Button();
            playPauseButton.Size = new Size(32, 32);
            playPauseButton.FlatStyle = FlatStyle.Flat;
            playPauseButton.FlatAppearance.BorderSize = 0;
            playPauseButton.BackColor = Settings.Default.BackgroundColor;
            //playPauseButton.Location = new Point(speechMenu.Width / 2 - playPauseButton.Width / 2, speechMenu.Height / 2 - playPauseButton.Height/2);
            playPauseButton.Image = ResizeImage("dotCapture.icons.play.png", 32, 32);
            synthesizer.StateChanged += (sender, e) =>
            {
                if (e.State == SynthesizerState.Speaking || e.State == SynthesizerState.Ready)
                    playPauseButton.Image = ResizeImage("dotCapture.icons.pause.png", 32, 32);
                else if (e.State == SynthesizerState.Paused)
                    playPauseButton.Image = ResizeImage("dotCapture.icons.play.png", 32, 32);
            };
            playPauseButton.Click += (sender, e) =>
            {
                if (synthesizer.State == SynthesizerState.Speaking || synthesizer.State == SynthesizerState.Ready)
                    synthesizer.Pause();
                else
                    synthesizer.Resume();
            };
            this.Controls.Add(playPauseButton);

            // Create the buttons and add them to the panel
            textToSpeechButton = CreateButton(resourceManager.GetString("text.to.speech.button"), "dotCapture.icons.text2speech.png", textToSpeechButton_Click);
            copyTextButton = CreateButton(resourceManager.GetString("copy.text.button"), "dotCapture.icons.copytext.png", copyTextButton_Click);
            translateButton = CreateButton(resourceManager.GetString("translate.button"), "dotCapture.icons.translate.png", translateButton_Click);
            saveButton = CreateButton(resourceManager.GetString("save.button"), "dotCapture.icons.save.png", saveButton_Click);
            copyButton = CreateButton(resourceManager.GetString("copy.button"), "dotCapture.icons.copy.png", copyButton_Click);
        }
        
        #region COMMON_CODE
        private Bitmap preprocessImage(Bitmap originalImage)
        {
            const int minDpi = 300; // Minimum DPI for the image (its best for tesseract)

            // Calculate the new width and height of the image
            float dpiX = originalImage.HorizontalResolution;
            float dpiY = originalImage.VerticalResolution;
            int newWidth = (int)(originalImage.Width * minDpi / dpiX);
            int newHeight = (int)(originalImage.Height * minDpi / dpiY);

            // Resize the image
            Bitmap resizedImage = new Bitmap(newWidth, newHeight);
            using (Graphics g = Graphics.FromImage(resizedImage))
            {
                g.DrawImage(originalImage, 0, 0, newWidth, newHeight);
            }
            //Clipboard.SetDataObject(resizedImage);
            // Convert the image to grayscale
            Bitmap grayscale = new Bitmap(resizedImage.Width, resizedImage.Height);
            using (Graphics g = Graphics.FromImage(grayscale))
            {
                // Create the grayscale ColorMatrix
                ColorMatrix colorMatrix = new ColorMatrix(
                    new float[][]
                    {
                new float[] {.3f, .3f, .3f, 0, 0},
                new float[] {.59f, .59f, .59f, 0, 0},
                new float[] {.11f, .11f, .11f, 0, 0},
                new float[] {0, 0, 0, 1, 0},
                new float[] {0, 0, 0, 0, 1}
                    });

                // Create the ImageAttributes object and set its color matrix
                ImageAttributes attributes = new ImageAttributes();
                attributes.SetColorMatrix(colorMatrix);

                // Draw the image using the grayscale color matrix
                g.DrawImage(resizedImage, new Rectangle(0, 0, resizedImage.Width, resizedImage.Height),
                    0, 0, resizedImage.Width, resizedImage.Height, GraphicsUnit.Pixel, attributes);
            }

            // Adjust the contrast of the image
            const float contrast = 1.2f; // change this value to adjust the contrast
            Bitmap contrastAdjusted = new Bitmap(grayscale.Width, grayscale.Height);
            using (Graphics g = Graphics.FromImage(contrastAdjusted))
            {
                float[][] colorMatrixElements = {
            new float[] {contrast, 0, 0, 0, 0}, // red scaling factor of 2
            new float[] {0, contrast, 0, 0, 0}, // green scaling factor of 2
            new float[] {0, 0, contrast, 0, 0}, // blue scaling factor of 2
            new float[] {0, 0, 0, 1, 0},       // alpha scaling factor of 1
            new float[] {0, 0, 0, 0, 1}};      // three translations of 0

                ColorMatrix colorMatrix = new ColorMatrix(colorMatrixElements);

                // Create the ImageAttributes object and set its color matrix
                ImageAttributes attributes = new ImageAttributes();
                attributes.SetColorMatrix(colorMatrix);

                // Draw the image using the contrast-adjusted color matrix
                g.DrawImage(grayscale, new Rectangle(0, 0, grayscale.Width, grayscale.Height),
                    0, 0, grayscale.Width, grayscale.Height, GraphicsUnit.Pixel, attributes);
            }

            return contrastAdjusted;
        }
        private Rectangle GetCroppedRectangle(){
            int x = Math.Min(selectionBoxStartPoint.X, selectionBoxEndPoint.X);
            int y = Math.Min(selectionBoxStartPoint.Y, selectionBoxEndPoint.Y);

            // Calculate the width and height of the rectangle
            int width = Math.Abs(selectionBoxStartPoint.X - selectionBoxEndPoint.X);
            int height = Math.Abs(selectionBoxStartPoint.Y - selectionBoxEndPoint.Y);
            return new Rectangle(x, y, width, height);
        }
        #endregion

        #region SETTINGS
        private void openSettingsMenu_click(object sender, EventArgs e)
        {
            settingsMenu.Show();
        }
        private void UpdateVisualsAndSettings()
        {
            //Update background color
            ChangeBackgroundControlColors(this, Settings.Default.BackgroundColor);
            ChangeBackgroundControlColors(trayIcon.ContextMenuStrip, Settings.Default.BackgroundColor);
            //Update foreground color
            ChangeForegroundControlColors(this, Settings.Default.TextColor);
            ChangeForegroundControlColors(trayIcon.ContextMenuStrip, Settings.Default.TextColor);
            //Update hover color
            ChangeHoverControlColors(this, Settings.Default.ButtonHoverColor);
            ChangeHoverControlColors(trayIcon.ContextMenuStrip, Settings.Default.ButtonHoverColor);
            //Update localization option
            var supportedLanguages = SupportedLanguagesDictionary.dictionary;
            if (supportedLanguages.ContainsKey(Settings.Default.InterfaceLanguage))
            {
                //Debug.WriteLine(Settings.Default.InterfaceLanguage);
                localization.Culture = new CultureInfo(supportedLanguages[Settings.Default.InterfaceLanguage]);
                Thread.CurrentThread.CurrentUICulture = new CultureInfo(supportedLanguages[Settings.Default.InterfaceLanguage]);
                ReloadTextLocalization();
            }
        }
        private void ReloadTextLocalization()
        {
            //Debug.WriteLine(localization.Culture.ToString());
            //if anyone knows a way to this without hardcode tell me please
            textToSpeechButton.Text = resourceManager.GetString("text.to.speech.button");
            copyTextButton.Text = resourceManager.GetString("copy.text.button");
            translateButton.Text = resourceManager.GetString("translate.button");
            saveButton.Text = resourceManager.GetString("save.button");
            copyButton.Text = resourceManager.GetString("copy.button");
            settingsStripItem.Text = resourceManager.GetString("settings.strip.menu");
            exitStripItem.Text = resourceManager.GetString("exit.strip.menu");
        }
        private void ChangeBackgroundControlColors(Control control, Color color)
        {
            control.BackColor = color;
            foreach (Control c in control.Controls)
                ChangeBackgroundControlColors(c, color);
        }
        private void ChangeForegroundControlColors(Control control, Color color)
        {
            control.ForeColor = color;
            if (control is Button)
            {
                Button button = (Button)control;
                if (button.Image != null)
                {
                    Bitmap originalImage = (Bitmap)button.Image;

                    // Create a new image with the same dimensions as the original image
                    Bitmap image = new Bitmap(originalImage.Width, originalImage.Height);

                    // Set the color of the pixels in the image to the desired color
                    for (int x = 0; x < image.Width; x++)
                    {
                        for (int y = 0; y < image.Height; y++)
                        {
                            Color pixelColor = originalImage.GetPixel(x, y);
                            if (pixelColor.A > 0)
                            {
                                image.SetPixel(x, y, Color.FromArgb(pixelColor.A, color));
                            }
                            else
                            {
                                image.SetPixel(x, y, Color.Transparent);
                            }
                        }
                    }
                    button.Image = image;
                }
            }
            foreach (Control c in control.Controls)
                ChangeForegroundControlColors(c, color);
        }
        private void ChangeHoverControlColors(Control control, Color color)
        {
            if (control is Button)
            {
                Button button = (Button)control;
                button.FlatAppearance.MouseOverBackColor = color;
            }
            foreach (Control c in control.Controls)
                ChangeHoverControlColors(c, color);
        }

        #endregion

        #region TEXT_TO_SPEECH
        private void textToSpeech_Play(int index)
        {
            string aux = "";
            for (int i = index; i < textToSpeechTexts.Count; i++)
                aux += $" {textToSpeechTexts[i]}";
            if (!string.IsNullOrEmpty(aux)){
                synthesizer.SpeakAsyncCancelAll();
                synthesizer.SpeakAsync(aux);
                synthesizer.Resume();
            }
            else
            {
                synthesizer.SpeakAsyncCancelAll();
                synthesizer.SpeakAsync(resourceManager.GetString("error.text.notfound"));
                synthesizer.Resume();
            }
        }
        private void generateSpeechButton(string buttonText,Rectangle buttonRect)
        {
            Button speechButton = new Button();
            speechButton.Cursor = Cursors.Hand;
            speechButton.Size = buttonRect.Size;
            speechButton.Location = buttonRect.Location;

            speechButton.FlatStyle = FlatStyle.Flat;
            speechButton.BackColor = Color.Transparent;
            speechButton.FlatAppearance.BorderSize = 1;
            speechButton.FlatAppearance.BorderColor = Color.Red;
            speechButton.FlatAppearance.MouseOverBackColor = Color.Red;
            speechButton.FlatAppearance.MouseDownBackColor = Color.Red;

            speechButton.Click += (sender, e) => {
                textToSpeech_Play(textToSpeechTexts.IndexOf(buttonText));
            };
            captureBackground.Controls.Add(speechButton);
        }
        private void textToSpeechButton_Click(object sender, EventArgs e)
        {
            Rectangle croppedRectangle = GetCroppedRectangle();
            // Save the image
            Bitmap bmpimage = ((Bitmap)captureBackground.Image).Clone(croppedRectangle, captureBackground.Image.PixelFormat);

            using var engine = new TesseractEngine(@"./tessdata", availableLanguages);
            using var page = engine.Process(preprocessImage(bmpimage));
            using var iter = page.GetIterator();
            iter.Begin();
            do
            {
                if (iter.TryGetBoundingBox(PageIteratorLevel.TextLine, out var rect))
                {

                    if (captureBackground.Image != null)
                    {
                        int old_x = (int)(rect.X1 * bmpimage.HorizontalResolution / 300);
                        int old_Y = (int)(rect.Y1 * bmpimage.VerticalResolution / 300);
                        int old_width = (int)(rect.Width * bmpimage.HorizontalResolution / 300);
                        int old_height = (int)(rect.Height * bmpimage.VerticalResolution / 300);

                        var curText = iter.GetText(PageIteratorLevel.TextLine);
                        curText = curText.TrimEnd('\n');
                        if (string.IsNullOrWhiteSpace(curText))
                            break;

                        textToSpeechTexts.Add(curText);

                        using var graphics = Graphics.FromImage(captureBackground.Image);
                        Size rectPadding = new Size(6, 6);
                        Rectangle adjustedRect = new Rectangle(croppedRectangle.X + old_x - rectPadding.Width / 2, croppedRectangle.Y + old_Y - rectPadding.Height / 2, old_width + rectPadding.Width, old_height + rectPadding.Height);

                        generateSpeechButton(curText, adjustedRect);
                    }
                }
                captureBackground.Refresh();
            } while (iter.Next(PageIteratorLevel.TextLine));
            playPauseButton.Location = new Point(croppedRectangle.X + (croppedRectangle.Width / 2) - (playPauseButton.Width / 2), croppedRectangle.Y - playPauseButton.Height);
            playPauseButton.Visible = true;
            playPauseButton.BringToFront();

            textToSpeech_Play(0);
        }
        #endregion

        #region COPY_TEXT
        private void copyTextButton_Click(object sender, EventArgs e)
        {
            // Save the image
            Bitmap bmpimage = ((Bitmap)captureBackground.Image).Clone(GetCroppedRectangle(), captureBackground.Image.PixelFormat);
            //Clipboard.SetDataObject(preprocessImage(bmpimage));
            using var engine = new TesseractEngine(@"./tessdata", availableLanguages);
            using var page = engine.Process(preprocessImage(bmpimage));
            // Get the recognized text as a string
            string text = page.GetText();

            // Copy the text to the clipboard
            if (text != "")
            {
                Clipboard.SetText(text);
                //Debug.WriteLine(text);
            }
            else
                //Debug.WriteLine(resourceManager.GetString("error.text.notfound"));

            ExitScreenCapture();
        }
        #endregion

        #region TRANSLATE_TEXT
        private void translateButton_Click(object sender, EventArgs e)
        {
            string selectedLanguage = null;
            Dictionary<string, string> languageCodes = new Dictionary<string, string>(){
                {"English", "en"},
                {"Spanish", "es"},
                {"Italian", "it"},
                {"Japanese", "ja"},
                {"Korean", "ko"},
                {"Portuguese", "pt"},
            };
            // Create a dropdown menu for selecting the output language
            ComboBox languageListComboBox = new ComboBox();
            // Set the size and position of the combobox
            languageListComboBox.Size = new Size(180, 20);
            languageListComboBox.Location = new Point(10, 10);
            // Add a list of available languages to the dropdown menu
            languageListComboBox.DataSource = languageCodes.Keys.ToList();

            // Create a "OK" button for submitting the selected language
            Button okButton = new Button();
            // Set the size and position of the button
            okButton.Size = new Size(90, 30);
            okButton.Location = new Point(10, 40);
            okButton.Text = resourceManager.GetString("language.panel.ok");

            // Create a "Cancel" button for canceling the selection
            Button cancelButton = new Button();
            // Set the size and position of the button
            cancelButton.Size = new Size(90, 30);
            cancelButton.Location = new Point(100, 40);
            cancelButton.Text = resourceManager.GetString("language.panel.cancel");

            // Create a panel to contain the dropdown menu and buttons
            languagePanel = new Panel();
            languagePanel.Controls.Add(languageListComboBox);
            languagePanel.Controls.Add(okButton);
            languagePanel.Controls.Add(cancelButton);

            Rectangle croppedRectangle = GetCroppedRectangle();

            // Set the size and position of the panel
            languagePanel.Size = new Size(200, 100);
            languagePanel.Location = new Point(
                croppedRectangle.Width / 2 - languagePanel.Size.Width / 2,
                croppedRectangle.Height / 2 - languagePanel.Size.Height / 2);

            // Add the panel to the form
            this.Controls.Add(languagePanel);
            languagePanel.BringToFront();

            // Add an event handler for the OK button
            okButton.Click += (sender, e) =>
            {
                // Close the panel and return the selected language
                languagePanel.Visible = false;
                selectedLanguage = languageCodes[languageListComboBox.SelectedItem.ToString()];
                languagePanel.Dispose();
                Translate(selectedLanguage);
            };
            // Add an event handler for the Cancel button
            cancelButton.Click += (sender, e) =>
            {
                // Close the panel and return null
                languagePanel.Visible = false;
                selectedLanguage = null;
                languagePanel.Dispose();
            };
            // Show the panel
            languagePanel.Visible = true;
        }
        private void Translate(string outputLanguage)
        {
            if (outputLanguage != null)
            {
                Rectangle croppedRectangle = GetCroppedRectangle();
                // Save the image
                Bitmap bmpimage = ((Bitmap)captureBackground.Image).Clone(croppedRectangle, captureBackground.Image.PixelFormat);

                using var engine = new TesseractEngine(@"./tessdata", availableLanguages);
                using var page = engine.Process(preprocessImage(bmpimage));
                using var iter = page.GetIterator();
                iter.Begin();
                do
                {
                    if (iter.TryGetBoundingBox(PageIteratorLevel.TextLine, out var rect))
                    {
                        if (captureBackground.Image == null)
                            return;
                        int old_x = (int)(rect.X1 * bmpimage.HorizontalResolution / 300);
                        int old_Y = (int)(rect.Y1 * bmpimage.VerticalResolution / 300);
                        int old_width = (int)(rect.Width * bmpimage.HorizontalResolution / 300);
                        int old_height = (int)(rect.Height * bmpimage.VerticalResolution / 300);

                        var curText = iter.GetText(PageIteratorLevel.TextLine);
                        curText = curText.TrimEnd('\n');
                        if (string.IsNullOrWhiteSpace(curText))
                            break;

                        // Translate the text
                        string translatedText = TranslateText(curText, DetectText(curText), outputLanguage);
                        translatedText = translatedText.Replace('\n', ' ');

                        // Draw the translated text on top of the image
                        using var graphics = Graphics.FromImage(captureBackground.Image);
                        Size rectPadding = new Size(6, 6);
                        Rectangle adjustedRect = new Rectangle(croppedRectangle.X + old_x - rectPadding.Width / 2, croppedRectangle.Y + old_Y - rectPadding.Height / 2, old_width + rectPadding.Width, old_height + rectPadding.Height);
                        float maxFontSize = 72;
                        while (maxFontSize > 6)
                        {
                            using (Font font = new Font("Arial", maxFontSize))
                            {
                                var calc = graphics.MeasureString(translatedText, font, adjustedRect.Width, StringFormat.GenericDefault);
                                if (calc.Height <= adjustedRect.Height)
                                {
                                    break;
                                }
                            }
                            maxFontSize -= 1f;
                        }
                        Font scaledFont = new Font("Arial", maxFontSize);

                        Color brushColor = bmpimage.GetPixel((int)(old_width/2), (int)old_Y);
                        graphics.FillRectangle(new SolidBrush(brushColor), adjustedRect);
                        graphics.DrawString(translatedText, scaledFont, new SolidBrush(Color.FromArgb(255 - brushColor.R, 255 - brushColor.G, 255 - brushColor.B)), adjustedRect.X, adjustedRect.Y + rectPadding.Height / 2);
                        //var calc2 = graphics.MeasureString(translatedText, scaledFont, adjustedRect.Size, StringFormat.GenericDefault);
                        //graphics.DrawRectangle(Pens.Red, adjustedRect.X, adjustedRect.Y, (int)calc2.Width, (int)calc2.Height);
                    }
                } while (iter.Next(PageIteratorLevel.TextLine));
                // Display the modified image on the screen
                captureBackground.Refresh();
            }
        }
        private string SendMicrosoftTranslatorRequest(string route, object[] body)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "dotCapture.appsettings.json";

            using Stream stream = assembly.GetManifestResourceStream(resourceName);
            using StreamReader reader = new StreamReader(stream);
            string config = reader.ReadToEnd();
            var configuration = JsonConvert.DeserializeObject<Dictionary<string, string>>(config);
            string key = configuration["TRANSLATOR_API_KEY"];
            string location = configuration["TRANSLATOR_API_LOCATION"];
            string endpoint = "https://api.cognitive.microsofttranslator.com";
            var requestBody = JsonConvert.SerializeObject(body);

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", key);
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Region", location);

            using var request = new HttpRequestMessage();
            request.Method = HttpMethod.Post;
            request.RequestUri = new Uri(endpoint + route);
            request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
            HttpResponseMessage response = client.SendAsync(request).Result;

            string result = response.Content.ReadAsStringAsync().Result;
            return result;
        }
        private string DetectText(string textToDetect)
        {
            object[] body = new object[] { new { Text = textToDetect } };
            string route = $"/detect?api-version=3.0";
            string result = SendMicrosoftTranslatorRequest(route, body);
            dynamic jsonObj = JsonConvert.DeserializeObject(result);
            string detectedLanguage = jsonObj[0].language;
            return detectedLanguage;
        }
        private string TranslateText(string textToTranslate, string fromLang, string toLang)
        {
            object[] body = new object[] { new { Text = textToTranslate } };
            string route = $"/translate?api-version=3.0&from={fromLang}&to={toLang}";
            string result = SendMicrosoftTranslatorRequest(route, body);
            dynamic jsonObj = JsonConvert.DeserializeObject(result);
            string translatedText = jsonObj[0].translations[0].text;
            return translatedText;
        }
        #endregion

        #region SAVE_IMAGE
        private void saveButton_Click(object sender, EventArgs e)
        {
            // Save the image to a variable before showing dialog
            using Bitmap bmpimage = ((Bitmap)captureBackground.Image).Clone(GetCroppedRectangle(), captureBackground.Image.PixelFormat);
            // Create a SaveFileDialog object
            using SaveFileDialog saveFileDialog = new SaveFileDialog();
            // Set the filter for the file dialog
            saveFileDialog.Filter = "PNG Image|*.png|JPEG Image|*.jpg|Bitmap Image|*.bmp|GIF Image|*.gif";

            // Set the default file extension
            saveFileDialog.DefaultExt = "png";

            // Open the dialog and check if the user clicked OK
            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                // Get the file name and extension
                string fileName = saveFileDialog.FileName;
                string fileExtension = Path.GetExtension(fileName);

                // Convert the file extension to a ImageFormat object
                ImageFormat imageFormat = null;
                switch (fileExtension)
                {
                    case ".jpg":
                        imageFormat = ImageFormat.Jpeg;
                        break;
                    case ".bmp":
                        imageFormat = ImageFormat.Bmp;
                        break;
                    case ".gif":
                        imageFormat = ImageFormat.Gif;
                        break;
                    case ".png":
                        imageFormat = ImageFormat.Png;
                        break;
                }

                // Check if the image format is not null
                if (imageFormat != null)
                {
                    bmpimage.Save(fileName, imageFormat);
                    ExitScreenCapture();
                }
            }
        }
        #endregion

        #region COPY_IMAGE
        private void copyButton_Click(object sender, EventArgs e)
        {
            int x = Math.Min(selectionBoxStartPoint.X, selectionBoxEndPoint.X);
            int y = Math.Min(selectionBoxStartPoint.Y, selectionBoxEndPoint.Y);

            // Calculate the width and height of the rectangle
            int width = Math.Abs(selectionBoxStartPoint.X - selectionBoxEndPoint.X);
            int height = Math.Abs(selectionBoxStartPoint.Y - selectionBoxEndPoint.Y);
            Rectangle cropRect = new Rectangle(x, y, width, height);
            Bitmap bmpimage = ((Bitmap)captureBackground.Image).Clone(cropRect, captureBackground.Image.PixelFormat);
            Clipboard.SetDataObject(bmpimage);
            ExitScreenCapture();
        }
        #endregion

        // Paint event to draw the rectangle on the screen
        private void DrawCaptureOverlay(object sender, PaintEventArgs e)
        {
            // Create a brush with a semi-transparent color
            using SolidBrush brush = new SolidBrush(Color.FromArgb(128, Color.Black)); // Set the alpha component to 128 (50% transparent)

            // Get the dimensions of the screen
            int screenWidth = Screen.PrimaryScreen.Bounds.Width;
            int screenHeight = Screen.PrimaryScreen.Bounds.Height;

            // Check if the starting and ending points have been set
            if (!selectionBoxStartPoint.IsEmpty && !selectionBoxEndPoint.IsEmpty)
            {
                Rectangle croppedRectangle = GetCroppedRectangle();

                if (selectionEnded == true)
                {
                    int captureMenu_Width = 160;
                    int captureMenu_Height = Math.Abs(croppedRectangle.Y - croppedRectangle.Bottom);
                    captureMenu_Height = Math.Clamp(captureMenu_Height / 2, 150, 648);

                    int captureMenu_X = croppedRectangle.Right + 1;
                    if (captureMenu_X + captureMenu_Width >= Screen.PrimaryScreen.Bounds.Width)
                        captureMenu_X = croppedRectangle.X - captureMenu_Width - 1;

                    int captureMenu_Y = croppedRectangle.Bottom - captureMenu_Height + 1;
                    if (captureMenu_Y < 0)// Screen.PrimaryScreen.Bounds.Height)
                        captureMenu_Y = croppedRectangle.Y - 1;

                    captureMenu.Location = new Point(captureMenu_X, captureMenu_Y);
                    captureMenu.Size = new Size(captureMenu_Width, captureMenu_Height);
                    captureMenu.BringToFront();
                    captureMenu.Visible = true;
                    this.ActiveControl = captureMenu;
                }

                // Draw the rectangle on the picture
                e.Graphics.DrawRectangle(new Pen(Color.White, 2), croppedRectangle);
                e.Graphics.ExcludeClip(new Rectangle(croppedRectangle.X - 2, croppedRectangle.Y - 2, croppedRectangle.Width + 4, croppedRectangle.Height + 4));
            }
            // Fill the entire screen with the semi-transparent brush
            e.Graphics.FillRectangle(brush, 0, 0, screenWidth, screenHeight);
        }
        // MouseDown event handler
        private void pictureBox_MouseDown(object sender, MouseEventArgs e)
        {
            if (selectionEnded)
                return;
            // Check if the selection has already started
            if (selectionBoxStartPoint == Point.Empty)
            {
                // Set the starting point of the rectangle
                selectionBoxStartPoint = e.Location;
            }
            else
            {
                // Set the ending point of the rectangle
                selectionBoxEndPoint = e.Location;

                // Set the flag to indicate that the selection has ended
                selectionEnded = true;

                // Invalidate the pictureBox to trigger a repaint
                captureBackground.Invalidate();
            }
        }
        // MouseMove event handler
        private void pictureBox_MouseMove(object sender, MouseEventArgs e)
        {
            if (selectionEnded)
                return;
            // Store the current mouse position as the ending point of the rectangle
            selectionBoxEndPoint = e.Location;
            // Invalidate the pictureBox to trigger a repaint
            captureBackground.Invalidate();
        }
        private Bitmap CaptureScreen()
        {

            // Create a bitmap object to store the screenshot
            Bitmap bmpScreenshot = new Bitmap(Screen.PrimaryScreen.Bounds.Width,
                                             Screen.PrimaryScreen.Bounds.Height);

            // Create a graphics object from the bitmap
            Graphics gfxScreenshot = Graphics.FromImage(bmpScreenshot);

            // Copy the entire screen to the graphics object
            gfxScreenshot.CopyFromScreen(Screen.PrimaryScreen.Bounds.X,
                                        Screen.PrimaryScreen.Bounds.Y,
                                        0, 0,
                                        Screen.PrimaryScreen.Bounds.Size,
                                        CopyPixelOperation.SourceCopy);

            // Return the screenshot
            return bmpScreenshot;
        }
        public void PressedCaptureScreen()
        {
            // Capture the screen and set the PictureBox image
            if(!this.Visible)
            {
                captureBackground.Image = CaptureScreen();
                this.Show();
            }
        }
        public void ExitScreenCapture()
        {
            if (this.Visible)
            {
                selectionBoxEndPoint = Point.Empty;
                selectionBoxStartPoint = Point.Empty;
                selectionEnded = false;
                captureBackground.Image = null;
                captureMenu.Visible = false;
                playPauseButton.Visible = false;
                textToSpeechTexts.Clear();
                synthesizer.SpeakAsyncCancelAll();
                if(languagePanel != null)
                    languagePanel.Dispose();
                foreach (Control c in captureBackground.Controls.OfType<Button>().ToList())
                {
                    captureBackground.Controls.Remove(c);
                    c.Dispose();
                }

                this.Visible = false;
                GC.Collect();
            }
        }
    }
}
