using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

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

            // Register the mouse event handlers
            pictureBox.MouseDown += new MouseEventHandler(pictureBox_MouseDown);
            pictureBox.MouseMove += new MouseEventHandler(pictureBox_MouseMove);

            pictureBox.Paint += new PaintEventHandler(Form_Paint);
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
        private PictureBox pictureBox;

        // Variables to store the starting and ending points of the rectangle
        private Point selectionBoxStartPoint;
        private Point selectionBoxEndPoint;
        private bool selectionEnded = false;
        // MouseDown event handler
        private void pictureBox_MouseDown(object sender, MouseEventArgs e)
        {
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
    }
}
