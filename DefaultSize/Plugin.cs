using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BoplFixedMath;
using HarmonyLib;
using Steamworks.Data;

namespace DefaultSize
{
    /// <summary>
    /// Changes the default size of all players. They get set to this size at the beginning of each level.
    ///     - If online, host's setting is used
    /// </summary>
    [BepInPlugin("me.antimality." + PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        private static Harmony harmony;

        // Config file
        internal static ConfigFile config;
        internal static ConfigEntry<float> defaultSizeSetting;

        private void Awake()
        {
            Log = Logger;
            config = Config;

            Log.LogInfo($"Plugin {PluginInfo.PLUGIN_NAME} is loaded!");
            harmony = new("me.antimality." + PluginInfo.PLUGIN_NAME);

            // Bind the config
            defaultSizeSetting = config.Bind("Settings", "Default player size", 1f, "Minimum is 0.01 (Lower would cap to minimum). The default game's max scale is 3.");
            // Lower cap
            if (defaultSizeSetting.Value < 0.01f)
            {
                defaultSizeSetting.Value = 0.01f;
                config.Save();
            }

            harmony.PatchAll(typeof(Patch));
        }

        private void OnDestroy()
        {
            harmony.UnpatchSelf();
        }
    }

    [HarmonyPatch]
    public class Patch
    {
        // Stored value
        private static Fix defaultSize;

        /// <summary>
        /// I will fully admit - this is not strictly necessary.
        /// This was added to ensure I don't desync when reloading the mod midgame for testing.
        /// Otherwise, you could just put the if-else block in OnGameStart and don't need this "getter"
        /// </summary>
        /// <returns></returns>
        // ONLY USE THIS TO GET THE VALUE
        private static Fix GetDefaultSize()
        {
            // Value already exists, return it
            if (defaultSize > Fix.Zero) return defaultSize;

            /// Initialize value:
            // If online
            if (GameLobby.isOnlineGame)
            {
                // Use host's default size setting
                defaultSize = (Fix)float.Parse(SteamManager.instance.currentLobby.GetData("DefaultSize"));
            }
            // If local
            else
            {
                // Use value from config
                defaultSize = (Fix)Plugin.defaultSizeSetting.Value;
            }
            return defaultSize;
        }

        /// <summary>
        /// Set the scale of the players at the begining of each round
        /// </summary>
        [HarmonyPatch(typeof(GameSessionHandler), "SpawnPlayers")]
        [HarmonyPostfix]
        public static void ChangePlayerSize()
        {
            // Change the size of all players
            foreach (Player player in PlayerHandler.Get().PlayerList())
            {
                player.Scale = GetDefaultSize();
            }
        }


        /// <summary>
        /// When you start a game, delete previously saved Default Size value
        /// </summary>
        [HarmonyPatch(typeof(GameSession), nameof(GameSession.Init))]
        [HarmonyPostfix]
        public static void OnGameStart()
        {
            // Reset the stored value
            defaultSize = Fix.Zero;
        }

        /// <summary>
        /// When creating a lobby, inject the DefaultSize value from config to the lobby
        /// Sadly, you can't set the value for the clients in this patch, because for SOME REASON
        /// If you join a local game while in an online lobby, BOPL doesn't create a new local lobby...
        /// </summary>
        [HarmonyPatch(typeof(SteamManager), "OnLobbyEnteredCallback")]
        [HarmonyPostfix]
        public static void OnEnterLobby(Lobby lobby)
        {
            // If I am the owner of this lobby, load the value
            if (SteamManager.LocalPlayerIsLobbyOwner)
            {
                // Harmony linting thinks this won't work (because I'm editing a parameter's value), but it does
                #pragma warning disable Harmony003
                lobby.SetData("DefaultSize", Plugin.defaultSizeSetting.Value.ToString());
            }
        }
    }
}

/*
    Don't Believe what you see, see what you believe.
    - Antimality.
*/
