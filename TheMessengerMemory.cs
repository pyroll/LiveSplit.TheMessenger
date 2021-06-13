using System;
using System.Collections.Generic;
using Voxif.AutoSplitter;
using Voxif.Helpers.Unity;
using Voxif.IO;
using Voxif.Memory;

namespace LiveSplit.TheMessenger {
    public class TheMessengerMemory : Memory {

        protected override string[] ProcessNames => new string[] { "TheMessenger" };

        public StringPointer Scene { get; private set; }

        public Pointer<IntPtr> Progression { get; private set; }
        public Pointer<int> Checkpoint { get; private set; }
        public Pointer<int> BossesCount { get; private set; }
        public Pointer<IntPtr> CutscenesArray { get; private set; }
        public Pointer<int> CutscenesCount { get; private set; }
        public Pointer<IntPtr> SealsArray { get; private set; }
        public Pointer<int> SealsCount { get; private set; }
        public Pointer<IntPtr> MasksArray { get; private set; }
        public Pointer<int> MasksCount { get; private set; }
        public Pointer<IntPtr> FeathersArray { get; private set; }
        public Pointer<int> FeathersCount { get; private set; }
        public Pointer<bool> UseWindmill { get; private set; }

        public Pointer<IntPtr> InventoryDict { get; private set; }
        public Pointer<int> InventoryGen { get; private set; }

        public Pointer<IntPtr> UiInstance { get; private set; }
        public Pointer<IntPtr> QuarbleAnim { get; private set; }
        public Pointer<IntPtr> QuarbleInDone { get; private set; }

        public Pointer<bool> DlcSelection { get; private set; }

        public StringPointer RoomKey { get; private set; }

        private int bossesDefeatedOffset = 0;
        private int cutscenesPlayedOffset = 0;
        private int feathersCollectedOffset = 0;
        private int maskPiecesOffset = 0;

        private UnityHelperTask unityTask;

        public TheMessengerMemory(Logger logger) : base(logger) {
            OnHook += () => {
                unityTask = new UnityHelperTask(game, logger);
                unityTask.Run(InitPointers);
            };

            OnExit += () => {
                if(unityTask != null) {
                    unityTask.Dispose();
                    unityTask = null;
                }
            };
        }

        private void InitPointers(IMonoHelper unity) {
            MonoNestedPointerFactory ptrFactory = new MonoNestedPointerFactory(game, unity);

            Scene = ptrFactory.MakeString("LevelManager", "instance", "currentSceneName", ptrFactory.StringHeaderSize);
            Scene.StringType = EStringType.UTF16Sized;

            Progression = ptrFactory.Make<IntPtr>("ProgressionManager", "instance", out var progClass);

            Checkpoint = ptrFactory.Make<int>(Progression, unity.GetFieldOffset(progClass, "checkpointSaveInfo"), 0x38);

            bossesDefeatedOffset = unity.GetFieldOffset(progClass, "bossesDefeated");
            BossesCount = ptrFactory.Make<int>(Progression, bossesDefeatedOffset, 0x18);

            cutscenesPlayedOffset = unity.GetFieldOffset(progClass, "cutscenesPlayed");
            CutscenesArray = ptrFactory.Make<IntPtr>(Progression, cutscenesPlayedOffset, 0x10);
            CutscenesCount = ptrFactory.Make<int>(Progression, cutscenesPlayedOffset, 0x18);

            int sealOffset = unity.GetFieldOffset(progClass, "challengeRoomsCompleted");
            SealsArray = ptrFactory.Make<IntPtr>(Progression, sealOffset, 0x10);
            SealsCount = ptrFactory.Make<int>(Progression, sealOffset, 0x18);

            feathersCollectedOffset = unity.GetFieldOffset(progClass, "feathersCollected");
            FeathersArray = ptrFactory.Make<IntPtr>(Progression, feathersCollectedOffset, 0x10);
            FeathersCount = ptrFactory.Make<int>(Progression, feathersCollectedOffset, 0x18);

            maskPiecesOffset = unity.GetFieldOffset(progClass, "maskPiecesCollected");
            MasksArray = ptrFactory.Make<IntPtr>(Progression, maskPiecesOffset, 0x10);
            MasksCount = ptrFactory.Make<int>(Progression, maskPiecesOffset, 0x18);

            UseWindmill = ptrFactory.Make<bool>(Progression, unity.GetFieldOffset(progClass, "useWindmillShuriken"));

            InventoryDict = ptrFactory.Make<IntPtr>("InventoryManager", "instance", "itemQuantityByItemId", 0x20);
            InventoryGen = ptrFactory.Make<int>(InventoryDict, 0x50);

            UiInstance = ptrFactory.Make<IntPtr>("UIManager", "instance", "screensByType");

            DlcSelection = ptrFactory.Make<bool>("DLCManager", "instance", 0x34); //'dlcSelectionDone' missing in older ver.

            RoomKey = ptrFactory.MakeString("GameManager", "instance", "level", 0x98, 0x10, ptrFactory.StringHeaderSize);
            RoomKey.StringType = EStringType.UTF16Sized;

            QuarbleAnim = QuarbleInDone = null;

            logger.Log(ptrFactory.ToString());
                
            unityTask = null;
        }

