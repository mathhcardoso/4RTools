using System;
using System.ComponentModel;
using _4RTools.Utils;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Input;
using Newtonsoft.Json;

namespace _4RTools.Model
{

    public class Autopot : Action
    {

        public static string ACTION_NAME_AUTOPOT = "Autopot";
        public static string ACTION_NAME_AUTOPOT_YGG = "AutopotYgg";

        public Key hpKey { get; set; }
        public Key hpBoxKey { get; set; }
        public int hpPercent { get; set; }
        public Key spKey { get; set; }
        public Key spBoxKey { get; set; }
        public int spPercent { get; set; }
        public int delay { get; set; } = 15;
        public int delayYgg { get; set; } = 50;

        public string actionName { get; set; }
        private _4RThread thread;

        public Autopot() { }
        public Autopot(string actionName)
        {
            this.actionName = actionName;
        }

        public Autopot(Key hpKey, Key hpBoxKey, int hpPercent, int delay, Key spKey, Key spBoxKey, int spPercent)
        {
            this.delay = delay;

            // HP
            this.hpKey = hpKey;
            this.hpBoxKey = hpBoxKey;
            this.hpPercent = hpPercent;

            // SP
            this.spKey = spKey;
            this.spBoxKey = spBoxKey;
            this.spPercent = spPercent;
        }

        public void Start()
        {
            Stop();
            Client roClient = ClientSingleton.GetClient();
            if(roClient != null)
            {
                int hpPotCount = 0;
                this.thread = new _4RThread(_ => AutopotThreadExecution(roClient, hpPotCount));
                _4RThread.Start(this.thread);
            }
        }

        private int AutopotThreadExecution(Client roClient, int hpPotCount)
        {
            // check hp first
            if (roClient.IsHpBelow(hpPercent))
            {
                pot(this.hpKey);
                hpPotCount++;

                if (hpPotCount == 3)
                {
                    hpPotCount = 0;
                    if (roClient.IsSpBelow(spPercent))
                    {
                        pot(this.spKey);
                    }
                }
            }
            // check sp
            if (roClient.IsSpBelow(spPercent))
            {
                pot(this.spKey);
            }

            if (roClient.IsHpBelow(hpPercent))
            {
                // Use HP box with 100 yggs
                if (roClient.HasSpace(3000))
                    pot(this.hpBoxKey);
            }

            if (roClient.IsSpBelow(spPercent))
            {
                // Use SP box with 100 yggs
                if (roClient.HasSpace(3000))
                    pot(this.spBoxKey);
            }

            Thread.Sleep(this.delay);
            return 0;
        }

        private void pot(Key key)
        {
            Keys k = (Keys)Enum.Parse(typeof(Keys), key.ToString());
            if ((k != Keys.None) && !Keyboard.IsKeyDown(Key.LeftAlt) && !Keyboard.IsKeyDown(Key.RightAlt))
            {
                Interop.PostMessage(ClientSingleton.GetClient().process.MainWindowHandle, Constants.WM_KEYDOWN_MSG_ID, k, 0); // keydown
                Interop.PostMessage(ClientSingleton.GetClient().process.MainWindowHandle, Constants.WM_KEYUP_MSG_ID, k, 0); // keyup
            }
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
            return this.actionName != null ? this.actionName : ACTION_NAME_AUTOPOT;
        }
    }
}
