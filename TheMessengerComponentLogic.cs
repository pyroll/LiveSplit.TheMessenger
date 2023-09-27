using LiveSplit.Model;
using LiveSplit.RuntimeText;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace LiveSplit.TheMessenger {
    public partial class TheMessengerComponent {

        private const string RoomTimerName = "Room Timer";
        private const int RoomTimerPrecisionCurrent = 2;
        private const int RoomTimerPrecisionPrevious = 3;
        private const string RoomTimerSeparator = " / ";
        private static readonly string DefaultLastRoomTime = "0." + new string('0', RoomTimerPrecisionPrevious) + RoomTimerSeparator;

        private string lastRoomKey;
        private string lastRoomTime = DefaultLastRoomTime;
        private readonly Stopwatch roomWatch = new Stopwatch();
        private RuntimeTextComponent roomComponent = null;
        private bool roomTimer = false;
        public bool RoomTimer {
            get => roomTimer;
            set {
                if(roomTimer = value) {
                    if(roomComponent == null) {
                        roomComponent = (RuntimeTextComponent)timer.CurrentState.Layout.Components.FirstOrDefault(c => c.ComponentName == "Runtime Text" || c.ComponentName == RoomTimerName);
                        if(roomComponent == null) {
                            roomComponent = new RuntimeTextComponent(timer.CurrentState, RoomTimerName, RoomTimerName) {
                                Value = lastRoomTime + "0." + new string('0', RoomTimerPrecisionCurrent)
                            };
                            timer.CurrentState.Layout.LayoutComponents.Add(new UI.Components.LayoutComponent("LiveSplit.RuntimeText.dll", roomComponent));
                        }
                    }
                } else {
                    if(roomComponent != null) {
                        foreach(UI.Components.ILayoutComponent component in timer.CurrentState.Layout.LayoutComponents) {
                            if(component.Component.ComponentName == "Runtime Text" || component.Component.ComponentName == RoomTimerName) {
                                timer.CurrentState.Layout.LayoutComponents.Remove(component);
                                break;
                            }
                        }
                        roomComponent = null;
                    }
                }
            }
        }

        private readonly RemainingDictionary remainingSplits;

        private readonly Dictionary<int, int> items = new Dictionary<int, int>();

        public override bool Update() {
            if(!memory.Update()) {
                return false;
            }

            if(roomTimer) {
                if(memory.RoomKey.Changed) {
                    if(String.IsNullOrEmpty(memory.RoomKey.New)) {
                        roomWatch.Stop(); //Stop during loading
                    } else {
                        if(String.IsNullOrEmpty(memory.RoomKey.Old)) {
                            if(lastRoomKey == memory.RoomKey.New) {
                                roomWatch.Start(); //Resume after loading
                            } else {
                                lastRoomTime = FormatRoomTimer(RoomTimerPrecisionPrevious) + RoomTimerSeparator;
                                roomWatch.Restart(); //Restart after loading
                            }
                        } else {
                            lastRoomTime = FormatRoomTimer(RoomTimerPrecisionPrevious) + RoomTimerSeparator;
                            roomWatch.Restart(); //Restart
                        }
                        lastRoomKey = memory.RoomKey.New;
                    }
                }
                roomComponent.Value = lastRoomTime + FormatRoomTimer(RoomTimerPrecisionCurrent);
            }

            if(settings.Reset == (int)EReset.MainMenu) {
                return true;
            } else {
                bool inMenu = memory.Scene.Old.Length < 8 || memory.Scene.New.Length < 8;
                bool isRunning = timer.CurrentState.CurrentPhase == TimerPhase.Running;
                return !(inMenu && isRunning);
            }
        }
        private string DetermineStartsWithCondition(int startMethod) {
            switch(startMethod) {
                case (int)EStart.NewGame:
                    return "Level_01";
                case (int)EStart.NVSkip:
                    return "Level_02";
                case (int)EStart.AnyLevel:
                default:
                    return "Level_";
            }
        }

        public override bool Start() {
            return (memory.Scene.Old.Length < 8 && memory.Scene.New.StartsWith(DetermineStartsWithCondition(settings.Start)))
                || (!memory.DlcSelection.Old && memory.DlcSelection.New);
        }

        public override void OnStart() {
            items.Clear();
            memory.ClearDlcProgression();
            remainingSplits.Setup(settings.Splits);
        }

        public override bool Split() {
            return remainingSplits.Count() != 0
                && (SplitLevel() ||
                    SplitBoss() ||
                    SplitInventory() ||
                    SplitCutscene() ||
                    SplitSeal() ||
                    SplitFeather() ||
                    SplitMask() ||
                    SplitCheckpoint() ||
                    SplitWindmill());

            bool SplitLevel() {
                return remainingSplits.ContainsKey("Level")
                    && memory.Scene.Changed && memory.Scene.Old.Length > 8 && memory.Scene.New.Length > 8
                    && remainingSplits.Split("Level", memory.SceneNameClean());
            }

            bool SplitBoss() {
                return remainingSplits.ContainsKey("Boss")
                    && memory.BossesCount.Old < memory.BossesCount.New
                    && remainingSplits.Split("Boss", memory.SceneNameClean());
            }

            bool SplitInventory() {
                if(!remainingSplits.ContainsKey("Inventory") || !memory.InventoryGen.Changed) {
                    return false;
                }

                foreach(ItemData item in memory.ItemSequence()) {
                    string splitName = null;
                    if(!items.ContainsKey(item.id)) {
                        items.Add(item.id, item.nb);
                        splitName = item.id + "";
                    } else if(items[item.id] < item.nb) {
                        items[item.id] = item.nb;
                        splitName = item.id + "_" + item.nb;
                    }
                    if(splitName != null && remainingSplits.Split("Inventory", splitName)) {
                        return true;
                    }
                }

                return false;
            }

            bool SplitCutscene() {
                return remainingSplits.ContainsKey("Cutscene")
                    && memory.CutscenesCount.Changed
                    && remainingSplits.Split("Cutscene", memory.ReadLastCutscene());
            }

            bool SplitSeal() {
                return remainingSplits.ContainsKey("Seal")
                    && memory.SealsCount.Changed
                    && remainingSplits.Split("Seal", memory.ReadLastSeal());
            }

            bool SplitFeather() {
                return remainingSplits.ContainsKey("Feather")
                    && memory.FeathersCount.Changed && memory.FeathersCount.New > 0
                    && remainingSplits.Split("Feather", memory.ReadLastFeather());
            }

            bool SplitMask() {
                return remainingSplits.ContainsKey("Mask")
                    && memory.MasksCount.Changed && memory.MasksCount.New > 0
                    && remainingSplits.Split("Mask", memory.ReadLastMask());
            }

            bool SplitCheckpoint() {
                return remainingSplits.ContainsKey("Checkpoint")
                    && memory.Checkpoint.Changed && memory.Checkpoint.New > -1
                    && remainingSplits.Split("Checkpoint", memory.SceneNameClean() + "_" + memory.Checkpoint.New);
            }

            bool SplitWindmill() {
                return remainingSplits.ContainsKey("UnlockWindmill")
                    && !memory.UseWindmill.Old && memory.UseWindmill.New
                    && remainingSplits.Split("UnlockWindmill");
            }
        }

        public override bool Reset() {
            return memory.Scene.Changed && memory.Scene.New.Length < 8;
        }

        public override void OnReset() {
            if(roomComponent != null) {
                roomComponent.Value = DefaultLastRoomTime + "0." + new string('0', RoomTimerPrecisionCurrent);
            }
            lastRoomKey = null;
            roomWatch.Reset();
        }

        public override bool Loading() {
            if(memory.QuarbleAnim == null) {
                memory.SearchLoading();
            }
            return memory.QuarbleAnim != null && memory.QuarbleAnim.New != default && memory.QuarbleInDone.New == default;
        }

        private string FormatRoomTimer(int msPrecision) {
            TimeSpan elapsed = roomWatch.Elapsed;

            StringBuilder sb = new StringBuilder();

            if(elapsed.TotalMinutes >= 1d) {
                sb.Append((int)elapsed.TotalMinutes);
                sb.Append(":");
            }

            sb.Append(elapsed.Seconds.ToString(elapsed.TotalSeconds >= 10d ? "D2" : "D"));

            if(msPrecision > 0 && msPrecision < 4) {
                sb.Append(".");
                if(msPrecision == 3) {
                    sb.Append(elapsed.Milliseconds.ToString("D3"));
                } else if(msPrecision == 2) {
                    sb.Append((elapsed.Milliseconds / 10).ToString("D2"));
                } else {
                    sb.Append(elapsed.Milliseconds / 100);
                }
            }

            return sb.ToString();
        }
    }
}