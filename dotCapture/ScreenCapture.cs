using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

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
                {"Japanese (Vertical)","jpn_vert" },
                {"Korean","kor" },
                {"Korean (Vertical)","kor_vert" },
                {"Chinese (Simplified)","chi_sim" },
                {"Chinese (Simplified) Vertical","chi_sim_vert" },
            };
        protected override CreateParams CreateParams{
            get{
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
        private ComboBox languageComboBox;
        private Button copyButton, saveButton, translateButton, copyTextButton;
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
            captureMenu.Size = new Size(75, 100);
            this.Controls.Add(captureMenu);

            // Create the buttons and add them to the panel
            copyButton = new Button();
            copyButton.Text = "Copy";
            copyButton.Size = new Size(75, 23);
            copyButton.Location = new Point(12, 12);
            captureMenu.Controls.Add(copyButton);

            saveButton = new Button();
            saveButton.Text = "Save";
            saveButton.Size = new Size(75, 23);
            saveButton.Location = new Point(12, 41);
            captureMenu.Controls.Add(saveButton);

            translateButton = new Button();
            translateButton.Text = "Translate";
            translateButton.Size = new Size(75, 23);
            translateButton.Location = new Point(12, 70);
            captureMenu.Controls.Add(translateButton);

            copyTextButton = new Button();
            copyTextButton.Text = "Copy Text";
            copyTextButton.Size = new Size(75, 23);
            copyTextButton.Location = new Point(12, 99);
            captureMenu.Controls.Add(copyTextButton);

            languageComboBox = new ComboBox();
            languageComboBox.Size = new Size(50, 23);
            languageComboBox.Location = new Point(90, 99);
            languageComboBox.Items.AddRange(supportedLanguages.Keys.ToArray());
            captureMenu.Controls.Add(languageComboBox);

            // Register the button click event handlers
            copyButton.Click += new EventHandler(copyButton_Click);
            saveButton.Click += new EventHandler(saveButton_Click);
            translateButton.Click += new EventHandler(translateButton_Click);
            copyTextButton.Click += new EventHandler(copyTextButton_Click);

            // Register the mouse event handlers
            captureBackground.MouseDown += new MouseEventHandler(pictureBox_MouseDown);
            captureBackground.MouseMove += new MouseEventHandler(pictureBox_MouseMove);

            captureBackground.Paint += new PaintEventHandler(Form_Paint);
        }

        private void copyTextButton_Click(object sender, EventArgs e)
        {
            throw new NotImplementedException();
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
            e.Graphics.FillRectangle(brush, 0, 0, screenWidth, screenHeight);

            // Check if the starting and ending points have been set
            if (selectionBoxStartPoint != Point.Empty && selectionBoxEndPoint != Point.Empty )
            {
                // Calculate the top-left corner of the rectangle
                Point topLeftPoint = new Point(Math.Min(selectionBoxStartPoint.X, selectionBoxEndPoint.X), Math.Min(selectionBoxStartPoint.Y, selectionBoxEndPoint.Y));
                Point bottomRightPoint = new Point(Math.Max(selectionBoxStartPoint.X, selectionBoxEndPoint.X), Math.Max(selectionBoxStartPoint.Y, selectionBoxEndPoint.Y));

                // Calculate the width and height of the rectangle
                int width = Math.Abs(selectionBoxStartPoint.X - selectionBoxEndPoint.X);
                int height = Math.Abs(selectionBoxStartPoint.Y - selectionBoxEndPoint.Y);

                if(selectionEnded == true)
                {

                    int captureMenu_Width = 128;
                    int captureMenu_Height = Math.Abs(topLeftPoint.Y - bottomRightPoint.Y);
                    captureMenu_Height = Math.Clamp(captureMenu_Height / 2, 150, 648);

                    int captureMenu_X = bottomRightPoint.X;
                    if (captureMenu_X + captureMenu_Width >= Screen.PrimaryScreen.Bounds.Width)
                        captureMenu_X = topLeftPoint.X - captureMenu_Width;

                    int captureMenu_Y = bottomRightPoint.Y - captureMenu_Height;
                    if (captureMenu_Y < 0)// Screen.PrimaryScreen.Bounds.Height)
                        captureMenu_Y = topLeftPoint.Y;

                    captureMenu.Location = new Point(captureMenu_X, captureMenu_Y);
                    captureMenu.Size = new Size(captureMenu_Width, captureMenu_Height);
                    captureMenu.BringToFront();
                    captureMenu.Visible = true;
                }

                // Create a brush object that will be used to fill the rectangle with a color
                Pen rectanglePen = new Pen(Color.White);

                // Draw the rectangle on the picture
                e.Graphics.DrawRectangle(rectanglePen, new Rectangle(topLeftPoint.X, topLeftPoint.Y, width, height));
                e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(64, Color.White)), new Rectangle(topLeftPoint.X, topLeftPoint.Y, width, height));
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