        public override bool Update() => base.Update() && unityTask == null;

        public string SceneNameClean() {
            return Scene.New.EndsWith("_Build") ? Scene.New.Substring(6, Scene.New.Length - 12) : Scene.New.Substring(6);
        }

        public string ReadLastCutscene() {
            return ReadStringFromArray(CutscenesArray.New, CutscenesCount.New);
        }

        public string ReadLastSeal() {
            return ReadStringFromArray(SealsArray.New, SealsCount.New);
        }

        public string ReadLastFeather() {
            return ReadFromArray<int>(FeathersArray.New, FeathersCount.New).ToString();
        }

        public string ReadLastMask() {
            return ReadStringFromArray(MasksArray.New, MasksCount.New);
        }

        private unsafe T ReadFromArray<T>(IntPtr startArray, int count) where T : unmanaged {
            return game.Read<T>(startArray + 0x20 + sizeof(T) * (count - 1));
        }

        private string ReadStringFromArray(IntPtr startArray, int count) {
            return game.ReadString(game.Read<IntPtr>(startArray + 0x20 + 0x8 * (count - 1)) + 0x14, EStringType.UTF16Sized);
        }

        public IEnumerable<ItemData> ItemSequence() {
            IntPtr idPtr = game.Read<IntPtr>(InventoryDict.New + 0x20);
            IntPtr nbPtr = game.Read<IntPtr>(InventoryDict.New + 0x28);

            int size = game.Read<int>(idPtr + 0x18);
            byte[] ids = game.Read(idPtr + 0x20, size * 4);
            byte[] nbs = game.Read(nbPtr + 0x20, size * 4);

            for(int i = 0; i < size; i++) {
                int id = ids.To<int>(i * 4);
                if(id == 0) {
                    continue;
                }
                int nb = nbs.To<int>(i * 4);
                yield return new ItemData(id, nb);
            }
        }

        public void SearchLoading() {
            IntPtr screensPtr = game.Read<IntPtr>(UiInstance.New + 0x28);
            int screensCount = game.Read<int>(UiInstance.New + 0x38);
            for(int i = 0; i < screensCount; i++) {
                int offset = 0x20 + 0x8 * i;
                IntPtr screenNamePtr = game.Read<IntPtr>(screensPtr, offset, 0x10, 0x20, 0x10, 0x30, 0x68);
                string screenName = game.ReadString(screenNamePtr, EStringType.UTF8);
                if(!screenName.StartsWith("LoadingAnimation")) {
                    continue;
                }
                Pointer<IntPtr> view = new NodePointer<IntPtr>(game, UiInstance, 0x28, offset, 0x10, 0x20);
                QuarbleAnim = new NodePointer<IntPtr>(game, view, 0x18);
                QuarbleInDone = new NodePointer<IntPtr>(game, view, 0x20);
                break;
            }
        }

        private static readonly HashSet<string> DlcProgressionsToRemove = new HashSet<string>() {
            "SurfBossIntroCutscene", "SurfBoss",
            "TotemBossIntroCutscene", "Totem",
            "RaceWinCutscene", "StartFinalPPBossCutscene", "PunchOut"
        };
        public void ClearDlcProgression() {
            if(unityTask != null || Progression == null) {
                return;
            }
            ClearDlcList(bossesDefeatedOffset);
            ClearDlcList(cutscenesPlayedOffset);

            ClearDlcCount(feathersCollectedOffset);
            ClearDlcCount(maskPiecesOffset);

            void ClearDlcList(int listOffset) {
                IntPtr progressionOffset = game.Read<IntPtr>(Progression.New + listOffset);
                IntPtr listPtr = game.Read<IntPtr>(progressionOffset + 0x10);
                int listCount = game.Read<int>(progressionOffset + 0x18);
                int listId = 0;
                while(listId < listCount) {
                    IntPtr stringPtr = game.Read<IntPtr>(listPtr + 0x20 + 0x8 * listId);
                    string stringValue = game.ReadString(stringPtr + 0x14, EStringType.UTF16Sized);
                    if(DlcProgressionsToRemove.Contains(stringValue)) {
                        if(listId != listCount - 1) {
                            IntPtr lastStringPtr = game.Read<IntPtr>(listPtr + 0x20 + 0x8 * (listCount - 1));
                            game.Write(lastStringPtr, listPtr + 0x20 + 0x8 * listId);
                        }
                        game.Write(--listCount, progressionOffset + 0x18);
                    } else {
                        ++listId;
                    }
                }
            }

            void ClearDlcCount(int listOffset) {
                IntPtr progressionOffset = game.Read<IntPtr>(Progression.New + listOffset);
                game.Write(0, progressionOffset + 0x18);
            }
        }
    }

    public struct ItemData {
        public int id;
        public int nb;

        public ItemData(int id, int nb) {
            this.id = id;
            this.nb = nb;
        }
    }
}