using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Text;
using System.IO;

using UnityEngine;

using VRCModLoader;
using VRChat.UI.QuickMenuUI;
using VRCMenuUtils;
using Newtonsoft.Json;

namespace VRCDynamicBones
{
    public static class VRCDBAExtensions
    {
        public static Vector3 GetEyesPosition(this VRC.Player player)
        {
            if (player != null && player.vrcPlayer != null && player.vrcPlayer.avatarGameObject != null) {
                var avatarDescriptor = player.vrcPlayer.avatarGameObject.GetComponent<VRCSDK2.VRC_AvatarDescriptor>();
                if (avatarDescriptor != null)
                    return avatarDescriptor.ViewPosition;
            }
            return Vector3.zero;
        }
    }

    [VRCModInfo("VRCDynamicBones", "1.0.4", "Kova")]
    internal class VRCDynamicBones : VRCMod
    {
        static QuickMenu quickMenu;
        static VRCEUiQuickMenu menuPage;

        public static QuickMenu MyQuickMenu {
            get {
                if (quickMenu == null)
                    quickMenu = ((QuickMenu)typeof(QuickMenu).GetMethod("get_Instance", BindingFlags.Public | BindingFlags.Static).Invoke(null, null));
                return quickMenu;
            }
        }

        class Config
        {
            public float menuButtonPositionX    = -1;
            public float menuButtonPositionY    = 1;

            public bool  enableAdvancedSettings = false;  // Enables Advanced Settings (i.e. control over dynamic bones with this mod)
            public int   dynamicBonesMode       = 0;      // -1..2 : Disabled / Local for everyone / Between you and other players / Between all players
            public float workingDistance        = 5;      // Maximum distance from you to dynamic bones at which they will stay enabled
            public int   updateRateMode         = 1;      // 0..1 : Constant / Distance Dependent
            public float maxUpdateRate          = 0;      // Update rate for dynamic bones that are local or that are very close to you when Distance Dependent mode is enabled
            public float minUpdateRate          = 30;     // Update rate for dynamic bones that are far away
            public int   localCollidersFilter   = 0;      // 0..2 : Enables specific colliders filter mode for local user    : All / Chest and up / Hands only
            public int   othersCollidersFilter  = 0;      // 0..2 : Enables specific colliders filter mode for other players : All / Chest and up / Hands only
            
            public string version = "1.04";

            public static string DefaultPath {
                get { return Directory.GetParent(Application.dataPath) + "/UserData/dynamicbonesprefs.json"; }
            }

            public static Config Load(string path)
            {
                Config config = null;
                try {
                    config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(path));
                }
                catch (Exception) {
                    config = new Config();
                }
                return config;
            }

            public void Save(string path)
            {
                string dir = Path.GetDirectoryName(path);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                string json = JsonConvert.SerializeObject(this);
                File.WriteAllText(path, json);
            }
        }

        class PlayerInfo
        {
            public GameObject gameObject;
            public string     displayName;

            public PlayerInfo(GameObject gameObject, string displayName) {
                this.gameObject  = gameObject;
                this.displayName = displayName;
            }
        }

        const float playersUpdateInterval = 1f/10f;
        
        List<PlayerInfo> players = new List<PlayerInfo>(100);

        VRCEUiQuickButton openMenuButton;
        VRCEUiQuickButton enableButton, bonesModeButton, distanceButton, updateRateModeButton;
        VRCEUiQuickButton localCollidersFilterButton, othersCollidersFilterButton, maxUpdateRateButton, minUpdateRateButton;
        
        Config config;
        
        CollisionsManager collisionManager = new CollisionsManager();

        StreamWriter logFile;

        #region Start & Update Loop

        void OnApplicationStart()
        {
            Log("Start " + this.Name);
            ModManager.StartCoroutine(Setup());
        }

        IEnumerator Setup()
        {
            // Wait for load
            yield return VRCMenuUtilsAPI.WaitForInit();
            
            if (!HasNewerVersionInstalled()) {
                Log("Setup " + this.Name + "...");
                Log("  load config... " + Config.DefaultPath);
                config = Config.Load(Config.DefaultPath);
                Log("  apply settings...");
                ApplySettings();
                Log("  setup menu...");
                UpdateMenu();
                Log("Success");
            }
        }

        bool HasNewerVersionInstalled()
        {
            foreach (var mod in ModManager.Mods) {
                if (mod != this && mod.Name.Equals(this.Name) && String.Compare(mod.Version, this.Version) >= 0)
                    return true;
            }
            return false;
        }

