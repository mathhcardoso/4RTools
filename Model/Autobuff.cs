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
        private static int RELEASE_KEY_MAX_TRIES = 15;
        private static int ACT_MAX_TRIES = 15;

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

        private Tuple<int, int> MoveMouse(int x, int y)
        {
            int screenWidth = Interop.InternalGetSystemMetrics(0);
            int screenHeight = Interop.InternalGetSystemMetrics(1);

            int newX = (int)Math.Round(x * 65536.0 / screenWidth);
            int newY = (int)Math.Round(y * 65536.0 / screenHeight);

            Interop.mouse_event(Constants.KEYEVENTF_EXTENDEDKEY | Constants.MOUSEEVENTF_ABSOLUTE, newX, newY, 0, 0);
            return new Tuple<int, int>(newX, newY);
        }

        private void ClickMouse(int x, int y)
        {
            (int newX, int newY) = MoveMouse(x, y);
            Thread.Sleep(100);
            Interop.mouse_event(Constants.MOUSEEVENTF_LEFTDOWN, newX, newY, 0, 0);
            Thread.Sleep(1);
            Interop.mouse_event(Constants.MOUSEEVENTF_LEFTUP, newX, newY, 0, 0);
        }

        private void CentralizeMouse()
        {
            Rectangle r;
            Interop.GetWindowRect(Interop.GetForegroundWindow(), out r);
            double x = (double)(r.Right / 2) + 2;
            double y = (double)(r.Bottom / 2) + 12;
            MoveMouse((int)x, (int)y);
        }

        private void PressKey(string key)
        {
            Interop.PostMessage(ClientSingleton.GetClient().process.MainWindowHandle, Constants.WM_KEYDOWN_MSG_ID, (Keys)Enum.Parse(typeof(Keys), key), 0);
        }

        private void ReleaseKey(string key)
        {
            Interop.PostMessage(ClientSingleton.GetClient().process.MainWindowHandle, Constants.WM_KEYUP_MSG_ID, (Keys)Enum.Parse(typeof(Keys), key), 0);

            Interop.keybd_event((byte)(Keys)Enum.Parse(typeof(Keys), key), 0, Constants.KEYEVENTF_KEYUP, 0);
        }

        private void PressKeyWithCtrlKey(byte ctlrKey, string key)
        {
            // Hold Left Alt
            Interop.keybd_event(ctlrKey, Constants.KEYEVENTF_EXTENDEDKEY, 0, 0);
            Thread.Sleep(1);

            PressKey(key);
            Thread.Sleep(200);

            // Release Left Alt
            Interop.keybd_event(ctlrKey, 0, Constants.KEYEVENTF_KEYUP, 0);
            Thread.Sleep(100);
        }

        private void Relog()
        {
            int optionPosDiff = 155;
            // Open options
            PressKey("Escape");

            Point cursorPos = System.Windows.Forms.Cursor.Position;
            int toX = cursorPos.X;
            int toY = cursorPos.Y + optionPosDiff;
            Thread.Sleep(8000);
            
            // Click on "Select Character" option
            ClickMouse(toX, toY);

            Thread.Sleep(2000);

            // Select character
            PressKey("Enter");
            Thread.Sleep(2000);

            // Move mouse to original position
            ClickMouse(toX, toY - optionPosDiff);
        }

        private void CreateDirectory(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        private void DeleteOldFiles(string path)
        {
            string[] files = Directory.GetFiles(path);
            foreach (string file in files)
            {
                FileInfo fi = new FileInfo(file);
                if (fi.CreationTime < DateTime.Now.AddDays(-1))
                {
                    File.Delete(file);
                } else
                {
                    break;
                }
            }
        }

        private void StoreItem()
        {
            if ((Key)Enum.Parse(typeof(Key), ProfileSingleton.GetCurrent().UserPreferences.storageTextKey) != Key.None)
            {
                // Hold Left Alt
                Interop.keybd_event(Constants.VK_MENU, Constants.KEYEVENTF_EXTENDEDKEY, 0, 0);
                Thread.Sleep(1);

                // Open Storage
                PressKey(ProfileSingleton.GetCurrent().UserPreferences.storageTextKey);

                // Open Inventory
                PressKey("E");
                Thread.Sleep(200);

                CentralizeMouse();

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

        private void Act(AHK ahk, string action)
        {
            int tries = 0;
            while ((action == "Start" && !ahk.isActive) || (action == "Stop" && ahk.isActive))
            {
                if (tries == ACT_MAX_TRIES)
                {
                    Console.WriteLine(action + " AHK failed");
                    break;
                }
                Console.WriteLine(action + " AHK - " + ++tries);
                if (action == "Start") ahk.Start(); else ahk.Stop();
                Thread.Sleep(1);
            }
        }

        private void Act(Macro macroSwitch, string action)
        {
            int tries = 0;
            while ((action == "Start" && !macroSwitch.isActive) || (action == "Stop" && macroSwitch.isActive))
            {
                if (tries == ACT_MAX_TRIES)
                {
                    Console.WriteLine(action + " MacroSwitch failed");
                    break;
                }

                Console.WriteLine(action + " MacroSwitch - " + ++tries);
                if (action == "Start") macroSwitch.Start(); else macroSwitch.Stop();
                Thread.Sleep(1);
            }
        }

        private void ReleaseMacroSwitchKeys()
        {
            HashSet<Key> keys = ProfileSingleton.GetCurrent().MacroSwitch.macroEntriesKeys;
            foreach ( var key in keys )
            {
                if (key == Key.None) continue;

                int tries = 0;
                string keyStr = key.ToString();
                while (Keyboard.IsKeyDown(key))
                {
                    Console.WriteLine(keyStr + ": " + Keyboard.IsKeyDown(key));
                    if (tries == RELEASE_KEY_MAX_TRIES)
                    {
                        Console.WriteLine("Release " + keyStr + " failed");
                        break;
                    }
                    Console.WriteLine("Release " + keyStr + " - " + ++tries);
                    ReleaseKey(keyStr);
                }
            }
        }

        private void TurnOnAlootid()
        {
            if ((Key)Enum.Parse(typeof(Key), ProfileSingleton.GetCurrent().UserPreferences.alootidTextKey) != Key.None)
            {
                PressKeyWithCtrlKey(Constants.VK_MENU, ProfileSingleton.GetCurrent().UserPreferences.alootidTextKey);
            }
        }

        private void HandleAntiBot()
        {
            CentralizeMouse();

            string dateNow = DateTime.Now.ToString("yyyy-MMMM-ddTHH-mm-ss");
            string today = DateTime.Now.ToString("yyyy-MMMM-dd");
            string imagesDir = "images";
            string imagePath = imagesDir + @"\4RTools_AntiBotCode_" + dateNow + ".jpg";
            string logsDir = "logs";
            string logFilePath = logsDir + @"\4RTools_Logs_" + today + ".txt";

            Act(ProfileSingleton.GetCurrent().AHK, "Stop");
            Act(ProfileSingleton.GetCurrent().MacroSwitch, "Stop");
            ReleaseMacroSwitchKeys();

            // To release mouse button
            Point cursorPos = System.Windows.Forms.Cursor.Position;
            ClickMouse(cursorPos.X, cursorPos.Y);

            CreateDirectory(imagesDir);
            CreateDirectory(logsDir);
            DeleteOldFiles(imagesDir);
            DeleteOldFiles(logsDir);

            // Create/Open Log file
            FileStream fs = new FileStream(logFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            StreamReader sr = new StreamReader(fs);
            string oldContent = sr.ReadToEnd();
            fs.Seek(0, SeekOrigin.Begin);

            // Delete possible wrong typed numbers
            for (int i = 0; i < 10; i++)
            {
                PressKey("Delete");
                PressKey("Back");
            }

            Thread.Sleep(2000);

            // Scrool down antibot box
            for (int i = 0; i < 10; i++)
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

            // Answer is correct, but the game doesnt leave the player
            if (plainText.Contains("Arquimag"))
            {
                Relog();
            }
            else if (plainText.Contains("Anti-B"))
            {
                AnswerAntiBot(justNumbers);
            }
            else // Answer is wrong
            {
                if (ProfileSingleton.GetCurrent().UserPreferences.passwordText != "")
                {
                    foreach (char c in ProfileSingleton.GetCurrent().UserPreferences.passwordText)
                    {
                        string key = c.ToString().ToUpper();
                        if (char.IsDigit(c))
                        {
                            PressKey("D" + key);
                        }
                        else if (char.IsUpper(c))
                        {
                            PressKeyWithCtrlKey(Constants.VK_SHIFT, key);
                        }
                        else if (char.IsSymbol(c))
                        {
                            // TODO Botei * fixo
                            PressKeyWithCtrlKey(Constants.VK_SHIFT, "D8");
                        }
                        else
                        {
                            PressKey(key);
                        }
                        Thread.Sleep(5);
                    }

                    Thread.Sleep(10000);
                    foreach (int t in new int[] { 3000, 1000, 1000 })
                    {
                        PressKey("Enter");
                        Thread.Sleep(t);
                    }
                }
            }

            Act(ProfileSingleton.GetCurrent().MacroSwitch, "Start");
            Act(ProfileSingleton.GetCurrent().AHK, "Start");
            ReleaseMacroSwitchKeys();
            TurnOnAlootid();
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
                    if (status == EffectStatusIDs.ENDURE && ProfileSingleton.GetCurrent().UserPreferences.enabledAntibot)
                    {
                        foundAntiBot = true;
                        HandleAntiBot();
                        foundAntiBot = false;
                    }

                    // Is 50% Overweight
                    if (status == EffectStatusIDs.OVERWEIGHT_50 && !foundAntiBot && ProfileSingleton.GetCurrent().UserPreferences.enabledAutoStorage)
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
