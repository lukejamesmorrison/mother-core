using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{

    /// <summary>
    /// This class manages data storage for Mother. It leverages 
    /// Program.Storage to ensure data persists across cycles, 
    /// errors and in the event of a recompile.
    /// </summary>
    public class LocalStorage : BaseCoreModule
    {
        /// <summary>
        /// The Mother instance.
        /// </summary>
        //readonly Mother Mother;

        /// <summary>
        /// The storage string.
        /// </summary>
        public string StorageString { get; private set; } = "";

        /// <summary>
        /// The storage dictionary.
        /// </summary>
        readonly Dictionary<string, object> StorageDictionary = new Dictionary<string, object>();

        /// <summary>
        /// Has the storage been modified?
        /// </summary>
        bool IsDirty  = false;

        /// <summary>
        /// Constructor. We use the Program's storage string to manage our state.
        /// </summary>
        /// <param name="mother"></param>
        public LocalStorage(Mother mother) : base (mother)
        {
            //Mother = mother;
            StorageDictionary = Serializer.DeserializeDictionary(Mother.Program.Storage);
        }

        /// <summary>
        /// Boot the module. We register commands.
        /// </summary>
        public override void Boot()
        {
            Mother.GetModule<CommandBus>().RegisterCommand(new SetCommand(this));
            Mother.GetModule<CommandBus>().RegisterCommand(new GetCommand(this));
        }

        /// <summary>
        /// GetSaveData the storage string to the Program's Storage property.
        /// </summary>
        public string GetSaveData()
        {
            StorageString = Serializer.SerializeDictionary(StorageDictionary);
            IsDirty = false;

            return StorageString;
        }

        /// <summary>
        /// Clear stored data.
        /// </summary>
        /// <returns></returns>
        public bool Clear()
        {
            StorageDictionary.Clear();
            StorageString = "";
            return IsDirty = true;
        }

        /// <summary>
        /// Get a value from storage.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public string Get(string key)
        {
            string value = "";

            if (StorageDictionary.ContainsKey(key))
                value += $"{StorageDictionary[key]}";

            return value;
        }

        /// <summary>
        /// Set a value in storage.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool Set(string key, string value)
        {
            StorageDictionary[key] = value;
            IsDirty = true;
            return IsDirty;
        }
    }
}
