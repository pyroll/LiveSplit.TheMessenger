using LiveSplit.Model;
using LiveSplit.UI.Components;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using Voxif.AutoSplitter;
using Voxif.IO;

[assembly: ComponentFactory(typeof(Factory))]
namespace LiveSplit.TheMessenger {
    public partial class TheMessengerComponent : Voxif.AutoSplitter.Component {

        public enum EStart {
            [Description("Off")]
            Off,
            [Description("New Game")]
            NewGame,
            [Description("Any Level")]
            AnyLevel,
            [Description("NV Skip")]
            NVSkip
        }

        public enum EReset {
            [Description("Off")]
            Off,
            [Description("Main Menu")]
            MainMenu
        }

        public enum EOption {
            [Description("Room Timer"), Type(typeof(OptionCheckBox))]
            RoomTimer
        }

        protected override SettingsInfo? StartSettings => new SettingsInfo((int)EStart.NewGame, GetEnumDescriptions<EStart>());
        protected override SettingsInfo? ResetSettings => new SettingsInfo((int)EReset.Off, GetEnumDescriptions<EReset>());
        protected override OptionsInfo? OptionsSettings => new OptionsInfo(null, CreateControlsFromEnum<EOption>());
        protected override EGameTime GameTimeType => EGameTime.Loading;

        private TheMessengerMemory memory;

        public TheMessengerComponent(LiveSplitState state) : base(state) {
#if DEBUG
            logger = new ConsoleLogger();
#else
            logger = new  FileLogger("_" + Factory.ExAssembly.GetName().Name.Substring(10) + ".log");
#endif
            logger.StartLogger();

            memory = new TheMessengerMemory(logger);

            settings = new TreeSettings(state, StartSettings, ResetSettings, OptionsSettings) {
                convertableSettings = new Dictionary<string, string> { { "Unlock_Windmill", "UnlockWindmill" } }
            };
            settings.OptionChanged += OptionChanged;

            remainingSplits = new RemainingDictionary(logger);
        }

        private void OptionChanged(object sender, OptionEventArgs e) {
            switch(Enum.Parse(typeof(EOption), e.Name)) {
                case EOption.RoomTimer:
                    RoomTimer = e.State == 1;
                    break;
            }
        }

        public override void Dispose() {
            settings.OptionChanged -= OptionChanged;
            RoomTimer = false;
            memory.Dispose();
            memory = null;
            base.Dispose();
        }
    }
}