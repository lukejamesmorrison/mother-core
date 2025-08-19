using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using Sandbox.ModAPI.Interfaces.Terminal;
using SpaceEngineers.Game.Entities.Blocks;
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
using VRage.Game.VisualScripting.Utils;
using VRage.Generics;
using VRage.Scripting.MemorySafeTypes;
using VRageMath;

namespace IngameScript
{

    /// <summary>
    /// The main program class.
    /// </summary>
    public class Mother
    {
        /// <summary>
        /// The Program wrapper. This is used to access the 
        /// Programmable Block's API.
        /// </summary>
        public MyGridProgram Program;

        /// <summary>
        /// The Name of the system.
        /// </summary>
        public string SystemName = "Mother OS";

        /// <summary>
        /// The command line tool used to parse Arguments and 
        /// options from the Space Engineers terminal.
        /// </summary>
        public MyCommandLine commandLine;

        /// <summary>
        /// Is Autopilot engaged?
        /// 
        /// We should move this to the FCS module.
        /// </summary>
        public bool AutopilotEngaged = false;

        /// <summary>
        /// The CubeGrid that this program is running on.
        /// </summary>
        public IMyCubeGrid CubeGrid;

        /// <summary>
        /// The Grid Terminal System for the current grid.
        /// </summary>
        public IMyGridTerminalSystem GridTerminalSystem;

        /// <summary>
        /// The Intergrid Communication System for the current grid.
        /// </summary>
        public IMyIntergridCommunicationSystem IGC;

        /// <summary>
        /// The Programmable Block that this program is running on.
        /// </summary>
        public IMyProgrammableBlock ProgrammableBlock;

        /// <summary>
        /// The Remote Control block for the current grid. This is used to access 
        /// positional data and manage autopilot.
        /// </summary>
        public IMyRemoteControl RemoteControl;

        /// <summary>
        /// The Runtime Info for the Program.
        /// </summary>
        public IMyGridProgramRuntimeInfo Runtime;

        /// <summary>
        /// The Id of the system. This is equal to IGC.Me to ensure 
        /// uniqueness when communications with other grids.
        /// </summary>
        public long Id;

        /// <summary>
        /// The short Id of the system. We use the last 5 digits of the Id.
        /// </summary>
        public string ShortId;

        /// <summary>
        /// The EntityId of the grid connected to the programmable block.
        /// </summary>
        public long GridId;

        /// <summary>
        /// The name of the grid. This can be used by 
        /// other players or grids to run commands on this grid.
        /// <see cref="AlmanacRecord.Nicknames"/>
        /// </summary>
        public string Name;

        /// <summary>
        /// The SafeZone is a bounding sphere around the grid. This is used for 
        /// flight planning to reduce the change of a collision.
        /// </summary>
        public BoundingSphereD SafeZone;
       
        /// <summary>
        /// The various system states of Mother Core. They represent the 
        /// top level state of the Program.
        /// </summary>
        public enum SystemStates
        {
            /// <summary>
            /// The system is uninitialized. This is the initial 
            /// state of Mother.
            /// </summary>
            UNINITIALIZED,
            /// <summary>
            /// The system is booting up. Mother will boot all 
            /// registered modules in order or registration.
            /// </summary>
            BOOT,
            /// <summary>
            /// The system is working. Mother has booted all modules 
            /// and is running as expected.
            /// </summary>
            WORKING,    
            /// <summary>
            /// The system is in test mode. This is used for debugging 
            /// and testing purposes.
            /// </summary>
            TEST,           
            /// <summary>
            /// The system has failed. This is used when Mother 
            /// encounters an unrecoverable error.
            /// </summary>
            FAIL,           
        }

        /// <summary>
        /// The current system state.
        /// </summary>
        public SystemStates SystemState = SystemStates.UNINITIALIZED;

        /// <summary>
        /// Is debug mode enabled?
        /// </summary>
        public bool DebugMode = false;

        /// <summary>
        /// The list of all modules registered with Mother. This includes both core 
        /// and extension modules.
        /// </summary>
        public Dictionary<string, IModule> AllModules = new Dictionary<string, IModule>();

        /// <summary>
        /// The order in which modules are registered. This is used to boot them in a coroutine.
        /// </summary>
        private readonly List<IModule> ModulesInOrderOfRegistration = new List<IModule>();