        void UpdateMenu()
        {
            if (openMenuButton == null) {
                var menuButtonPos = new Vector2(-630 + 420 * config.menuButtonPositionX, 1470 - 420 * config.menuButtonPositionY);
                openMenuButton = new VRCEUiQuickButton(this.Name + "MenuButton", menuButtonPos, "Dynamic Bones Advanced", "Dynamic Bones Advanced Settings", MyQuickMenu.transform.Find("ShortcutMenu"));
                openMenuButton.Control.gameObject.SetActive(true);
                openMenuButton.OnClick += OpenMenu;
                
                menuPage = new VRCEUiQuickMenu(this.Name + "Menu", true);
                
                // Buttons:
                //   Advanced Settings        Enabled (control over dynamic bones) / Disabled (default dynamic bones behaviour)
                //   Dynamic Bones Mode       LOCAL / GLOBAL (YOU) / GLOBAL (EVERYONE)  (Local / Between You and Other Players / Between All Players / Disabled)
                //   Working Distance         3m/5m/10m/20m/40m/INFINITE                (Maximum distance from user to dynamic bones at which they will stay enabled)
                //   Update Rate              Constant / Distance Dependent             (Constant update rate or distance dependent)
                //   Max Update Rate          30/60/90/120/Display Rate                 (Update rate for dynamic bones that are local or close to the user)
                //   Min Update Rate          15/30/60/90/Display Rate                  (Update rate for dynamic bones that are far away)
                //   Local Colliders Filter   ALL / UPPER BODY / HANDS ONLY             (Filters specific colliders for local user avatar)
                //   Others Colliders Filter  ALL / UPPER BODY / HANDS ONLY             (Filters specific colliders for other players)

                enableButton                = AddButton("", "Advanced Settings:\nEnabled / Disabled (default dynamic bones behaviour)", ToggleAdvancedSettings);
                bonesModeButton             = AddButton("", "Dynamic Bones Mode:\nLocal / Between You and Other Players / Between All Players / Disabled", ToggleBonesMode);
                distanceButton              = AddButton("", "Maximum distance from user to dynamic bones\nat which they will stay enabled", ToggleDistance);
                updateRateModeButton        = AddButton("", "Constant update rate or\nDistance Dependent update rate", ToggleUpdateRateMode);
                localCollidersFilterButton  = AddButton("", "Filters specific colliders for your avatar:\nAll / Chest and Up / Hands Only", ToggleLocalCollidersFilterMode);
                othersCollidersFilterButton = AddButton("", "Filters specific colliders for other players:\nAll / Chest and Up / Hands Only", ToggleOthersCollidersFilterMode);
                maxUpdateRateButton         = AddButton("", "Update rate for dynamic bones\nthat are local or close to the user", ToggleMaxUpdateRate);
                minUpdateRateButton         = AddButton("", "Update rate for dynamic bones\nthat are far away", ToggleMinUpdateRate);
            }
            
            enableButton.Text                = "Advanced Settings\n" + (config.enableAdvancedSettings ? "ENABLED" : "DISABLED");
            bonesModeButton.Text             = "Mode\n"              + new string[] { "DISABLED", "LOCAL", "GLOBAL\nYOU", "GLOBAL\nEVERYONE" }[config.dynamicBonesMode+1];
            distanceButton.Text              = "Working Distance\n"  + (config.workingDistance <= 0 ? "INFINITE" : " " + (config.workingDistance).ToString() + "m");
            updateRateModeButton.Text        = "Update Rate Mode\n"  + (config.updateRateMode == 0 ? "CONSTANT" : "DISTANCE");
            localCollidersFilterButton.Text  = "Your Colliders\n"    + (config.localCollidersFilter  == 0 ? "ALL" : (config.localCollidersFilter  == 1 ? "TOP" : "HANDS"));
            othersCollidersFilterButton.Text = "Other's Colliders\n" + (config.othersCollidersFilter == 0 ? "ALL" : (config.othersCollidersFilter == 1 ? "TOP" : "HANDS"));
            maxUpdateRateButton.Text         = (config.updateRateMode == 0 ? "Update Rate\n" : "Max Rate\n") + (config.maxUpdateRate > 0 ? " " + (config.maxUpdateRate).ToString() : "DISPLAY");
            minUpdateRateButton.Text         = "Min Rate\n" + (config.minUpdateRate > 0 ? " " + (config.minUpdateRate).ToString() : "DISPLAY");
            
            // show/hide buttons
            bonesModeButton.Control.gameObject.SetActive(config.enableAdvancedSettings);
            distanceButton.Control.gameObject.SetActive(config.enableAdvancedSettings);
            updateRateModeButton.Control.gameObject.SetActive(config.enableAdvancedSettings);
            localCollidersFilterButton.Control.gameObject.SetActive(config.enableAdvancedSettings);
            othersCollidersFilterButton.Control.gameObject.SetActive(config.enableAdvancedSettings);
            maxUpdateRateButton.Control.gameObject.SetActive(config.enableAdvancedSettings);
            minUpdateRateButton.Control.gameObject.SetActive(config.enableAdvancedSettings && config.updateRateMode != 0);
        }

