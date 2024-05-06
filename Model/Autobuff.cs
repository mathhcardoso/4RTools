using System;
using System.Threading;
using System.Windows.Input;
using System.Windows.Forms;
using System.Collections.Generic;
using Newtonsoft.Json;
using _4RTools.Utils;
using System.Drawing;
using Patagames.Ocr.Enums;
using Patagames.Ocr;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Data;

namespace _4RTools.Model
{

    public class AutoBuff : Action
    {
        public static string ACTION_NAME_AUTOBUFF = "Autobuff";

        private _4RThread thread;
        public int delay { get; set; } = 1;
        public Dictionary<EffectStatusIDs, Key> buffMapping = new Dictionary<EffectStatusIDs, Key>();

        public void Start()
        {
            Stop();
            Client roClient = ClientSingleton.GetClient();
            if (roClient != null)
            {
                this.thread = AutoBuffThread(roClient);
                _4RThread.Start(this.thread);
            }
        }

        private Bitmap GrayScale(Bitmap Bmp)
        {
            int rgb;
            int threshold = 180;
            Color c;

            for (int y = 0; y < Bmp.Height; y++)
                for (int x = 0; x < Bmp.Width; x++)
                {
                    c = Bmp.GetPixel(x, y);
                    rgb = (int)((c.R + c.G + c.B) / 3) > threshold ? 255 : 0;
                    Bmp.SetPixel(x, y, Color.FromArgb(rgb, rgb, rgb));
                }
            return Bmp;
        }

        private Tuple<int, int> Move_mouse(int to_x, int to_y)
        {
            int screenWidth = Interop.InternalGetSystemMetrics(0);
            int screenHeight = Interop.InternalGetSystemMetrics(1);

            int mic_x = (int)Math.Round(to_x * 65536.0 / screenWidth);
            int mic_y = (int)Math.Round(to_y * 65536.0 / screenHeight);

            Interop.mouse_event(Constants.KEYEVENTF_EXTENDEDKEY | Constants.MOUSEEVENTF_ABSOLUTE, mic_x, mic_y, 0, 0);
            return new Tuple<int, int>(mic_x, mic_y);
        }

        private void Click_mouse(int x, int y)
        {
            (int new_x, int new_y) = Move_mouse(x, y);
            Thread.Sleep(100);
            Interop.mouse_event(Constants.MOUSEEVENTF_LEFTDOWN, new_x, new_y, 0, 0);
            Thread.Sleep(1);
            Interop.mouse_event(Constants.MOUSEEVENTF_LEFTUP, new_x, new_y, 0, 0);
        }

        private void PressKey(string key)
        {
            Interop.PostMessage(ClientSingleton.GetClient().process.MainWindowHandle, Constants.WM_KEYDOWN_MSG_ID, (Keys)Enum.Parse(typeof(Keys), key), 0);
        }

        private void ReleaseKey(string key)
        {
            Interop.PostMessage(ClientSingleton.GetClient().process.MainWindowHandle, Constants.WM_KEYUP_MSG_ID, (Keys)Enum.Parse(typeof(Keys), key), 0);
        }

        private void UseAltShortCut(string key)
        {
            // Hold Left Alt
            Interop.keybd_event(Constants.VK_MENU, Constants.KEYEVENTF_EXTENDEDKEY, 0, 0);
            Thread.Sleep(1);

            PressKey(key);
            Thread.Sleep(200);

            // Release Left Alt
            Interop.keybd_event(Constants.VK_MENU, 0, Constants.KEYEVENTF_KEYUP, 0);
            Thread.Sleep(100);
        }

        private void Relog()
        {
            int optionPosDiff = 155;
            // Open options
            PressKey("Escape");

            Point cursorPos = System.Windows.Forms.Cursor.Position;
            int to_x = cursorPos.X;
            int to_y = cursorPos.Y + optionPosDiff;
            Thread.Sleep(8000);
            
            // Click on "Select Character" option
            Click_mouse(to_x, to_y);

            Thread.Sleep(2000);

            // Select character
            PressKey("Enter");
            Thread.Sleep(2000);

            // Move mouse to original position
            Click_mouse(to_x, to_y - optionPosDiff);
        }

        private void CreateDirectory(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            DeleteFiles(path);
        }

        private void DeleteFiles(string path)
        {
            string[] files = Directory.GetFiles(path);
            foreach (string file in files)
            {
                FileInfo fi = new FileInfo(file);
                if (fi.LastAccessTime < DateTime.Now.AddDays(-2))
                    fi.Delete();
            }
        }