        /// <summary>
        /// The list of all core modules registered with Mother. These modules are 
        /// essential to the operation of the system.
        /// </summary>
        public Dictionary<string, ICoreModule> CoreModules = new Dictionary<string, ICoreModule>();

        /// <summary>
        /// The list of all extension modules registered with Mother. These modules 
        /// are optional and can be added or removed as needed.
        /// </summary>
        public Dictionary<string, IExtensionModule> ExtensionModules = new Dictionary<string, IExtensionModule>();

        /// <summary>
        /// The list of all commands registered with Mother. These commands are used 
        /// to interact with the system.
        /// </summary>
        public List<IModuleCommand> Commands = new List<IModuleCommand>();

        /// <summary>
        /// The list commands defined within the programmable block's Custom Data.
        /// </summary>
        public Dictionary<string, string> ConfigCommands = new Dictionary<string, string>();

        /// <summary>
        /// Constructor. We initialize our system with the Program class 
        /// and register the Core Modules.
        /// </summary>
        /// <param name="program"></param>
        public Mother(MyGridProgram program)
        {
            Initialize(program);
        }

        /// <summary>
        /// Initialize Mother. We run high level system processes and register 
        /// the core modules. We persist the Program class to simplify dependencies 
        /// on the Programmable Block interface for modules.
        /// </summary>
        /// <param name="program"></param>
        public void Initialize(MyGridProgram program)
        {
            // setup Program references to simplify access by modules.
            Program = program;
            IGC = Program.IGC;
            ProgrammableBlock = Program.Me;
            CubeGrid = ProgrammableBlock.CubeGrid;
            GridTerminalSystem = Program.GridTerminalSystem;
            Runtime = Program.Runtime;

            // Set up important properties
            Id = IGC.Me;
            ShortId = $"{Id}".Substring($"{Id}".Length - 5);
            GridId = CubeGrid.EntityId;
            Name = ProgrammableBlock.CubeGrid.CustomName;

            RegisterCoreModules();
        }
          
        /// <summary>
        /// Register the Core Modules.  
        /// These modules should be defined in their desired boot and run order.
        /// </summary>
        public void RegisterCoreModules()
        {
            List<ICoreModule> modules = new List<ICoreModule> {
                // ESSENTIAL
                new Log(this),
                new Configuration(this),
                new Clock(this),
                new EventBus(this),
                new CommandBus(this),
                new LocalStorage(this),
                new Debug(this),

                // CRITICAL
                new BlockCatalogue(this),
                new ActivityMonitor(this),
                new Almanac(this),
                new IntergridMessageService(this),

                // FUNCTIONAL
                new Terminal(this),

                // BLOCK BASED (FOR CONNECTIONS)
                new ConnectorModule(this),
                new MergeBlockModule(this),
            };

            modules.ForEach(module => RegisterCoreModule(module));
        }

        /// <summary>
        /// Set the system state.
        /// </summary>
        /// <param name="state"></param>
        void SetState(SystemStates state)
        {
            SystemState = state;
        }

        /// <summary>
        /// Boot the system. This is called after all core and extension modules have been registered. 
        /// Core modules are booted before extension modules. Modules are booted in the order they 
        /// have been registered. 
        /// </summary>
        public void Boot()
        {
            SetState(SystemStates.BOOT);

            // clear clock schedule
            GetModule<Clock>().ClearScheduledTasks();

            Print("Booting Mother OS...");

            // Set any boot time config that the modules need.
            SetBootTimeConfig();

            // Register the core commands that are not associated with a module.
            RegisterCoreCommands();

            // Enqueue the master boot coroutine which controls state end-to-end
            GetModule<Clock>().AddCoroutine(BootSequence());
        }

        /// <summary>
        /// Master boot sequence controlled by Boot(). Runs module boots sequentially,
        /// then flips state to WORKING and performs post-boot actions.
        /// </summary>
        IEnumerable<double> BootSequence()
        {
            // 1) Boot all modules sequentially (each may itself be multi-tick)
            foreach (var t in BootModulesCoroutine())
                yield return t;

            // 2) Now the system is fully booted, flip state and announce
            SetState(SystemStates.WORKING);

            Print("Mother OS is online.");
            Print("Clearing console in 2 seconds...");
            Print("The Empire must grow.");

            // 3) Post-boot cleanup after a short delay
            GetModule<Clock>().QueueForLater(() => GetModule<Terminal>()?.ClearConsole(), 2.0);
        }

