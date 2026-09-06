#if !UNITY_EDITOR
using BepInEx;
using Comfort.Common;
using EFT;
using EFT.UI;
using PackNStrap.Patches;
using SPT.Reflection.Utils;
using System;
using System.IO;


namespace PackNStrap
{
    [BepInDependency("com.cj.useFromAnywhere", "1.3.2")]
    [BepInPlugin(
    PluginConstants.Guid,
    PluginConstants.Name,
    PluginConstants.Version)]

    internal class PackNStrap : BaseUnityPlugin
    {
        public static PackNStrap Instance;
        private static GameWorld _gameWorld;
        public static Player Player;
        public static string PlayerNickname;
        private static GameUI _gameUI;
        private static Profile _playerProfile;
        public static IEftSession BackEndSession;
        public static readonly string PluginPath = Path.Combine(Environment.CurrentDirectory, "BepInEx", "plugins");

        internal void Awake()
        {
            Instance = this;

            new GetPrioritizedGridsForUnloadedObjectPatch().Enable();
            new MergeContainerWithChildrenPatch().Enable();
            new UnloadWeaponPatch().Enable();
            new FindSlotForPickupPatch().Enable();
            new RegisterCustomItemTypesPatch().Enable();
        }

        internal void Update()
        {
            if (Singleton<GameWorld>.Instantiated && (_gameWorld == null || _gameUI == null || Player == null))
            {
                _gameWorld = Singleton<GameWorld>.Instance;
                _gameUI = MonoBehaviourSingleton<GameUI>.Instance;
                Player = Singleton<GameWorld>.Instance.MainPlayer;
                _playerProfile = PatchConstants.BackEndSession.Profile;
                PlayerNickname = _playerProfile.Nickname;
                BackEndSession = PatchConstants.BackEndSession;
            }
        }
    }
}
#endif
