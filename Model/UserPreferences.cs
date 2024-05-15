using Newtonsoft.Json;
using System.Windows.Forms;

namespace _4RTools.Model
{
    public class UserPreferences : Action
    {
        private string ACTION_NAME = "UserPreferences";
        public string toggleStateKey { get; set; } = Keys.End.ToString();
        public bool enabledAntibot { get; set; } = false;
        public string passwordText { get; set; } = "";
        public bool enabledAutoStorage { get; set; } = false;
        public string storageTextKey { get; set; } = Keys.None.ToString();
        public string alootidTextKey { get; set; } = Keys.None.ToString();

        public UserPreferences()
        {
        }

        public void Start() { }

        public void Stop() { }

        public string GetConfiguration()
        {
            return JsonConvert.SerializeObject(this);
        }

        public string GetActionName()
        {
            return ACTION_NAME;
        }
    }
}