        IEnumerable<double> BootModulesCoroutine()
        {
            int total = ModulesInOrderOfRegistration.Count;
            for (int i = 0; i < total; i++)
            {
                var module = ModulesInOrderOfRegistration[i];

                Print($"Booting modules: ({i + 1} / {total})");

                var boot = module.BootCoroutine();

                while (boot.MoveNext())
                    yield return boot.Current;

                RegisterCommands(module.GetCommands());
            }

            Print("All modules booted.");
        }

        /// <summary>
        /// Set the boot time configuration. This is used to set up additional properties on 
        /// Mother during boot.
        /// </summary>
        void SetBootTimeConfig()
        {
            double SAFE_RADIUS = 50;
            SafeZone = new BoundingSphereD(CubeGrid.WorldVolume.Center, CubeGrid.WorldVolume.Radius + SAFE_RADIUS);
        }

        /// <summary>
        /// Register the core commands that are not associated with a module.
        /// </summary>
        void RegisterCoreCommands()
        {
            RegisterCommand(new PurgeCommand(this));
            RegisterCommand(new BootCommand(this));
        }

        /// <summary>
        /// Run the system. This is called on every update cycle or when triggered 
        /// by an update source like a terminal command, or incoming message. 
        /// </summary>
        /// <param name="argument"></param>
        /// <param name="updateType"></param>
        public void Run(string argument, UpdateType updateType)
        {

            //string state = "";

            //switch (SystemState)
            //{
            //    case SystemStates.UNINITIALIZED:
            //        state = "Uninitialized";
            //        break;
            //    case SystemStates.BOOT:
            //        state = "Booting";
            //        break;
            //    case SystemStates.WORKING:
            //        state = "Working";
            //        break;
            //}

            // If the system is uninitialized, we initialize it.
            if (SystemState == SystemStates.UNINITIALIZED)
                Boot();

            // Otherwise the system is booting
            else if (SystemState == SystemStates.BOOT)
            {
                GetModule<Clock>().Run();

                //GetModule<Terminal>()?.Highlight($"State: {state}");

                // update terminal during boot
                GetModule<Terminal>()?.UpdateTerminal();
            }

            // Or, the system is working correctly.
            else if (SystemState == SystemStates.WORKING)
            {
                // Tf the update source is a player action or programmable blocks script
                if ((updateType & (UpdateType.Trigger | UpdateType.Terminal | UpdateType.Script)) != 0)
                    GetModule<CommandBus>().RunTerminalCommand(argument);

                // If the update source is the intergrid communication system,
                // we process the incoming communications.
                else if (updateType == UpdateType.IGC)
                    GetModule<IntergridMessageService>().HandleIncomingIGCMessages();

                // Otherwise we run all modules and assume a runtime update.
                else
                {
                    RunModules();
                    OtherRuntimeItems();
                }

                GetModule<Terminal>().UpdateTerminal();
            }
        }

        /// <summary>
        /// Run all modules. This is called on every program cycle.
        /// </summary>
        void RunModules() => AllModules.Values.ToList().ForEach(module => module.Run());

        /// <summary>
        /// Placeholder for other runtime items. This is useful for producing system 
        /// output during runtime.
        /// </summary>
        void OtherRuntimeItems()
        {
            // get list of local almanac records if local
            //var localRecords = GetModule<Almanac>().Records.FindAll(record => record.IsLocalEntity());
            //var neutralRecords = GetModule<Almanac>().Records.FindAll(record => record.IsNeutral());
            //var friendlyRecords = GetModule<Almanac>().Records.FindAll(record => record.IsFriendly());

            //Terminal terminal  = GetModule<Terminal>();

            //terminal.Highlight($"Local Entities: {localRecords.Count}");
            //terminal.Highlight($"Neutral Entities: {neutralRecords.Count}");
            //terminal.Highlight($"Friendly Entities: {friendlyRecords.Count}");


            //// flatten the list of AlmanacRecord.Channels and count occurances of each isntance
            //var channelCounts = new Dictionary<string, int>();
            //foreach (var record in GetModule<Almanac>().Records)
            //{
            //    foreach (var channel in record.Channels)
            //    {
            //        if (channelCounts.ContainsKey(channel))
            //            channelCounts[channel]++;
            //        else
            //            channelCounts[channel] = 1;
            //    }
            //}

            //// Print the channel counts
            //string channelCountDetails = string.Join("\n", channelCounts.Select(c => $"{c.Key}: {c.Value}"));
            //terminal.Highlight($"Channel Counts:\n{channelCountDetails}");
        }