        static int buttonsAdded = 0;

        VRCEUiQuickButton AddButton(string text, string tooltip, Action action)
        {
            int xi = buttonsAdded % 4;
            int yi = buttonsAdded / 4;
            Vector2 pos = new Vector2(-625 + xi * 420, 1050 - yi * 420);
            var button = new VRCEUiQuickButton(this.Name + "Button" + buttonsAdded, pos, text, tooltip, menuPage.Control.transform);
            button.Control.gameObject.SetActive(true);
            button.OnClick += action;
            buttonsAdded++;
            return button;
        }

        public void OpenMenu()
        {
            collisionManager.showDebugColliders = true;  // show debug colliders while mod menu is open

            VRCMenuUtilsAPI.ShowQuickMenuPage(menuPage.Control);
        }
        
        void OnLevelWasLoaded(int level)
        {
            Log("OnLevelWasLoaded (" + level + ")");
            players.Clear();
            collisionManager.Clear();
        }

        void OnLevelWasInitialized(int level)
        {
            Log("OnLevelWasInitialized (" + level + ")");
        }
        
        float lastTimePlayersUpdated = 0;
        
        void OnUpdate()
        {
            if (config == null || !config.enableAdvancedSettings)
                return;

            float time = Time.time;
            if (time - lastTimePlayersUpdated >= playersUpdateInterval) {
                UpdatePlayers();
                lastTimePlayersUpdated = time;
            }
            
            if (collisionManager.showDebugColliders && menuPage != null && menuPage.Control != null && !menuPage.Control.gameObject.activeSelf)
                collisionManager.showDebugColliders = false;  // hide debug colliders

            collisionManager.Update();
        }

        bool ValidatePlayerAvatar(VRC.Player player)
        {
            return !(player == null ||
                     player.vrcPlayer == null ||
                     player.vrcPlayer.isActiveAndEnabled == false ||
                     player.vrcPlayer.avatarAnimator == null ||
                     player.vrcPlayer.avatarGameObject == null ||
                     player.vrcPlayer.avatarGameObject.name.IndexOf("Avatar_Utility_Base_") == 0);
        }
        
        bool PlayerListContainsPlayer(List<PlayerInfo> playerList, PlayerInfo player)
        {
            foreach (PlayerInfo p in playerList) {
                if (Equals(p.gameObject, player.gameObject))
                    return true;
            }
            return false;
        }
        
        void UpdatePlayers()
        {
            if (VRC.Core.APIUser.CurrentUser == null)
                return;
            
            // Remove destroyed objects
            for (int i = 0; i < players.Count; i++) {
                PlayerInfo p = players[i];
                if (p.gameObject == null) {
                    Log("Remove Player: " + p.displayName);
                    players.RemoveAt(i);
                    i--;
                }
                i++;
            }

            VRC.Player[] vrcPlayers = VRC.PlayerManager.GetAllPlayers();
            
            foreach (VRC.Player player in vrcPlayers) {
                if (ValidatePlayerAvatar(player)) {
                    PlayerInfo p = new PlayerInfo(player.vrcPlayer.avatarGameObject, player.ToString());

                    if (!PlayerListContainsPlayer(players, p)) {
                        bool isLocalPlayer = player.name.Contains("Local");
                        Log("Add " + (isLocalPlayer ? "Local Player: " : "Player: ") + p.displayName);
                        collisionManager.AddPlayer(p.gameObject, isLocalPlayer, p.displayName, player.GetEyesPosition().y);
                        players.Add(p);
                    }
                }
            }
        }

        #endregion
        #region Functions

        public void ToggleAdvancedSettings()
        {
            Log("ToggleMod()");
            config.enableAdvancedSettings = !config.enableAdvancedSettings;
            if (config.enableAdvancedSettings)
                config.dynamicBonesMode = Mathf.Max(config.dynamicBonesMode, 0);
            else {
                Log("Advanced Settings has been disabled");
                players.Clear();
                collisionManager.Clear();
            }
            ApplySettings();
            UpdateMenu();
        }
        
