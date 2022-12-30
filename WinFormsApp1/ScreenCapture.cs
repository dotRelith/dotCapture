using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Tesseract;

namespace WinFormsApp1
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

        private PictureBox pictureBox;
        private Panel panel;
        private Button copyButton, saveButton, translateButton, copyTextButton;
        public ScreenCapture()
        {
            instance = this;
            // Create a new form and register for the KeyUp event

            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.ShowInTaskbar = false;
            this.Opacity = 0;

            pictureBox = new PictureBox();
            pictureBox.Size = Screen.PrimaryScreen.Bounds.Size;
            pictureBox.Location = new Point(0, 0);
            this.Controls.Add(pictureBox);

            // Create a new panel and add it to the form
            panel = new Panel();
            panel.Size = new Size(100, 150);
            this.Controls.Add(panel);

            // Create the buttons and add them to the panel
            copyButton = new Button();
            copyButton.Text = "Copy";
            copyButton.Size = new Size(75, 23);
            copyButton.Location = new Point(12, 12);
            panel.Controls.Add(copyButton);

            saveButton = new Button();
            saveButton.Text = "Save";
            saveButton.Size = new Size(75, 23);
            saveButton.Location = new Point(12, 41);
            panel.Controls.Add(saveButton);

            translateButton = new Button();
            translateButton.Text = "Translate";
            translateButton.Size = new Size(75, 23);
            translateButton.Location = new Point(12, 70);
            panel.Controls.Add(translateButton);

            copyTextButton = new Button();
            copyTextButton.Text = "Copy Text";
            copyTextButton.Size = new Size(75, 23);
            copyTextButton.Location = new Point(12, 99);
            panel.Controls.Add(copyTextButton);

            // Register the button click event handlers
            copyButton.Click += new EventHandler(copyButton_Click);
            saveButton.Click += new EventHandler(saveButton_Click);
            translateButton.Click += new EventHandler(translateButton_Click);
            copyTextButton.Click += new EventHandler(copyTextButton_Click);

            // Register the mouse event handlers
            pictureBox.MouseDown += new MouseEventHandler(pictureBox_MouseDown);
            pictureBox.MouseMove += new MouseEventHandler(pictureBox_MouseMove);

            pictureBox.Paint += new PaintEventHandler(Form_Paint);
        }

        private void copyTextButton_Click(object sender, EventArgs e)
        {
            string tesseractDataDir = Path.Combine(Application.StartupPath, "tessdata");

            using (var engine = new TesseractEngine(tesseractDataDir, "osd", EngineMode.Default))
            {
                int x = Math.Min(selectionBoxStartPoint.X, selectionBoxEndPoint.X);
                int y = Math.Min(selectionBoxStartPoint.Y, selectionBoxEndPoint.Y);

                // Calculate the width and height of the rectangle
                int width = Math.Abs(selectionBoxStartPoint.X - selectionBoxEndPoint.X);
                int height = Math.Abs(selectionBoxStartPoint.Y - selectionBoxEndPoint.Y);
                Rectangle cropRect = new Rectangle(x, y, width, height);
                // Save the image
                Bitmap bmpimage = ((Bitmap)pictureBox.Image).Clone(cropRect, pictureBox.Image.PixelFormat);
                using (var image = bmpimage)
                {
                    using (var pix = PixConverter.ToPix(image))
                    {
                        using (var page = engine.Process(pix))
                        {
                            // Extract the text from the image
                            string text = page.GetText();

                            // Check if the text is not empty
                            if (!string.IsNullOrEmpty(text))
                            {
                                // Copy the text to the clipboard
                                Clipboard.SetText(text);
                                ExitScreenCapture();
                            }
                        }
                    }
                }
            }
        }

        private void translateButton_Click(object sender, EventArgs e)
        {
            throw new NotImplementedException();
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
                    Bitmap bmpimage = ((Bitmap)pictureBox.Image).Clone(cropRect, pictureBox.Image.PixelFormat);
                    bmpimage.Save(fileName, imageFormat);
                    ExitScreenCapture();
                }
            }
        }

        private void copyButton_Click(object sender, EventArgs e)
        {
            int x = Math.Min(selectionBoxStartPoint.X, selectionBoxEndPoint.X);
            int y = Math.Min(selectionBoxStartPoint.Y, selectionBoxEndPoint.Y);

            // Calculate the width and height of the rectangle
            int width = Math.Abs(selectionBoxStartPoint.X - selectionBoxEndPoint.X);
            int height = Math.Abs(selectionBoxStartPoint.Y - selectionBoxEndPoint.Y);
            Rectangle cropRect = new Rectangle(x, y, width, height);
            Bitmap bmpimage = ((Bitmap)pictureBox.Image).Clone(cropRect, pictureBox.Image.PixelFormat);
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
            e.Graphics.FillRectangle(brush, 0, 0, screenWidth, screenHeight);

            // Check if the starting and ending points have been set
            if (selectionBoxStartPoint != Point.Empty && selectionBoxEndPoint != Point.Empty)
            {
                // Calculate the top-left corner of the rectangle
                int x = Math.Min(selectionBoxStartPoint.X, selectionBoxEndPoint.X);
                int y = Math.Min(selectionBoxStartPoint.Y, selectionBoxEndPoint.Y);

                // Calculate the width and height of the rectangle
                int width = Math.Abs(selectionBoxStartPoint.X - selectionBoxEndPoint.X);
                int height = Math.Abs(selectionBoxStartPoint.Y - selectionBoxEndPoint.Y);

                // Create a brush object that will be used to fill the rectangle with a color
                Pen rectanglePen = new Pen(Color.White);

                // Draw the rectangle on the picture
                e.Graphics.DrawRectangle(rectanglePen, new Rectangle(x, y, width, height));
                e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(64, Color.White)), new Rectangle(x, y, width, height));
            }
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

                panel.Location = new Point(selectionBoxEndPoint.X + 5, selectionBoxEndPoint.Y);
                panel.BringToFront();
                panel.Visible = true;

                // Set the flag to indicate that the selection has ended
                selectionEnded = true;
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
            pictureBox.Invalidate();
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
            pictureBox.Image = CaptureScreen();
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
                pictureBox.Image = null;
                panel.Visible = false;
                this.Opacity = 0;
            }
        }
    }
}