        /// <summary>
        /// UNUSED
        /// 
        /// Set the update frequency of the program. This allows the player to determine 
        /// the program speed. This is currently unused and is subject to ongoing 
        /// experimentation.
        /// </summary>
        //public void SetUpdateFrequency()
        //{
        //    //Runtime.UpdateFrequency = Configuration.Get("general.update_frequency") == "10" ? UpdateFrequency.Update10 : UpdateFrequency.Update100;
        //}

        /// <summary>
        /// Save the storage string to the Program's Storage property. We do this to 
        /// persist data across program cycles and recompiles.
        /// </summary>
        public string Save() => GetModule<LocalStorage>()?.GetSaveData();

        /// <summary>
        /// Register an extension module with Mother.
        /// </summary>
        /// <param name="module"></param>
        public void RegisterModule(IExtensionModule module)
        {
            ExtensionModules[module.GetModuleName()] = module;

            AllModules[module.GetModuleName()] = module;

            ModulesInOrderOfRegistration.Add(module);
        }

        /// <summary>
        /// Register multiple extension modules with Mother.
        /// </summary>
        /// <param name="modules"></param>
        public void RegisterModules(List<IExtensionModule> modules)
        {
            modules.ForEach(module => RegisterModule(module));
        }

        /// <summary>
        /// Get the name of a module by its type.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public string GetModuleName<T>() where T : IModule
        {
            foreach (var entry in AllModules)
                if (entry.Value is T)
                    return entry.Key;

            return typeof(T).Name; // fallback
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T GetModule<T>() where T : class, IModule
        {
            var moduleName = GetModuleName<T>();
            IModule module;

            if (AllModules.TryGetValue(moduleName, out module))
                return module as T;

            return null;
        }

        /// <summary>
        /// Register a core module with Mother.
        /// </summary>
        /// <param name="module"></param>
        /// <returns></returns>
        ICoreModule RegisterCoreModule(ICoreModule module)
        {
            CoreModules[module.GetModuleName()] = module;
            AllModules[module.GetModuleName()] = module;
            ModulesInOrderOfRegistration.Add(module);

            return module;
        }

        /// <summary>
        /// Register a command with Mother.
        /// Accessor for CommandBus.RegisterCommand()
        /// </summary>
        /// <param name="command"></param>
        public void RegisterCommand(IModuleCommand command)
        {
            GetModule<CommandBus>().RegisterCommand(command);
        }

        /// <summary>
        /// Register multiple commands with Mother.
        /// </summary>
        /// <param name="commands"></param>
        public void RegisterCommands(List<IModuleCommand> commands)
        {
            commands.ForEach(command => RegisterCommand(command));
        }

        /// <summary>
        /// Wait for a specified number of seconds before executing an action.
        /// Proxy for Clock.QueueForLater
        /// </summary>
        /// <param name="action"></param>
        /// <param name="seconds"></param>
        public void Wait(Action action, double seconds)
        {
            GetModule<Clock>().QueueForLater(action, seconds);
        }

        /// <summary>
        /// Print a message to the terminal. If Mother has not booted, we 
        /// use the Program instance directly for printing.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="trim"></param>
        public void Print(string message, bool trim = true)
        {
            Terminal terminal = GetModule<Terminal>();

            // Use default echo if Terminal module is not available
            if (terminal == null)
                Program.Echo(message);

            else
                terminal.Print(message, trim);
        }

        /// <summary>
        /// Get the gravity vector of the ship from artificial gravity 
        /// or natural gravity.
        /// </summary>
        /// <returns></returns>
        public Vector3D GetGravity()
        {
            Vector3D gravity = RemoteControl.GetArtificialGravity();

            // No artificial gravity detected
            if (gravity.LengthSquared() == 0)
                gravity = RemoteControl.GetNaturalGravity();

            return gravity;
        }
    }
}
