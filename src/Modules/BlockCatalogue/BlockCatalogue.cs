using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.GameSystems;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Drawing.Printing;
using System.Linq;
using System.Runtime.CompilerServices;
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
    /// The BlockCatalogue module manages blocks and block groups. Mother 
    /// uses is to track all blocks on the grid, their state changes, 
    /// configurations, and hooks.
    /// </summary>
    public class BlockCatalogue : BaseCoreModule
    {
        /// <summary>
        /// The Clock core module.
        /// </summary>
        Clock Clock;

        /// <summary>
        /// The Configuration core module.
        /// </summary>
        Configuration Configuration;

        /// <summary>
        /// The EventBus core module.
        /// </summary>
        EventBus EventBus;

        /// <summary>
        /// Set of local grid IDs. This includes the main grid and any grids connected 
        /// via rotors, hinges and pistons to represent a single "construct".
        /// </summary>
        public HashSet<long> LocalGridIds = new HashSet<long>();

        /// <summary>
        /// Reference to the primary Remote Control block.
        /// </summary>
        public IMyRemoteControl PrimaryRemoteControlBlock;

        /// <summary>
        /// List of block groups on the current grid.
        /// </summary>
        readonly List<IMyBlockGroup> BlockGroups = new List<IMyBlockGroup>();

        /// <summary>
        /// List of all Terminal Blocks on the grid.
        /// </summary>
        public List<IMyTerminalBlock> TerminalBlocks  = new List<IMyTerminalBlock>();

        /// <summary>
        /// Dictionary of all block configurations on the grid.  This allows 
        /// us to access block-level custom data.
        /// </summary>
        public readonly Dictionary<IMyTerminalBlock, MyIni> BlockConfigs = new Dictionary<IMyTerminalBlock, MyIni>();

        /// <summary>
        /// Dictionary of block tags.  This allows us to group and target blocks by tags.
        /// </summary>
        public readonly Dictionary<string, HashSet<IMyTerminalBlock>> BlockTags = new Dictionary<string, HashSet<IMyTerminalBlock>>();

        /// <summary>
        /// Dictionary of block hooks.  This allows us to register hooks for blocks.
        /// </summary>
        readonly Dictionary<IMyTerminalBlock, Dictionary<string, string>> BlockHooks = new Dictionary<IMyTerminalBlock, Dictionary<string, string>>();

        /// <summary>
        /// Dictionary to store block states. This is used to track state changes for blocks.
        /// </summary>
        readonly Dictionary<long, object> BlockStates = new Dictionary<long, object>();

        /// <summary>
        /// Dictionary to store blocks to monitor and their corresponding state handlers.
        /// </summary>
        readonly Dictionary<IMyTerminalBlock, IBlockStateHandler> BlocksToMonitor = new Dictionary<IMyTerminalBlock, IBlockStateHandler>();

        /// <summary>
        /// The current index for processing blocks in each program cycle.
        /// </summary>
        int CurrentIndex = 0;

        /// <summary>
        /// The number of blocks to process in each program cycle.
        /// </summary>
        const int BLOCKS_PER_CYCLE = 50;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="mother"></param>
        public BlockCatalogue(Mother mother) : base(mother)
        {
            Mother = mother;
        }

        /// <summary>
        /// Boot the module. We determine all grids that are part of the main 
        /// construct, load the blocks on the grids and subscribe to events.
        /// </summary>
        public override void Boot()
        {
            // Modules
            Configuration = Mother.GetModule<Configuration>();
            EventBus = Mother.GetModule<EventBus>();
            Clock = Mother.GetModule<Clock>();

            // Load construct and blocks
            LoadLocalGridIds(Mother.CubeGrid);
            LoadBlocks();

            // Events
            SubscribeToEvents();

            // Start automatic block configuration refresh
            InitiateBlockConfigurationRefresh();
        }

        /// <summary>
        /// Initiate a block configuration refresh routine. This will reload block configurations
        /// on a consistent interval so respond to player changes.
        /// </summary>
        void InitiateBlockConfigurationRefresh()
        {
            Clock.AddCoroutine(GetRefreshBlockConfigurationRoutine());
        }

        /// <summary>
        /// Coroutine to refresh block configurations. This will run every second by queueing itself.
        /// </summary>
        /// <returns></returns>
        IEnumerable<double> GetRefreshBlockConfigurationRoutine()
        {
            LoadBlockConfigurations();

            Clock.AddCoroutine(GetRefreshBlockConfigurationRoutine(), 1);

            yield return 0;
        }

        /// <summary>
        /// Load all Terminal Blocks on the grid. We load block groups, 
        /// and register block configuration and hooks.
        /// </summary>
        public void LoadBlocks()
        {
            // Clear caches
            BlockTags.Clear();
            BlockHooks.Clear();
            BlockConfigs.Clear();

            // Load all IMyTerminalBlock blocks from the grid terminal system.
            GetBlocksFromGridTerminalSystem(TerminalBlocks);

            // Load remote control - we should improve this implementation later. 

            LoadRemoteControlBlock();

            LoadBlockConfigurations();

            RegisterBlockHooksFromProgrammableBlockConfiguration();

            LoadBlockGroups();
        }

        /// <summary>
        /// Subscribe to events that are relevant for this module.
        /// </summary>
        void SubscribeToEvents()
        {
            EventBus.Subscribe<ConnectorLockedEvent>(this);
            EventBus.Subscribe<ConnectorUnlockedEvent>(this);
            EventBus.Subscribe<MergeBlockLockedEvent>(this);
            EventBus.Subscribe<MergeBlockOffEvent>(this);
        }

        /// <summary>
        /// Run the module. This method is called every program cycle. 
        /// We check for changes in block state.
        /// </summary>
        public override void Run()
        {
            CheckBlocksForStateChange();
        }

        /// <summary>
        /// Check the state of blocks that are registered for monitoring. We use a custom 
        /// handler as each block type has a different implementation of state.
        /// </summary>
        /// <see href="https://github.com/malware-dev/MDK-SE/wiki/Sandbox.ModAPI.Ingame.DoorStatus"/>
        /// <see href="https://github.com/malware-dev/MDK-SE/wiki/Sandbox.ModAPI.Ingame.ChargeMode"/>
        /// <see href="https://github.com/malware-dev/MDK-SE/wiki/Sandbox.ModAPI.Ingame.MyShipConnectorStatus"/>
        void CheckBlocksForStateChange()
        {
            var blockKeys = BlocksToMonitor.Keys.ToList();
            int totalBlocks = blockKeys.Count;

            var blocksToProcess = blockKeys
                .Skip(CurrentIndex)
                .Take(BLOCKS_PER_CYCLE)
                .ToList();

            foreach (var block in blocksToProcess)
            {
                if (!BlocksToMonitor.ContainsKey(block)) continue; // Skip if block is not registered

                IBlockStateHandler handler = BlocksToMonitor[block];

                object previousState = BlockStates.ContainsKey(block.EntityId) ? BlockStates[block.EntityId] : null;
                object currentState = handler.GetBlockCurrentState(block);

                if (handler.HasBlockStateChanged(block, previousState))
                {
                    handler.OnBlockStateChanged(block);

                    BlockStates[block.EntityId] = currentState; // Update stored state
                }
            }

            CurrentIndex += BLOCKS_PER_CYCLE;

            if (CurrentIndex >= totalBlocks)
                CurrentIndex = 0;
        }

        /// <summary>
        /// Handle events that are sent to the module. If a connector status 
        /// changes, we should reload block groups as the CubeGrid Terminal 
        /// System has changed.
        /// </summary>
        /// <param name="e"></param>
        /// <param name="eventData"></param>
        public override void HandleEvent(IEvent e, object eventData)
        {
            if (e is ConnectorLockedEvent || e is ConnectorUnlockedEvent)
                LoadBlockGroups();
        }

        /// <summary>
        /// Set a tag on a specific block. This allows us to group blocks easily with a tag.
        /// </summary>
        /// <param name="block"></param>
        /// <param name="tag"></param>
        /// <returns></returns>
        public IMyTerminalBlock SetBlockWithTag(IMyTerminalBlock block, string tag)
        {
            MyIni blockConfiguration = GetBlockConfiguration(block);

            // if tag not in the block's tags
            string tagsValue = $"{blockConfiguration.Get("general", "tags")}";

            if (tagsValue == "")
                tagsValue = tag;

            else if (!tagsValue.Contains(tag))
                tagsValue += $",{tag}";

            blockConfiguration.Set("general", "tags", tagsValue);
            block.CustomData = blockConfiguration.ToString();

            if (!BlockTags.ContainsKey(tag))
                BlockTags[tag] = new HashSet<IMyTerminalBlock>();

            BlockTags[tag].Add(block);

            return block;
        }

        /// <summary>
        /// Register a block for state monitoring. This allows us to monitor changes 
        /// in the block's state with a custom handler to support any block type.
        /// </summary>
        /// <param name="block"></param>
        /// <param name="handler"></param>
        public void RegisterBlockForStateMonitoring(IMyTerminalBlock block, IBlockStateHandler handler)
        {
            BlocksToMonitor[block] = handler;

            // Store initial state
            BlockStates[block.EntityId] = handler.GetBlockCurrentState(block);
        }

        /// <summary>
        /// Get blocks of a specific type from the construct from the grid terminal system. We 
        /// only call this during boot, and otherwise use TerminalBlocks as our block lookup
        /// during runtime.Blocks may be filtered via an option parameter. 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="blocks"></param>
        /// <param name="filter"></param>
        void GetBlocksFromGridTerminalSystem<T>(List<T> blocks, Func<T, bool> filter = null) where T : class, IMyTerminalBlock
        {
            Mother.GridTerminalSystem.GetBlocksOfType(blocks, block =>
            {
                return LocalGridIds.Contains(block.CubeGrid.EntityId) && (filter == null || filter(block));
            });
        }

        /// <summary>
        /// Get blocks of a specific type from the construct.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="filter"></param>
        public List<T> GetBlocks<T>(Func<T, bool> filter = null) where T : class, IMyTerminalBlock
        {
            List<T> filteredBlocks = TerminalBlocks
                .OfType<T>()
                .Where(block => filter == null || filter(block))
                .ToList();

            return filteredBlocks;
        }

        bool HasBlockConfigurationChanged(IMyTerminalBlock block, MyIni newConfiguration)
        {
            return BlockConfigs.ContainsKey(block)
                    && newConfiguration.ToString() != BlockConfigs[block].ToString();
        }

        bool BlockIsMother(IMyTerminalBlock block)
        {
            return block is IMyProgrammableBlock && block.EntityId == Mother.Id;
        }

        /// <summary>
        /// Load block configurations from the custom data of each block. We do 
        /// this to access hooks and other block specific configurations.
        /// </summary>
        void LoadBlockConfigurations()
        {
            foreach (var block in TerminalBlocks)
            {
                MyIni blockConfiguration = new MyIni();
                MyIniParseResult result;

                if (!blockConfiguration.TryParse(block.CustomData, out result))
                    continue;

                if(Mother.SystemState == Mother.SystemStates.BOOT)
                    BlockConfigs[block] = blockConfiguration;

                // check for change here    
                else if
                (
                    Mother.SystemState == Mother.SystemStates.WORKING
                    && HasBlockConfigurationChanged(block, blockConfiguration)
                )
                {
                    // if this is the programmable block, we reboot Mother.
                    if (block is IMyProgrammableBlock && block.EntityId == Mother.Id)
                    {
                        Mother.Print("Mother configuration changed.\nRebooting...");
                        Mother.Boot();
                        return;
                    }

                    // update the block config
                    BlockConfigs[block] = blockConfiguration;

                    // Emit event for other modules to use
                    Emit<BlockConfigChangedEvent>(block);

                    // Notify player of update
                    Mother.Print($"Config changed: {block.CustomName}", false);
                }

                // if result is empty, set default configuration values
                if (blockConfiguration.ToString() == "")
                    SetDefaultConfiguration(block, blockConfiguration);

                // Load block tags
                if (blockConfiguration.ContainsSection("general"))
                    LoadBlockTags(block, blockConfiguration);

                // Load block hooks
                if (blockConfiguration.ContainsSection("hooks"))
                    LoadBlockHooks(block, blockConfiguration);
            }
        }

        /// <summary>
        /// Set default configuration values for a block. This is used when a block 
        /// does not contain any configuration. Mother will prefill common programmableBlockConfig 
        /// items on first boot.
        /// </summary>
        /// <param name="block"></param>
        /// <param name="blockConfiguration"></param>
        void SetDefaultConfiguration(IMyTerminalBlock block, MyIni blockConfiguration)
        {
            foreach (var key in Configuration.BlockConfigDefaults.Keys)
            {
                // split hookName by '.' for section and hookName
                string[] keyParts = key.Split('.');
                blockConfiguration.Set(keyParts[0], keyParts[1], Configuration.BlockConfigDefaults[key]);
            }
            block.CustomData = blockConfiguration.ToString();
        }

        /// <summary>
        /// Load block tags from the block's custom data. This allows us to group 
        /// blocks without using the in-game Terminal.
        /// </summary>
        /// <param name="block"></param>
        /// <param name="blockConfiguration"></param>
        void LoadBlockTags(IMyTerminalBlock block, MyIni blockConfiguration)
        {
            string tagsValue = $"{blockConfiguration.Get("general", "tags")}";

            if (tagsValue == "") return;

            foreach (var tag in tagsValue.Split(','))
            {
                string cleanTag = tag.Trim();

                if (!BlockTags.ContainsKey(cleanTag))
                    BlockTags[cleanTag] = new HashSet<IMyTerminalBlock>();

                BlockTags[cleanTag].Add(block);
            }
        }

        /// <summary>
        /// Load block hooks from the block's custom data. This allows us to register
        /// </summary>
        /// <param name="block"></param>
        /// <param name="blockConfiguration"></param>
        void LoadBlockHooks(IMyTerminalBlock block, MyIni blockConfiguration)
        {
            var hooks = new Dictionary<string, string>();

            List<MyIniKey> keys = new List<MyIniKey>();
            blockConfiguration.GetKeys("hooks", keys);

            foreach (var hookName in keys)
            {
                string hookValue = $"{blockConfiguration.Get("hooks", hookName.Name)}";
                string simplifiedValue = ReplaceThisKeywordWithBlockName(block, hookValue);

                hooks[hookName.Name] = simplifiedValue;
            }

            BlockHooks[block] = hooks;
        }

        /// <summary>
        /// Replace the "this" keyword in the hook value with the block's name.
        /// </summary>
        /// <param name="block"></param>
        /// <param name="hookValue"></param>
        /// <returns></returns>
        string ReplaceThisKeywordWithBlockName(IMyTerminalBlock block, string hookValue)
        {
            // Create a new string to store the result
            StringBuilder modifiedValue = new StringBuilder();
            int startIndex = 0;

            // Iterate through the string and manually replace "this" when appropriate
            while (startIndex < hookValue.Length)
            {
                int index = hookValue.IndexOf("this", startIndex);

                if (index == -1)
                {
                    // No more "this" found, append the rest of the string
                    modifiedValue.Append(hookValue.Substring(startIndex));
                    break;
                }

                // Check if "this" is preceded by a space and followed by a space or semicolon
                if (index > 0 && hookValue[index - 1] == ' ' &&
                    (index + 4 == hookValue.Length || hookValue[index + 4] == ' ' || hookValue[index + 4] == ';'))
                {
                    // Append text before "this" and the replacement
                    modifiedValue.Append(hookValue.Substring(startIndex, index - startIndex));
                    modifiedValue.Append($"\"{block.CustomName}\"");
                }
                else
                {
                    // Append "this" as is if the condition isn't met
                    modifiedValue.Append(hookValue.Substring(startIndex, index - startIndex + 4));
                }

                // Move the startIndex past the current "this"
                startIndex = index + 4;
            }

            return $"{modifiedValue}";
        }

        /// <summary>
        /// Register block hooks from the Programmable Block configuration. This allows 
        /// us to define hooks for blocks via Mother's Programmable Block custom data 
        /// to centralization logic.
        /// </summary>
        void RegisterBlockHooksFromProgrammableBlockConfiguration()
        {
            MyIni programmableBlockConfig = Configuration.Ini;

            List<MyIniKey> keys = new List<MyIniKey>();
            programmableBlockConfig.GetKeys("hooks", keys);

            // print each hookName in the "hooks" section
            foreach (var key in keys)
            {
                // if hookName contains a '.' character, it is a block hook and we need
                // to split into [block name], [hook name]
                if (key.Name.Contains("."))
                {
                    // get names and remove quotes from block name
                    string[] keyParts = key.Name.Split('.');
                    string blockName = keyParts[0].Trim('\"');
                    string hookName = keyParts[1];

                    foreach (var block in GetBlocksByName<IMyTerminalBlock>(blockName))
                    {
                        // create new dictionary for block if it doesn't exist
                        if (!BlockHooks.ContainsKey(block))
                            BlockHooks[block] = new Dictionary<string, string>();

                        BlockHooks[block][hookName] = programmableBlockConfig.Get("hooks", key.Name).ToString();
                    }
                }
            }

        }

        /// <summary>
        /// Run a hook for a block. Hooks are fired when a block's state changes. 
        /// They are fired based on the block type and can be defined in a 
        /// block's custom data.
        /// </summary>
        /// <param name="block"></param>
        /// <param name="hookName"></param>
        public void RunHook(IMyTerminalBlock block, string hookName)
        {
            if (BlockHooks.ContainsKey(block) && BlockHooks[block].ContainsKey(hookName))
            {
                string hookAction = BlockHooks[block][hookName];

                Mother.GetModule<CommandBus>().RunTerminalCommand(hookAction);
            }
        }

        /// <summary>
        /// Get the block configuration for a specific block. If the block does 
        /// not have config, we return an empty configuration instance.
        /// </summary>
        /// <param name="block"></param>
        /// <returns></returns>
        public MyIni GetBlockConfiguration(IMyTerminalBlock block)
        {
            return BlockConfigs.ContainsKey(block) 
                ? BlockConfigs[block] 
                : new MyIni();
        }

        /// <summary>
        /// Get all blocks of a specific type based on a name. We can use the 
        /// block's CustomName property, a block group name, or a tag.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="name"></param>
        /// <returns></returns>
        public new List<T> GetBlocksByName<T>(string name) where T : class, IMyTerminalBlock
        {
            List<T> blocks = new List<T>();

            // Try to get block groups with the given Name
            List<IMyBlockGroup> groups = GetBlockGroups(name);

            // We are targeting a group
            if (groups.Count > 0)
            {
                foreach (var group in groups)
                {
                    List<T> groupBlocks = new List<T>();
                    group.GetBlocksOfType(groupBlocks);

                    // Include only blocks from valid grids
                    blocks.AddRange(groupBlocks.Where(block => LocalGridIds.Contains(block.CubeGrid.EntityId)));
                }
            }

            // we are targeting a tag
            else if (name.StartsWith("#"))
            {
                if(BlockTags.ContainsKey(name.Substring(1)))
                {
                    BlockTags[name.Substring(1)]?.ToList().ForEach(block =>
                    {
                        if (block is T && LocalGridIds.Contains(block.CubeGrid.EntityId))
                            blocks.Add(block as T);
                    });
                } 
                else
                {
                    Mother.Print($"Tag not found: {name}");
                    return blocks;
                }
            }

            // we are targeting a specific block
            else
            {
                T block = GetBlock(name) as T;

                // Add the block if it exists and is on the local grid
                if (block != null && LocalGridIds.Contains(block.CubeGrid.EntityId))
                    blocks.Add(block);
            }

            return blocks;
        }

        /// <summary>
        /// Load all block groups from the grid. We run this on boot, and when a grid 
        /// connects/ disconnects with another grid. If we do not do this, Mother 
        /// will target blocks on other grids when connected, and will be unable 
        /// to find groups after a disconnect.
        /// </summary>
        public void LoadBlockGroups()
        {
            BlockGroups.Clear();
            Mother.GridTerminalSystem.GetBlockGroups(BlockGroups);
        }

        /// <summary>
        /// Get all block groups with a specific name.
        /// </summary>
        /// <param name="groupName"></param>
        /// <returns></returns>
        List<IMyBlockGroup> GetBlockGroups(string groupName)
        {
            return BlockGroups
                .Where(group => string.Equals(group.Name, groupName, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        /// <summary>
        /// Get a block by its name. Only the display name is used for this method.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        IMyTerminalBlock GetBlock(string name) => TerminalBlocks.FirstOrDefault(x => x.DisplayNameText == name);

        /// <summary>
        /// Load the primary remote control block. We select a primary for 
        /// use with autopilot and navigation.
        /// </summary>
        void LoadRemoteControlBlock()
        {
            List<IMyRemoteControl> blocks = TerminalBlocks
                .OfType<IMyRemoteControl>()
                .ToList();

            if (blocks.Count == 0)
                throw new Exception("\n\nNo remote control block found. Add one to your grid and press 'Recompile'.\n");

            PrimaryRemoteControlBlock = blocks[0];

            // reset params
            PrimaryRemoteControlBlock.ClearWaypoints();
            PrimaryRemoteControlBlock.SetAutoPilotEnabled(false);

            // Hoist to Mother
            //
            // We should refactor this someday.  Ideally, Mother is setting this, or making it more widely
            // available to modules. It could be logically grouped with other position and motion
            // getters.  This class should not change a value on Mother directly.
            // Use Mother method as last resort.
            Mother.RemoteControl = PrimaryRemoteControlBlock;
   
        }

        /// <summary>
        /// Load local grid IDs. This includes the main grid and any grids connected via a hinge, 
        /// rotor or piston.  We do this to ensure our grid does not target blocks on other 
        /// grids when connected via a connector. This method circumvents the 
        /// IMyCubeBlock.IsSameConstructAs() method to reduced complexity.
        /// </summary>
        /// <param name="startingGrid"></param>
        void LoadLocalGridIds(IMyCubeGrid startingGrid)
        {
            HashSet<long> localGridIds = new HashSet<long>();
            Queue<IMyCubeGrid> gridsToCheck = new Queue<IMyCubeGrid>();

            // StartAutopilot with the main grid
            gridsToCheck.Enqueue(startingGrid);

            while (gridsToCheck.Count > 0)
            {
                IMyCubeGrid currentGrid = gridsToCheck.Dequeue();

                // Skip if already visited
                if (localGridIds.Contains(currentGrid.EntityId)) continue;

                // Mark this grid as visited
                localGridIds.Add(currentGrid.EntityId);

                // connection blocks
                List<IMyMechanicalConnectionBlock> connectionBlocks = new List<IMyMechanicalConnectionBlock>();

                Mother.GridTerminalSystem.GetBlocksOfType(
                    connectionBlocks, 
                    block => block.CubeGrid == currentGrid || block.TopGrid == currentGrid
                );

                foreach (var block in connectionBlocks)
                {
                    // Add the connected grid to the queue
                    if (block.TopGrid != null && !localGridIds.Contains(block.TopGrid.EntityId))
                        gridsToCheck.Enqueue(block.TopGrid);

                    // Add the current grid to the queue
                    else if (block.CubeGrid != null && !localGridIds.Contains(block.CubeGrid.EntityId))
                        gridsToCheck.Enqueue(block.CubeGrid);
                }
            }

            LocalGridIds = localGridIds;
        }
    }
}