        private void StoreItem()
        {
            // Hold Left Alt
            Interop.keybd_event(Constants.VK_MENU, Constants.KEYEVENTF_EXTENDEDKEY, 0, 0);
            Thread.Sleep(1);

            // Open Storage
            PressKey("D5");

            // Open Inventory
            PressKey("E");
            Thread.Sleep(200);

            // Right Click
            for (int i = 0; i < 10; i++)
            {
                Interop.PostMessage(ClientSingleton.GetClient().process.MainWindowHandle, Constants.WM_RBUTTONDOWN, 0, 0);
                Thread.Sleep(10);
                Interop.PostMessage(ClientSingleton.GetClient().process.MainWindowHandle, Constants.WM_RBUTTONUP, 0, 0);
                Thread.Sleep(10);
            }

            // Close Inventory
            PressKey("E");
            Thread.Sleep(100);

            // Release Left Alt
            Interop.keybd_event(Constants.VK_MENU, 0, Constants.KEYEVENTF_KEYUP, 0);
            Thread.Sleep(100);
        }

        private void TakeScreenShot(string imagePath)
        {
            // Take a ScreenShot
            Bitmap bitmap = new Bitmap(200, 200);
            Graphics graphics = Graphics.FromImage(bitmap as Image);
            IntPtr dc = graphics.GetHdc();
            bool success = Interop.PrintWindow(ClientSingleton.GetClient().process.MainWindowHandle, dc, 0);
            graphics.ReleaseHdc(dc);

            // Resize image and transform to grayScale and save
            Size size = new Size(499, 499);
            Image image = GrayScale(new Bitmap(bitmap, size));
            image.Save(imagePath, ImageFormat.Jpeg);
        }

        private string ExtractTextFromImage(string imagePath)
        {
            var api = OcrApi.Create();
            api.Init(Languages.English);
            string plainText = api.GetTextFromImage(imagePath);
            return plainText;
        }

        private void AnswerAntiBot(string code)
        {
            // Type requested numbers
            foreach (char number in code)
            {
                PressKey("D" + number.ToString());
                Thread.Sleep(100);
            }

            Thread.Sleep(150);
            PressKey("Enter");
            Thread.Sleep(150);
            PressKey("Enter");
            Thread.Sleep(150);
        }

