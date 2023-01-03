using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
using System.Threading.Tasks;
using System.Windows.Forms;
using Tesseract;

namespace dotCapture
{
    class ScreenCapture : Form
    {
        const int WS_EX_TOOLWINDOW = 0x00000080;
        Dictionary<string, string> supportedLanguages = new Dictionary<string, string>
            {
                {"English","eng" },
                {"Spanish","spa" },
                {"Portuguese","por" },
                {"Japanese","jpn" },
                {"Korean","kor" },
            };
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
        private Panel captureMenu;
        private NotifyIcon trayIcon;
        private Button copyButton, saveButton, translateButton, copyTextButton;

        private Image ResizeImage(string path, int width, int height)
        {
            Image image = Image.FromFile(path);
            image = new Bitmap(image, new Size(width, height));
            return image;
        }
        public ScreenCapture()
        {
            instance = this;
            // Create a new form and register for the KeyUp event

            trayIcon = new();
            trayIcon.Icon = new Icon(SystemIcons.Application, 40, 40);
            trayIcon.Text = "My Application";
            trayIcon.Visible = true;
            trayIcon.ContextMenuStrip = new ContextMenuStrip();
            trayIcon.ContextMenuStrip.Items.Add("Exit", null, OnExit);

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

            int buttonHeight = captureMenu.Height / 4;

            // Create the buttons and add them to the panel
            copyButton = new Button();
            copyButton.Text = "Copy";
            copyButton.Size = new Size(captureMenu.Width, buttonHeight);
            copyButton.Location = new Point(0, captureMenu.Height - 1 * buttonHeight);
            captureMenu.Controls.Add(copyButton);

            saveButton = new Button();
            saveButton.Text = "Save";
            saveButton.Size = new Size(captureMenu.Width, buttonHeight);
            saveButton.Location = new Point(0, captureMenu.Height - 2 * buttonHeight);
            captureMenu.Controls.Add(saveButton);

            translateButton = new Button();
            translateButton.Text = "Translate";
            translateButton.Size = new Size(captureMenu.Width, buttonHeight);
            translateButton.Location = new Point(0, captureMenu.Height - 3 * buttonHeight);
            captureMenu.Controls.Add(translateButton);

            copyTextButton = new Button();
            copyTextButton.Text = "Copy Text";
            copyTextButton.Size = new Size(captureMenu.Width, buttonHeight);
            copyTextButton.Location = new Point(0, captureMenu.Height - 4 * buttonHeight);
            captureMenu.Controls.Add(copyTextButton);

            copyButton.Anchor = AnchorStyles.Left | AnchorStyles.Bottom | AnchorStyles.Right;
            saveButton.Anchor = AnchorStyles.Left | AnchorStyles.Bottom | AnchorStyles.Right;
            translateButton.Anchor = AnchorStyles.Left | AnchorStyles.Bottom | AnchorStyles.Right;
            copyTextButton.Anchor = AnchorStyles.Left | AnchorStyles.Bottom | AnchorStyles.Right;

            copyButton.FlatStyle = FlatStyle.Flat;
            saveButton.FlatStyle = FlatStyle.Flat;
            translateButton.FlatStyle = FlatStyle.Flat;
            copyTextButton.FlatStyle = FlatStyle.Flat;

            copyButton.FlatAppearance.BorderSize = 0;
            saveButton.FlatAppearance.BorderSize = 0;
            translateButton.FlatAppearance.BorderSize = 0;
            copyTextButton.FlatAppearance.BorderSize = 0;

            copyButton.Font = new Font("Century Gothic", 12, FontStyle.Regular);
            saveButton.Font = new Font("Century Gothic", 12, FontStyle.Regular);
            translateButton.Font = new Font("Century Gothic", 12, FontStyle.Regular);
            copyTextButton.Font = new Font("Century Gothic", 12, FontStyle.Regular);

            copyButton.Image = ResizeImage("icons/copy.png", 24, 24);
            copyButton.ImageAlign = ContentAlignment.MiddleLeft;
            saveButton.Image = ResizeImage("icons/diskette.png", 24, 24);
            saveButton.ImageAlign = ContentAlignment.MiddleLeft;
            translateButton.Image = ResizeImage("icons/translation.png", 24, 24);
            translateButton.ImageAlign = ContentAlignment.MiddleLeft;
            copyTextButton.Image = ResizeImage("icons/ocr.png", 24, 24);
            copyTextButton.ImageAlign = ContentAlignment.MiddleLeft;

            copyButton.TextAlign = ContentAlignment.MiddleRight;
            saveButton.TextAlign = ContentAlignment.MiddleRight;
            translateButton.TextAlign = ContentAlignment.MiddleRight;
            copyTextButton.TextAlign = ContentAlignment.MiddleRight;

            // Register the button click event handlers
            copyButton.Click += new EventHandler(copyButton_Click);
            saveButton.Click += new EventHandler(saveButton_Click);
            translateButton.Click += new EventHandler(translateButton_Click);
            copyTextButton.Click += new EventHandler(copyTextButton_Click);

            // Register the mouse event handlers
            captureBackground.MouseDown += new MouseEventHandler(pictureBox_MouseDown);
            captureBackground.MouseMove += new MouseEventHandler(pictureBox_MouseMove);

            captureBackground.Paint += new PaintEventHandler(Form_Paint);

            this.Deactivate += (sender, e) => { ExitScreenCapture(); };
        }
        private Bitmap preprocessImage(Bitmap originalImage)
        {
            const int minDpi = 300; // Minimum DPI for the image

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

            using (var engine = new TesseractEngine(@"./tessdata", "eng+ita+jpn+jpn_vert+kor+kor_vert+por+spa"))
            {
                using (var page = engine.Process(preprocessImage(bmpimage)))
                {
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
            }
        }

        private void translateButton_Click(object sender, EventArgs e)
        {
            int x = Math.Min(selectionBoxStartPoint.X, selectionBoxEndPoint.X);
            int y = Math.Min(selectionBoxStartPoint.Y, selectionBoxEndPoint.Y);

            // Calculate the width and height of the rectangle
            int width = Math.Abs(selectionBoxStartPoint.X - selectionBoxEndPoint.X);
            int height = Math.Abs(selectionBoxStartPoint.Y - selectionBoxEndPoint.Y);
            Rectangle cropRect = new Rectangle(x, y, width, height);
            // Save the image
            Bitmap bmpimage = ((Bitmap)captureBackground.Image).Clone(cropRect, captureBackground.Image.PixelFormat);

            using (var engine = new TesseractEngine(@"./tessdata", "eng+ita+jpn+jpn_vert+kor+kor_vert+por+spa"))
            {
                using (var page = engine.Process(preprocessImage(bmpimage)))
                {
                    using (var iter = page.GetIterator())
                    {
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
                                Debug.WriteLine(curText);

                                // Translate the text
                                string translatedText = TranslateText(curText, "en", "pt");
                                translatedText = translatedText.Replace('\n', ' ');

                                // Draw the translated text on top of the image
                                using (var graphics = Graphics.FromImage(captureBackground.Image))
                                {
                                    Rectangle adjustedRect = new Rectangle(x + old_x, y + old_Y, old_width, old_height);
                                    float maxFontSize = 72;
                                    while (maxFontSize > 6)
                                    {
                                        using (var font = new Font("Arial", maxFontSize))
                                        {
                                            var calc = graphics.MeasureString(translatedText, font, adjustedRect.Width, StringFormat.GenericDefault);
                                            //Debug.WriteLine(calc.Height + "  " + adjustedRect.Height + "  " + maxFontSize);
                                            if (calc.Height <= adjustedRect.Height)
                                            {
                                                break;
                                            }
                                        }
                                        maxFontSize -= 1f;
                                    }
                                    Font scaledFont = new Font("Arial", maxFontSize);

                                    graphics.FillRectangle(Brushes.White, adjustedRect);
                                    graphics.DrawString(translatedText, scaledFont, Brushes.Black, adjustedRect);
                                    var calc2 = graphics.MeasureString(translatedText, scaledFont, adjustedRect.Size, StringFormat.GenericDefault);
                                    graphics.DrawRectangle(Pens.Red, adjustedRect.X, adjustedRect.Y, (int)calc2.Width, (int)calc2.Height);
                                }
                            }
                        } while (iter.Next(PageIteratorLevel.TextLine));
                        // Display the modified image on the screen
                        captureBackground.Refresh();
                    }
                }
            }
        }

