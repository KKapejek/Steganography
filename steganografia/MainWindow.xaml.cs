using System;
using System.Text;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using System.IO;
using System.Collections;
using System.Drawing;

namespace steganografia
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void _loadImage(object sender, RoutedEventArgs e)
        {
            OpenFileDialog op = new OpenFileDialog();
            op.Title = "Select an image";
            op.Filter = "All supported graphics|*.jpg;*.jpeg;*.png|" +
              "Portable Network Graphic (*.png)|*.png";

            if (op.ShowDialog() == true)
            {
                imgInput.Source = new BitmapImage(new Uri(op.FileName));
            }
        }

        private void _loadFile(object sender, RoutedEventArgs e)
        {
            OpenFileDialog op = new OpenFileDialog();
            op.Filter = "Text|*.txt|All|*.*";

            if (op.ShowDialog().GetValueOrDefault())
            {
                if (System.IO.Path.GetExtension(op.FileName) == ".txt")
                {
                    string textFromFile = File.ReadAllText(op.FileName);
                    textBlock.Text = textFromFile;
                } else
                {
                    textBlock.Text = "File must be .txt";
                }
            }
        }

        //byte[] imgInBytes = imgToByteArray(imgInput.Source as BitmapImage);
        public static byte[] imgToByteArray(BitmapImage Image)
        {
            int stride = (int)Image.PixelWidth * (Image.Format.BitsPerPixel / 8);
            byte[] pixels = new byte[Image.PixelHeight * stride];
            Image.CopyPixels(pixels, stride, 0);
            return pixels;
        }

        //imgToSave.Source = byteArrayToImg(imgToEncode, imgInBytes);
        public static BitmapSource byteArrayToImg(BitmapImage imgOriginal, byte[] pixels)
        {
            int stride = (int)imgOriginal.PixelWidth * (imgOriginal.Format.BitsPerPixel / 8);
            BitmapSource bitmap = BitmapSource.Create(imgOriginal.PixelWidth, imgOriginal.PixelHeight, imgOriginal.DpiX,
                                             imgOriginal.DpiY, imgOriginal.Format, null, pixels, stride);
            return bitmap;
        }

        private void _encodeText(object sender, RoutedEventArgs e)
        {
            if(imgInput.Source != null && textBlock.Text != null)
            {
                BitmapImage imgToEncode = imgInput.Source as BitmapImage;
                byte[] imgInBytes = imgToByteArray(imgToEncode);

                //Password = "Encoded" == 01000101 01101110 01100011 01101111 01100100 01100101 01100100
                string password = "Encoded";
                string textToEncode = password + textBlock.Text.ToString();
                byte[] textInBytes = Encoding.UTF8.GetBytes(textToEncode);

                int offset = 0;
                int codingNumber = (int)decodeBitsSlider.Value;
                int codingModulator = 0;
                int lengthMultiplier = 0;

                switch (codingNumber)
                {
                    case 1:
                        codingModulator = 2;
                        lengthMultiplier = 32;
                        break;

                    case 2:
                        codingModulator = 4;
                        lengthMultiplier = 16;
                        break;

                    case 4:
                        codingModulator = 16;
                        lengthMultiplier = 4;
                        break;

                    case 8:
                        codingModulator = 256;
                        lengthMultiplier = 2;
                        break;
                }

                if(imgInBytes.Length > textInBytes.Length * lengthMultiplier )
                {
                    for(int i = 0; i < textInBytes.Length; i++)
                    {
                        //Skip 4th byte to skip Alpha channel
                        if (offset % 4 == 3) offset++;

                        int holdTextByteAsInt = textInBytes[i];
                        if (holdTextByteAsInt != 0)
                        {
                            for (int j = 0; j < 8; j += codingNumber)
                            {
                                int holdImgByteAsInt = imgInBytes[offset];

                                //0 last N bits
                                holdImgByteAsInt -= holdImgByteAsInt % codingModulator;

                                //Add N text bits
                                holdImgByteAsInt += holdTextByteAsInt % codingModulator;
                                holdTextByteAsInt /= codingModulator;

                                //Replace until 0
                                imgInBytes[offset] = (byte)holdImgByteAsInt;
                                textInBytes[i] = (byte)holdTextByteAsInt;

                                offset++;
                                //Skip 4th byte to skip Alpha channel
                                if (offset % 4 == 3) offset++;
                            }
                        }
                    }
                    //Set stop byte
                    imgInBytes[offset] = 30;

                    BitmapSource imgToSave = byteArrayToImg(imgToEncode, imgInBytes);
                    imgOutput.Source = imgToSave;

                    //Save image to path of .exe as output.png
                    //C:\Users\Krzysiek\source\repos\steganografia\steganografia\bin\Debug
                    using (var fileStream = new FileStream("output.jpg", FileMode.Create))
                    {
                        BitmapEncoder encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(imgToSave));
                        encoder.Save(fileStream);
                    }
                }
                else
                {
                    MessageBox.Show("The text file is too big for this image. Try a bigger encoding number or another image!", "Error!");
                }
            }
            else
            {
                MessageBox.Show("There is no image or text file loaded. Did you forget about those?", "Error!");
            }
        }

        int intPow(int x, int pow)
        {
            int ret = 1;
            while (pow != 0)
            {
                if ((pow & 1) == 1)
                    ret *= x;
                x *= x;
                pow >>= 1;
            }
            return ret;
        }

        private void _decodeText(object sender, RoutedEventArgs e)
        {
            if(imgInput.Source != null)
            {
                BitmapImage imgToDecode = imgInput.Source as BitmapImage;
                byte[] imgInBytes = imgToByteArray(imgToDecode);
                int codingNumber = (int)decodeBitsSlider.Value;

                string decodedText = "";
                int offset = 0;
                int codingModulator = 0;
                int lengthMultiplier = 0;
                bool isCoded = false;
                bool wasChecked = false;

                switch (codingNumber)
                {
                    case 1:
                        codingModulator = 2;
                        lengthMultiplier = 32;
                        break;

                    case 2:
                        codingModulator = 4;
                        lengthMultiplier = 16;
                        break;

                    case 4:
                        codingModulator = 16;
                        lengthMultiplier = 4;
                        break;

                    case 8:
                        codingModulator = 256;
                        lengthMultiplier = 2;
                        break;
                }

                for (int i = 0; i < imgInBytes.Length / lengthMultiplier; i++)
                {
                    //Skip 4th byte to skip Alpha channel
                    if (offset % 4 == 3) offset++;

                    //check for stop byte
                    if (imgInBytes[offset] != 30)
                    {
                        int decodedTextAsInt = 0;
                        for (int j = 0; j < 8; j += codingNumber)
                        {
                            int holdImgByteAsInt = imgInBytes[offset];

                            //Build byte
                            decodedTextAsInt += (holdImgByteAsInt % codingModulator) * intPow(2, j);

                            offset++;
                            //Skip 4th byte to skip Alpha channel
                            if (offset % 4 == 3) offset++;
                        }

                        decodedText += (char)decodedTextAsInt;

                        //Check if image is encoded
                        if (decodedText.Length == 7 && wasChecked == false)
                        {
                            if (decodedText == "Encoded")
                            {
                                isCoded = true;
                                wasChecked = true;
                                decodedText = "";
                            }
                            else
                            {
                                isCoded = false;
                                MessageBox.Show("This image isn't encoded, or you are using the wrong encoding number.", "Error!");
                                break;
                            }
                        }
                    }
                    else break;
                }
                if (isCoded == true) textBlock.Text = decodedText;
            }
            else
            {
                MessageBox.Show("There is no message to decode. This can be considered a good thing.", "Error!");
            }
        }
    }
}