        private void HandleAntiBot()
        {
            string dateNow = DateTime.Now.ToString("yyyy-MMMM-ddTHH-mm-ss");
            string today = DateTime.Now.ToString("yyyy-MMMM-dd");
            string imagesDir = "images";
            string imagePath = imagesDir + @"\4RTools_AntiBotCode_" + dateNow + ".jpg";
            string logsDir = "logs";
            string logFilePath = logsDir + @"\4RTools_Logs_" + today + ".txt";

            if (ProfileSingleton.GetCurrent().AHK.isActive)
            {
                ProfileSingleton.GetCurrent().AHK.Stop();
                Thread.Sleep(100);
            }

            if (ProfileSingleton.GetCurrent().MacroSwitch.isActive)
            {
                ProfileSingleton.GetCurrent().MacroSwitch.Stop();
                Thread.Sleep(100);
            }

            // Release F3
            ReleaseKey("F3");
            Thread.Sleep(100);
            // Release D0
            ReleaseKey("D0");
            Thread.Sleep(100);

            // Click mouse para soltar da skill
            Point cursorPos = System.Windows.Forms.Cursor.Position;
            Click_mouse(cursorPos.X, cursorPos.Y);

            // Create images directory
            CreateDirectory(imagesDir);

            // Create logs directory
            CreateDirectory(logsDir);

            // Create/Open Log file
            FileStream fs = new FileStream(logFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            StreamReader sr = new StreamReader(fs);
            string oldContent = sr.ReadToEnd();
            fs.Seek(0, SeekOrigin.Begin);

            // Delete possible wrong typed numbers
            for (int j = 0; j < 10; j++)
            {
                PressKey("Delete");
                PressKey("Back");
            }

            Thread.Sleep(2000);

            // Scrool down antibot box
            for (int j = 0; j < 10; j++)
            {
                PressKey("Down");
            }

            Thread.Sleep(2000);

            // Antibot chat should be positioned on the Basic Info card
            TakeScreenShot(imagePath);

            // Extract text from saved image
            string plainText = ExtractTextFromImage(imagePath);
            string code = plainText.Split(':').Last().Trim();
            int lenCode = code.Length;
            string justNumbers = new String(code.Where(Char.IsDigit).ToArray());
            int lenJustNumbers = justNumbers.Length;

            // Write log
            using (StreamWriter sw = new StreamWriter(fs))
            {
                sw.WriteLine($"{dateNow}\n->{imagePath}\n->{plainText}\n->'{code}' ({lenCode})\n->'{justNumbers}' ({lenJustNumbers})\n\n");
                sw.Write(oldContent);
            }

            // Response is correct, but the game doesnt leave the player
            if (plainText.Contains("Arquimago"))
            {
                Relog();
            }
            else if (plainText.Contains("Anti-Bot"))
            {
                AnswerAntiBot(justNumbers);
            }
            else // Answer wrong code
            {
                PressKey("V".ToString().ToUpper());
                Thread.Sleep(1);
                PressKey("E".ToString().ToUpper());
                Thread.Sleep(1);
                PressKey("L".ToString().ToUpper());
                Thread.Sleep(1);
                PressKey("H".ToString().ToUpper());
                Thread.Sleep(1);
                PressKey("A".ToString().ToUpper());
                Thread.Sleep(1);
                PressKey("D2".ToString().ToUpper());
                Thread.Sleep(1);
                PressKey("D2".ToString().ToUpper());
                Thread.Sleep(1);

                // Press Enter twice
                Thread.Sleep(10000);
                PressKey("Enter");
                Thread.Sleep(3000);
                PressKey("Enter");
                Thread.Sleep(1000);
                PressKey("Enter");
                Thread.Sleep(1000);
            }
            
            if (!ProfileSingleton.GetCurrent().AHK.isActive)
            {
                ProfileSingleton.GetCurrent().AHK.Start();
            }

            if (!ProfileSingleton.GetCurrent().MacroSwitch.isActive)
            {
                ProfileSingleton.GetCurrent().MacroSwitch.Start();
            }

            // Turn on auto loot
            UseAltShortCut("D8");
        }

        public _4RThread AutoBuffThread(Client c)
        {
            _4RThread autobuffItemThread = new _4RThread(_ =>
            {

                bool foundQuag = false;
                bool foundAntiBot = false;
                Dictionary<EffectStatusIDs, Key> bmClone = new Dictionary<EffectStatusIDs, Key>(this.buffMapping);
                for (int i = 0; i < Constants.MAX_BUFF_LIST_INDEX_SIZE; i++)
                {
                    uint currentStatus = c.CurrentBuffStatusCode(i);
                    EffectStatusIDs status = (EffectStatusIDs)currentStatus;

                    // Anti bot
                    if (status == EffectStatusIDs.ENDURE)
                    {
                        foundAntiBot = true;
                        HandleAntiBot();
                        foundAntiBot = false;
                    }

                    // Is 90% Overweight
                    if (status == EffectStatusIDs.OVERWEIGHT_90 && !foundAntiBot)
                        StoreItem();

                    if (status == EffectStatusIDs.OVERTHRUSTMAX)
                    {
                        if (buffMapping.ContainsKey(EffectStatusIDs.OVERTHRUST))
                        {
                            bmClone.Remove(EffectStatusIDs.OVERTHRUST);
                        }
                    }

                    if (buffMapping.ContainsKey(status)) //CHECK IF STATUS EXISTS IN STATUS LIST AND DO ACTION
                    {
                        bmClone.Remove(status);
                    }

                    if (status == EffectStatusIDs.QUAGMIRE) foundQuag = true;
                }

                foreach (var item in bmClone)
                {
                    if (foundQuag && (item.Key == EffectStatusIDs.CONCENTRATION || item.Key == EffectStatusIDs.INC_AGI || item.Key == EffectStatusIDs.TRUESIGHT || item.Key == EffectStatusIDs.ADRENALINE))
                    {
                        break;
                    }
                    else if (c.ReadCurrentHp() >= Constants.MINIMUM_HP_TO_RECOVER)
                    {
                        this.useAutobuff(item.Value);
                        Thread.Sleep(10);
                    }
                }

                Thread.Sleep(300);
                return 0;

            });

            return autobuffItemThread;
        }

        public void AddKeyToBuff(EffectStatusIDs status, Key key)
        {
            if (buffMapping.ContainsKey(status))
            {
                buffMapping.Remove(status);
            }

            if (FormUtils.IsValidKey(key))
            {
                buffMapping.Add(status, key);
            }
        }
        public void ClearKeyMapping()
        {
            buffMapping.Clear();
        }

        public void Stop()
        {
            _4RThread.Stop(this.thread);
        }

        public string GetConfiguration()
        {
            return JsonConvert.SerializeObject(this);
        }

        public string GetActionName()
        {
            return ACTION_NAME_AUTOBUFF;
        }

        private void useAutobuff(Key key)
        {
            if((key != Key.None) && !Keyboard.IsKeyDown(Key.LeftAlt) && !Keyboard.IsKeyDown(Key.RightAlt))
                Interop.PostMessage(ClientSingleton.GetClient().process.MainWindowHandle, Constants.WM_KEYDOWN_MSG_ID, (Keys)Enum.Parse(typeof(Keys), key.ToString()), 0);
        }
    }
}
