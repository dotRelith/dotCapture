using Newtonsoft.Json;
using System;
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

        // Property to get the singleton instance
        public static ScreenCapture Instance
        {
            get { return instance; }
        }

        private PictureBox captureBackground;
        private Panel captureMenu, languagePanel;
        private NotifyIcon trayIcon;
        private Button copyButton, saveButton, translateButton, copyTextButton;
        
        private Image ResizeImage(string path, int width, int height)
        {
            using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(path);
            Image image = Image.FromStream(stream);
            image = new Bitmap(image, new Size(width, height));
            return image;
        }
        public ScreenCapture()
        {
            instance = this;
            // Create a new form and register for the KeyUp event

            InitializeComponent();

            this.FormClosing += dotCapture_FormClosing;
            this.Deactivate += (sender, e) => { ExitScreenCapture(); };
        }
        private Button CreateButton(string text, string imageFileName, EventHandler clickEvent)
        {
            int buttonHeight = captureMenu.Height / 4;
            Button button = new Button();
            button.Text = text;
            button.Size = new Size(captureMenu.Width, buttonHeight);
            button.Location = new Point(0, captureMenu.Height - (captureMenu.Controls.Count + 1) * buttonHeight);
            button.Dock = DockStyle.Bottom;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
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
            trayIcon.ContextMenuStrip.Items.Add("Exit", null, dotCapture_FormClosing);

            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.ShowInTaskbar = false;
            this.Opacity = 0;

            captureBackground = new PictureBox();
            captureBackground.Size = Screen.PrimaryScreen.Bounds.Size;
            captureBackground.Location = new Point(0, 0);
            this.Controls.Add(captureBackground);

            // Create a new panel and add it to the form
            captureMenu = new Panel();
            captureMenu.Size = new Size(128, 150);
            captureMenu.Focus();
            this.Controls.Add(captureMenu);

            // Create the buttons and add them to the panel
            copyTextButton = CreateButton("Copy Text", "dotCapture.icons.copytext.png", copyTextButton_Click);
            translateButton = CreateButton("Translate", "dotCapture.icons.translate.png", translateButton_Click);
            saveButton = CreateButton("Save", "dotCapture.icons.save.png", saveButton_Click);
            copyButton = CreateButton("Copy", "dotCapture.icons.copy.png", copyButton_Click);

            // Register the mouse event handlers
            captureBackground.MouseDown += new MouseEventHandler(pictureBox_MouseDown);
            captureBackground.MouseMove += new MouseEventHandler(pictureBox_MouseMove);

            captureBackground.Paint += new PaintEventHandler(Form_Paint);
        }

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

        private void copyTextButton_Click(object sender, EventArgs e)
        {
            int x = Math.Min(selectionBoxStartPoint.X, selectionBoxEndPoint.X);
            int y = Math.Min(selectionBoxStartPoint.Y, selectionBoxEndPoint.Y);

            // Calculate the width and height of the rectangle
            int width = Math.Abs(selectionBoxStartPoint.X - selectionBoxEndPoint.X);
            int height = Math.Abs(selectionBoxStartPoint.Y - selectionBoxEndPoint.Y);
            Rectangle cropRect = new Rectangle(x, y, width, height);
            // Save the image
            Bitmap bmpimage = ((Bitmap)captureBackground.Image).Clone(cropRect, captureBackground.Image.PixelFormat);
            //Clipboard.SetDataObject(preprocessImage(bmpimage));
            using var engine = new TesseractEngine(@"./tessdata", "eng+ita+jpn+jpn_vert+kor+kor_vert+por+spa");
            using var page = engine.Process(preprocessImage(bmpimage));
            // Get the recognized text as a string
            string text = page.GetText();

            // Copy the text to the clipboard
            if (text != "")
            {
                Clipboard.SetText(text);
                Debug.WriteLine(text);
            }
            else
                Debug.WriteLine("Should'nt have a null text");

            ExitScreenCapture();
        }
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
            okButton.Text = "OK";

            // Create a "Cancel" button for canceling the selection
            Button cancelButton = new Button();
            // Set the size and position of the button
            cancelButton.Size = new Size(90, 30);
            cancelButton.Location = new Point(100, 40);
            cancelButton.Text = "Cancel";

            // Create a panel to contain the dropdown menu and buttons
            languagePanel = new Panel();
            languagePanel.Controls.Add(languageListComboBox);
            languagePanel.Controls.Add(okButton);
            languagePanel.Controls.Add(cancelButton);

            // Set the size and position of the panel
            languagePanel.Size = new Size(200, 100);
            languagePanel.Location = new Point(
                this.ClientSize.Width / 2 - languagePanel.Size.Width / 2,
                this.ClientSize.Height / 2 - languagePanel.Size.Height / 2);

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
                int x = Math.Min(selectionBoxStartPoint.X, selectionBoxEndPoint.X);
                int y = Math.Min(selectionBoxStartPoint.Y, selectionBoxEndPoint.Y);

                // Calculate the width and height of the rectangle
                int width = Math.Abs(selectionBoxStartPoint.X - selectionBoxEndPoint.X);
                int height = Math.Abs(selectionBoxStartPoint.Y - selectionBoxEndPoint.Y);
                Rectangle cropRect = new Rectangle(x, y, width, height);
                // Save the image
                Bitmap bmpimage = ((Bitmap)captureBackground.Image).Clone(cropRect, captureBackground.Image.PixelFormat);

                using var engine = new TesseractEngine(@"./tessdata", "eng+ita+jpn+jpn_vert+kor+kor_vert+por+spa");
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
                        Rectangle adjustedRect = new Rectangle(x + old_x - rectPadding.Width / 2, y + old_Y - rectPadding.Height / 2, old_width + rectPadding.Width, old_height + rectPadding.Height);
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

                        graphics.FillRectangle(Brushes.White, adjustedRect);
                        graphics.DrawString(translatedText, scaledFont, Brushes.Black, adjustedRect.X, adjustedRect.Y + rectPadding.Height / 2);
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
        private void saveButton_Click(object sender, EventArgs e)
        {
            int x = Math.Min(selectionBoxStartPoint.X, selectionBoxEndPoint.X);
            int y = Math.Min(selectionBoxStartPoint.Y, selectionBoxEndPoint.Y);

            // Calculate the width and height of the rectangle
            int width = Math.Abs(selectionBoxStartPoint.X - selectionBoxEndPoint.X);
            int height = Math.Abs(selectionBoxStartPoint.Y - selectionBoxEndPoint.Y);
            Rectangle cropRect = new Rectangle(x, y, width, height);
            // Save the image to a variable before showing dialog
            using Bitmap bmpimage = ((Bitmap)captureBackground.Image).Clone(cropRect, captureBackground.Image.PixelFormat);
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
                System.Drawing.Imaging.ImageFormat imageFormat = null;
                switch (fileExtension)
                {
                    case ".jpg":
                        imageFormat = System.Drawing.Imaging.ImageFormat.Jpeg;
                        break;
                    case ".bmp":
                        imageFormat = System.Drawing.Imaging.ImageFormat.Bmp;
                        break;
                    case ".gif":
                        imageFormat = System.Drawing.Imaging.ImageFormat.Gif;
                        break;
                    case ".png":
                        imageFormat = System.Drawing.Imaging.ImageFormat.Png;
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
        private void dotCapture_FormClosing(object sender, EventArgs e)
        {
            trayIcon.Dispose();
            trayIcon.Visible = false;
            //Debug.WriteLine("rasd");
            Application.Exit();
        }
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

        // Paint event to draw the rectangle on the screen
        private void Form_Paint(object sender, PaintEventArgs e)
        {
            // Create a brush with a semi-transparent color
            using SolidBrush brush = new SolidBrush(Color.FromArgb(128, Color.Black)); // Set the alpha component to 128 (50% transparent)

            // Get the dimensions of the screen
            int screenWidth = Screen.PrimaryScreen.Bounds.Width;
            int screenHeight = Screen.PrimaryScreen.Bounds.Height;

            // Check if the starting and ending points have been set
            if (!selectionBoxStartPoint.IsEmpty && !selectionBoxEndPoint.IsEmpty)
            {
                // Calculate the top-left corner of the rectangle
                Point topLeftPoint = new Point(Math.Min(selectionBoxStartPoint.X, selectionBoxEndPoint.X), Math.Min(selectionBoxStartPoint.Y, selectionBoxEndPoint.Y));
                Point bottomRightPoint = new Point(Math.Max(selectionBoxStartPoint.X, selectionBoxEndPoint.X), Math.Max(selectionBoxStartPoint.Y, selectionBoxEndPoint.Y));

                // Calculate the width and height of the rectangle
                int width = Math.Abs(selectionBoxStartPoint.X - selectionBoxEndPoint.X);
                int height = Math.Abs(selectionBoxStartPoint.Y - selectionBoxEndPoint.Y);

                if (selectionEnded == true)
                {
                    int captureMenu_Width = 128;
                    int captureMenu_Height = Math.Abs(topLeftPoint.Y - bottomRightPoint.Y);
                    captureMenu_Height = Math.Clamp(captureMenu_Height / 2, 150, 648);

                    int captureMenu_X = bottomRightPoint.X + 1;
                    if (captureMenu_X + captureMenu_Width >= Screen.PrimaryScreen.Bounds.Width)
                        captureMenu_X = topLeftPoint.X - captureMenu_Width - 1;

                    int captureMenu_Y = bottomRightPoint.Y - captureMenu_Height + 1;
                    if (captureMenu_Y < 0)// Screen.PrimaryScreen.Bounds.Height)
                        captureMenu_Y = topLeftPoint.Y - 1;

                    captureMenu.Location = new Point(captureMenu_X, captureMenu_Y);
                    captureMenu.Size = new Size(captureMenu_Width, captureMenu_Height);
                    captureMenu.BringToFront();
                    captureMenu.Visible = true;
                    this.ActiveControl = captureMenu;
                }

                // Draw the rectangle on the picture
                e.Graphics.DrawRectangle(new Pen(Color.White, 2), new Rectangle(topLeftPoint.X, topLeftPoint.Y, width, height));
                e.Graphics.ExcludeClip(new Rectangle(topLeftPoint.X - 2, topLeftPoint.Y - 2, width + 4, height + 4));
            }
            // Fill the entire screen with the semi-transparent brush
            e.Graphics.FillRectangle(brush, 0, 0, screenWidth, screenHeight);
        }

        // Variables to store the starting and ending points of the rectangle
        private Point selectionBoxStartPoint;
        private Point selectionBoxEndPoint;
        private bool selectionEnded = false;
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
            if(this.Opacity == 0)
            {
                captureBackground.Image = CaptureScreen();
                this.Activate();
                this.Opacity = 1;
            }
        }
        public void ExitScreenCapture()
        {
            if (this.Opacity != 0)
            {
                selectionBoxEndPoint = Point.Empty;
                selectionBoxStartPoint = Point.Empty;
                selectionEnded = false;
                captureBackground.Image = null;
                captureMenu.Visible = false;
                if(languagePanel != null)
                    languagePanel.Dispose();

                this.Opacity = 0;
                GC.Collect();
            }
        }
    }
}