        private string TranslateText(string textToTranslate, string fromLang, string toLang)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "dotCapture.appsettings.json";

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            using (StreamReader reader = new StreamReader(stream))
            {
                string config = reader.ReadToEnd();
                var configuration = JsonConvert.DeserializeObject<Dictionary<string, string>>(config);
                string key = configuration["TRANSLATOR_API_KEY"];
                string location = configuration["TRANSLATOR_API_LOCATION"];
                string endpoint = "https://api.cognitive.microsofttranslator.com";
                string route = $"/translate?api-version=3.0&from={fromLang}&to={toLang}";
                object[] body = new object[] { new { Text = textToTranslate } };
                var requestBody = JsonConvert.SerializeObject(body);

                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", key);
                    client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Region", location);

                    using (var request = new HttpRequestMessage())
                    {
                        request.Method = HttpMethod.Post;
                        request.RequestUri = new Uri(endpoint + route);
                        request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
                        HttpResponseMessage response = client.SendAsync(request).Result;

                        string result = response.Content.ReadAsStringAsync().Result;
                        dynamic jsonObj = JsonConvert.DeserializeObject(result);
                        string translatedText = jsonObj[0].translations[0].text;
                        //Debug.WriteLine("\n\n" + translatedText);
                        return translatedText;
                    }
                }
            }
        }

        private void saveButton_Click(object sender, EventArgs e)
        {
            // Create a SaveFileDialog object
            SaveFileDialog saveFileDialog = new SaveFileDialog();

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
                    int x = Math.Min(selectionBoxStartPoint.X, selectionBoxEndPoint.X);
                    int y = Math.Min(selectionBoxStartPoint.Y, selectionBoxEndPoint.Y);

                    // Calculate the width and height of the rectangle
                    int width = Math.Abs(selectionBoxStartPoint.X - selectionBoxEndPoint.X);
                    int height = Math.Abs(selectionBoxStartPoint.Y - selectionBoxEndPoint.Y);
                    Rectangle cropRect = new Rectangle(x, y, width, height);
                    // Save the image
                    Bitmap bmpimage = ((Bitmap)captureBackground.Image).Clone(cropRect, captureBackground.Image.PixelFormat);
                    bmpimage.Save(fileName, imageFormat);
                    ExitScreenCapture();
                }
            }
        }
        private void OnExit(object sender, EventArgs e)
        {
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
            SolidBrush brush = new SolidBrush(Color.FromArgb(128, Color.Black)); // Set the alpha component to 128 (50% transparent)

            // Get the dimensions of the screen
            int screenWidth = Screen.PrimaryScreen.Bounds.Width;
            int screenHeight = Screen.PrimaryScreen.Bounds.Height;

            // Fill the entire screen with the semi-transparent brush

            // Check if the starting and ending points have been set
            if (selectionBoxStartPoint != Point.Empty && selectionBoxEndPoint != Point.Empty)
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

                // Create a brush object that will be used to fill the rectangle with a color
                Pen rectanglePen = new Pen(Color.White);
                rectanglePen.Width = 2;

                // Draw the rectangle on the picture
                e.Graphics.DrawRectangle(rectanglePen, new Rectangle(topLeftPoint.X, topLeftPoint.Y, width, height));
                e.Graphics.ExcludeClip(new Rectangle(topLeftPoint.X - 2, topLeftPoint.Y - 2, width + 4, height + 4));
            }
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
            captureBackground.Image = CaptureScreen();
            this.Activate();
            this.Opacity = 1;
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

                this.Opacity = 0;
            }
        }
    }
}