        public void ToggleBonesMode()
        {
            Log("ToggleBonesMode()");
            config.dynamicBonesMode = NextValue(new int[] { 0, 1, 2, -1 }, config.dynamicBonesMode);  // Local / Between You and Other Players / Between All Players / Disabled
            ApplySettings();
            UpdateMenu();
        }

        public void ToggleDistance()
        {
            Log("ToggleDistance()");
            config.workingDistance = NextValue(new int[]{ 3, 5, 10, 20, 40, 0 }, Mathf.RoundToInt(config.workingDistance));  // 3m/5m/10m/20m/40m/INFINITE
            ApplySettings();
            UpdateMenu();
        }

        public void ToggleUpdateRateMode()
        {
            Log("ToggleUpdateRateMode()");
            config.updateRateMode = NextValue(new int[] { 0, 1 }, config.updateRateMode);
            ApplySettings();
            UpdateMenu();
        }

        public void ToggleMaxUpdateRate()
        {
            Log("ToggleMaxUpdateRate()");
            config.maxUpdateRate = NextValue(new int[] { 30, 60, 90, 120, 0 }, Mathf.RoundToInt(config.maxUpdateRate));  // 30/60/90/120/Display Rate
            if (config.maxUpdateRate > 0 && config.minUpdateRate >= config.maxUpdateRate)
                config.minUpdateRate = config.maxUpdateRate;
            ApplySettings();
            UpdateMenu();
        }

        public void ToggleMinUpdateRate()
        {
            Log("ToggleMinUpdateRate()");
            config.minUpdateRate = NextValue(new int[] { 15, 30, 60, 90, 0 }, Mathf.RoundToInt(config.minUpdateRate));  // 15/30/60/90/Display Rate
            if (config.maxUpdateRate > 0 && config.minUpdateRate >= config.maxUpdateRate)
                config.minUpdateRate = 0;
            ApplySettings();
            UpdateMenu();
        }

        public void ToggleLocalCollidersFilterMode()
        {
            Log("ToggleLocalCollidersFilterMode()");
            config.localCollidersFilter = NextValue(new int[] { 0, 1, 2 }, config.localCollidersFilter);  // ALL / UPPER BODY / HANDS ONLY
            ApplySettings();
            UpdateMenu();
        }

        public void ToggleOthersCollidersFilterMode()
        {
            Log("ToggleOthersCollidersFilterMode()");
            config.othersCollidersFilter = NextValue(new int[] { 0, 1, 2 }, config.othersCollidersFilter);  // ALL / UPPER BODY / HANDS ONLY
            ApplySettings();
            UpdateMenu();
        }

        int NextValue(int[] values, int currentValue)
        {
            if (values.Length == 0)
                return currentValue;
            
            int i = 0;
            if (values.Contains(currentValue)) {
                i = Array.IndexOf(values, currentValue) + 1;
                if (i >= values.Length)
                    i = 0;
            }
            return values[i];
        }
        
        void ApplySettings()
        {
            collisionManager.dynamicBonesMode      = (CMDynamicBonesMode)config.dynamicBonesMode;
            collisionManager.workingDistance       = config.workingDistance;
            collisionManager.updateRateMode        = (CMUpdateRateMode)config.updateRateMode;
            collisionManager.maxUpdateRate         = config.maxUpdateRate;
            collisionManager.minUpdateRate         = config.minUpdateRate;
            collisionManager.localCollidersFilter  = (CMCollidersFilter)config.localCollidersFilter;
            collisionManager.othersCollidersFilter = (CMCollidersFilter)config.othersCollidersFilter;

            config.Save(Config.DefaultPath);
        }
        
        #endregion
        #region Utilites
        
        void Log(string s)
        {
            //VRCModLogger.Log("[VRCDynamicBones] " + s);

            DateTime dateTime = DateTime.Now;
            if (logFile == null) {
                string logsPath = Directory.GetParent(Application.dataPath) + "/Logs";
                if (!Directory.Exists(logsPath))
                    Directory.CreateDirectory(logsPath);
                string logPath = logsPath + "/VRCDynamicBones " + String.Format("{0:yyyy-MM-dd HH-mm-ss-fff}", dateTime) + ".log";
                logFile = new StreamWriter(logPath, true);
                logFile.AutoFlush = true;
            }
            logFile.WriteLine(String.Format("{0:[HH:mm:ss.fff] }", dateTime) + s);
        }

        #endregion
    }
}
