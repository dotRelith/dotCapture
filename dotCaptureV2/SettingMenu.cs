using dotCaptureV2.Properties;
using dotCaptureV2.Resources;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Resources;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing.Imaging;
using System.Diagnostics;

namespace dotCaptureV2
{
    public partial class SettingMenu : Form
    {
        private Color backgroundColor, textColor, hoverColor;
        private EventHandler settingChanged;
        private ResourceManager resourceManager;

        private Image ResizeImage(string path, int width, int height)
        {
            using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(path);
            Image image = Image.FromStream(stream);
            image = new Bitmap(image, new Size(width, height));
            return image;
        }

        public SettingMenu()
        {        
            resourceManager = localization.ResourceManager;
            InitializeComponent();
            //Setup combobox
            var supportedLanguages = SupportedLanguagesDictionary.dictionary.Keys.ToList();
            interfaceLanguageComboBox.Items.AddRange(supportedLanguages.ToArray());
            interfaceLanguageComboBox.SelectedIndex = supportedLanguages.IndexOf(Settings.Default.InterfaceLanguage);
            //Setup localization texts
            interfaceLanguageLabel.Text = resourceManager.GetString("settings.language.label");
            backgroundColorLabel.Text = resourceManager.GetString("settings.backgroundColor.label");
            textColorLabel.Text = resourceManager.GetString("settings.textColor.label");
            hoverColorLabel.Text = resourceManager.GetString("settings.hoverColor.label");
            applyButton.Text = resourceManager.GetString("settings.apply");
            cancelButton.Text = resourceManager.GetString("settings.cancel");
            //Setup picture boxes
            backgroundColorPictureBox.BackColor = backgroundColor;
            textColorPictureBox.BackColor = textColor;
            hoverColorPictureBox.BackColor = hoverColor;
            //Setup color buttons
            backgroundColorButton.Click += (s, e) => { ChangeColor(ref backgroundColor, backgroundColorPictureBox); };
            textColorButton.Click += (s, e) => { ChangeColor(ref textColor, textColorPictureBox); };
            hoverColorButton.Click += (s, e) => { ChangeColor(ref hoverColor, hoverColorPictureBox); };
            //Setup dummy button
            dummyButton.Text = resourceManager.GetString("settings.dummy");
            dummyButton.Image = ResizeImage("dotCaptureV2.icons.dummy.png", 24,24);
            //Subscribe event
            cancelButton.Click += (s, e) => { this.Hide(); };
            applyButton.Click += (s, e) => { this.ApplyValues(); this.Hide(); };
            settingChanged += UpdateDummyColors;
        }

        private void ApplyValues()
        {
            Settings.Default.InterfaceLanguage = interfaceLanguageComboBox.SelectedItem.ToString();
            Settings.Default.BackgroundColor = backgroundColor;
            Settings.Default.TextColor = textColor;
            Settings.Default.ButtonHoverColor = hoverColor;
            Settings.Default.Save();
        }

        private void SettingsMenu_Shown(object sender, EventArgs e){
            backgroundColor = Settings.Default.BackgroundColor;
            textColor = Settings.Default.TextColor;
            hoverColor = Settings.Default.ButtonHoverColor;
            backgroundColorPictureBox.BackColor = backgroundColor;
            textColorPictureBox.BackColor = textColor;
            hoverColorPictureBox.BackColor = hoverColor;
            settingChanged.Invoke(this, EventArgs.Empty);
        }

        private void ChangeColor(ref Color colorToChange, PictureBox buttonColor)
        {
            // Show the color dialog box. If the user clicks OK, change the color of the button.
            ColorDialog colorDialog = new ColorDialog();
            colorDialog.Color = colorToChange;
            colorDialog.FullOpen = true;
            if (colorDialog.ShowDialog() == DialogResult.OK)
            {
                colorToChange = colorDialog.Color;
                buttonColor.BackColor = colorToChange;
                settingChanged.Invoke(this, EventArgs.Empty);
            }
        }

        private void UpdateDummyColors(object sender, EventArgs e)
        {
            // Load the original image into a Bitmap object
            Bitmap originalImage = (Bitmap)ResizeImage("dotCaptureV2.icons.dummy.png", 24, 24);

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
                        image.SetPixel(x, y, Color.FromArgb(pixelColor.A,textColor));
                    }
                    else
                    {
                        image.SetPixel(x, y, Color.Transparent);
                    }
                }
            }
            // Set the dummyButton's Image property to the new image
            dummyButton.Image = image;

            dummyButton.BackColor = backgroundColor;
            dummyButton.ForeColor = textColor;
            dummyButton.FlatAppearance.MouseOverBackColor = hoverColor;
        }

    }
}
