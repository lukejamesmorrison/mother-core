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
        /// Set of grid IDs on this construct. This includes the main grid and any grids connected 
        /// via rotors, hinges and pistons to represent a single "construct".
        /// </summary>
        public HashSet<long> ConstructGridIds = new HashSet<long>();

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
        /// The name of the general configuration section.
        /// </summary>
        const string SECTION_GENERAL = "general";

        /// <summary>
        /// The key for block tags in the a block configuration.
        /// </summary>
        const string KEY_TAGS = "tags";

        /// <summary>
        /// The name of the hooks configuration section.
        /// </summary>
        const string SECTION_HOOKS = "hooks";

        /// <summary>
        /// Flag to indicate if a construct refresh is pending.
        /// </summary>
        bool _constructRefreshPending = false;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="mother"></param>
        public BlockCatalogue(Mother mother) : base(mother)
        {
            Mother = mother;
        }

        /// <summary>
        /// Boot the module over multiple cycles. We discover all constructs 
        /// connected via mechanical blocks and store their configuration.
        /// </summary>
        public override IEnumerator<double> BootCoroutine()
        {
            // Run standard boot.
            Boot();

            // Load blocks across construct.
            foreach (var t in DiscoverConstructAndLoadBlocksCoroutine(Mother.CubeGrid))
                yield return t;

            // Register block hooks.
            RegisterBlockHooksFromProgrammableBlockConfiguration();

            // Initiate auto-refresh of block configurations.
            Clock.AddCoroutine(GetRefreshBlockConfigurationRoutine());

            yield break;
        }

        /// <summary>
        /// Boot the module. We registered useful modules and subscribe to events.
        /// </summary>
        public override void Boot()
        {
            // Modules
            Configuration = Mother.GetModule<Configuration>();
            EventBus = Mother.GetModule<EventBus>();
            Clock = Mother.GetModule<Clock>();

            // Events
            EventBus.Subscribe<ConnectorLockedEvent>(this);
            EventBus.Subscribe<ConnectorUnlockedEvent>(this);
            EventBus.Subscribe<MergeBlockLockedEvent>(this);
            EventBus.Subscribe<MergeBlockOffEvent>(this);
            EventBus.Subscribe<SystemConfigChangedEvent>(this);
            EventBus.Subscribe<MechanicalBlockAttachedEvent>(this);
            EventBus.Subscribe<MechanicalBlockDetachedEvent>(this);
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
                // Skip if block is not registered
                if (!BlocksToMonitor.ContainsKey(block)) continue; 

                IBlockStateHandler handler = BlocksToMonitor[block];

                object previousState = BlockStates.ContainsKey(block.EntityId) ? BlockStates[block.EntityId] : null;
                object currentState = handler.GetBlockCurrentState(block);

                if (handler.HasBlockStateChanged(block, previousState))
                {
                    handler.OnBlockStateChanged(block);

                    BlockStates[block.EntityId] = currentState;
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

            // Handle mechanical block attach/detach events
            if (e is MechanicalBlockAttachedEvent || e is MechanicalBlockDetachedEvent)
                LoadBlockGroups();

            // Handle merge block events - when grids merge/unmerge, the grid topology changes
            // so we need a full construct refresh to update ConstructGridIds and reload blocks
            if (e is MergeBlockLockedEvent || e is MergeBlockOffEvent)
                RefreshConstruct();

            // Handle system config changes - reload block hooks from programmable block config
            if (e is SystemConfigChangedEvent)
                RegisterBlockHooksFromProgrammableBlockConfiguration();
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
            string tagsValue = $"{blockConfiguration.Get(SECTION_GENERAL, KEY_TAGS)}";

            if (tagsValue == "")
                tagsValue = tag;

            else if (!tagsValue.Contains(tag))
                tagsValue += $",{tag}";

            blockConfiguration.Set(SECTION_GENERAL, KEY_TAGS, tagsValue);
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

        //bool BlockIsMother(IMyTerminalBlock block)
        //{
        //    return block is IMyProgrammableBlock && block.EntityId == Mother.Id;
        //}

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
                    // update the block config
                    BlockConfigs[block] = blockConfiguration;

                    // Emit event for other modules to use
                    Emit<BlockConfigChangedEvent>(block);

                    // if this is the programmable block, emit a system config changed event
                    // so that dependent modules can reload their configuration values.
                    if (block.EntityId == Mother.Id)
                        Emit<SystemConfigChangedEvent>(block);

                    // Notify player of update
                    Mother.Print($"Config changed: {block.CustomName}", false);
                }

                // if result is empty, set default configuration values
                if (blockConfiguration.ToString() == "")
                    SetDefaultConfiguration(block, blockConfiguration);

                // Load block tags
                if (blockConfiguration.ContainsSection(SECTION_GENERAL))
                    LoadBlockTags(block, blockConfiguration);

                // Load block hooks
                if (blockConfiguration.ContainsSection(SECTION_HOOKS))
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
            string tagsValue = $"{blockConfiguration.Get(SECTION_GENERAL, KEY_TAGS)}";

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
            blockConfiguration.GetKeys(SECTION_HOOKS, keys);

            foreach (var hookName in keys)
            {
                string hookValue = $"{blockConfiguration.Get(SECTION_HOOKS, hookName.Name)}";
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
            StringBuilder modifiedValue = new StringBuilder();
            int startIndex = 0;

            // Iterate through the string and replace "this" keyword
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
            programmableBlockConfig.GetKeys(SECTION_HOOKS, keys);

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

                        BlockHooks[block][hookName] = programmableBlockConfig.Get(SECTION_HOOKS, key.Name).ToString();
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
                    blocks.AddRange(groupBlocks.Where(block => ConstructGridIds.Contains(block.CubeGrid.EntityId)));
                }
            }

            // we are targeting a tag
            else if (name.StartsWith("#"))
            {
                if(BlockTags.ContainsKey(name.Substring(1)))
                {
                    BlockTags[name.Substring(1)]?.ToList().ForEach(block =>
                    {
                        if (block is T && ConstructGridIds.Contains(block.CubeGrid.EntityId))
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

                // Add the block if it exists and is on the construct
                if (block != null && ConstructGridIds.Contains(block.CubeGrid.EntityId))
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
                throw new Exception("\n\nNo remote control block found. Add one and 'Recompile'.\n");

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
        /// Cache of mechanical blocks for adjacency building.
        /// </summary>
        readonly List<IMyMechanicalConnectionBlock> mechanicalBlocksCache = new List<IMyMechanicalConnectionBlock>();

        /// <summary>
        /// Queue for BFS grid traversal during construct discovery.
        /// </summary>
        readonly Queue<IMyCubeGrid> gridBfsQueue = new Queue<IMyCubeGrid>();

        /// <summary>
        /// Set of visited grid IDs during construct discovery.
        /// </summary>
        readonly HashSet<long> visitedGrids = new HashSet<long>();

        /// <summary>
        /// Adjacency map of grids connected via mechanical blocks.
        /// </summary>
        Dictionary<long, HashSet<long>> gridAdjacencyMap = new Dictionary<long, HashSet<long>>();

        /// <summary>
        /// Number of grids to traverse per tick when discovering the construct.
        /// Grids are connected via mechanical blocks (rotor, hinge, piston).
        /// </summary>
        const int GRIDS_PER_TICK = 40;

        /// <summary>
        /// Number of blocks to parse per tick when loading block configurations.
        /// </summary>
        const int BLOCKS_PER_TICK_LOAD = 500;

        /// <summary>
        /// Coroutine to discover all grids in the construct and load blocks in batches.
        /// </summary>
        /// <param name="start"></param>
        /// <returns></returns>
        IEnumerable<double> DiscoverConstructAndLoadBlocksCoroutine(IMyCubeGrid start)
        {
            // Build adjacency from mechanical blocks
            BuildMechanicalAdjacency();

            // BFS the construct in batches
            gridBfsQueue.Clear();
            visitedGrids.Clear();
            ConstructGridIds.Clear();

            gridBfsQueue.Enqueue(start);

            while (gridBfsQueue.Count > 0)
            {
                int processed = 0;

                while (gridBfsQueue.Count > 0 && processed < GRIDS_PER_TICK)
                {
                    var g = gridBfsQueue.Dequeue();
                    long gid = g.EntityId;

                    if (visitedGrids.Contains(gid)) { processed++; continue; }

                    visitedGrids.Add(gid);
                    ConstructGridIds.Add(gid);

                    HashSet<long> neighbors;

                    if (gridAdjacencyMap.TryGetValue(gid, out neighbors))
                    {
                        foreach (long nid in neighbors)
                        {
                            var nGrid = TryGetGridFromId(nid);
                            if (nGrid != null && !visitedGrids.Contains(nid))
                                gridBfsQueue.Enqueue(nGrid);
                        }
                    }

                    processed++;
                }

                yield return 0;
            }

            foreach (var t in LoadBlocksCoroutine())
                yield return t;
        }

        /// <summary>
        /// Build mechanical adjacency map from all mechanical blocks on the main 
        /// grid. This allows us to traverse connected grids efficiently.
        /// </summary>
        void BuildMechanicalAdjacency()
        {
            mechanicalBlocksCache.Clear();
            gridAdjacencyMap.Clear();

            Mother.GridTerminalSystem.GetBlocksOfType(mechanicalBlocksCache);

            for (int i = 0; i < mechanicalBlocksCache.Count; i++)
            {
                var m = mechanicalBlocksCache[i];
                var a = m.CubeGrid;
                var b = m.TopGrid;

                if (a == null || b == null) continue;

                long aid = a.EntityId, bid = b.EntityId;

                HashSet<long> al;
                if (!gridAdjacencyMap.TryGetValue(aid, out al))
                    gridAdjacencyMap[aid] = al = new HashSet<long>();

                al.Add(bid);

                HashSet<long> bl;
                if (!gridAdjacencyMap.TryGetValue(bid, out bl))
                    gridAdjacencyMap[bid] = bl = new HashSet<long>();

                bl.Add(aid);
            }
        }

        /// <summary>
        /// Try to get a grid from its entity ID by searching mechanical blocks cache.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        IMyCubeGrid TryGetGridFromId(long id)
        {
            for (int i = 0; i < mechanicalBlocksCache.Count; i++)
            {
                var b = mechanicalBlocksCache[i];

                if (b.CubeGrid != null && b.CubeGrid.EntityId == id) return b.CubeGrid;
                if (b.TopGrid != null && b.TopGrid.EntityId == id) return b.TopGrid;
            }

            return null;
        }

        /// <summary>
        /// Coroutine to load blocks in batches. This allows us to stay under instruction
        /// limits and load entire block catalogue over multiple cycles.
        /// </summary>
        /// <returns></returns>
        IEnumerable<double> LoadBlocksCoroutine()
        {
            BlockTags.Clear();
            BlockHooks.Clear();
            BlockConfigs.Clear();
            TerminalBlocks.Clear();

            var all = new List<IMyTerminalBlock>();

            Mother.GridTerminalSystem.GetBlocks(all);

            // keep only blocks in the construct
            for (int i = 0; i < all.Count; i++)
            {
                var tb = all[i];

                if (ConstructGridIds.Contains(tb.CubeGrid.EntityId))
                    TerminalBlocks.Add(tb);
            }

            LoadRemoteControlBlock();

            // parse configs/tags/hooks
            int index = 0;
            while (index < TerminalBlocks.Count)
            {
                int take = Math.Min(BLOCKS_PER_TICK_LOAD, TerminalBlocks.Count - index);

                for (int i = 0; i < take; i++)
                {
                    var block = TerminalBlocks[index + i];

                    var ini = new MyIni();

                    MyIniParseResult res;

                    if (ini.TryParse(block.CustomData, out res))
                    {
                        BlockConfigs[block] = ini;

                        if (ini.ToString().Length == 0)
                            SetDefaultConfiguration(block, ini);

                        if (ini.ContainsSection(SECTION_GENERAL))
                            LoadBlockTags(block, ini);

                        if (ini.ContainsSection(SECTION_HOOKS))
                            LoadBlockHooks(block, ini);
                    }
                }

                index += take;
                yield return 0;
            }

            LoadBlockGroups();

            yield return 0;
        }

        /// <summary>
        /// Refresh the construct by re-discovering grids and reloading blocks.
        /// This is called when a mechanical block attaches or detaches, which
        /// changes the grids that are part of the construct.
        /// </summary>
        public void RefreshConstruct()
        {
            // Prevent multiple concurrent refreshes
            if (_constructRefreshPending) return;

            _constructRefreshPending = true;

            // Queue the refresh coroutine
            Clock.AddCoroutine(RefreshConstructCoroutine());
        }

        /// <summary>
        /// Handle a mechanical block attachment. Crawl from the newly attached grid
        /// and add only grids not already part of the construct.
        /// </summary>
        /// <param name="attachedGrid">The grid that was just attached via mechanical block.</param>
        public void OnMechanicalBlockAttached(IMyCubeGrid attachedGrid)
        {
            if (attachedGrid == null || _constructRefreshPending) return;

            // If the attached grid is already part of the construct, nothing to do
            if (ConstructGridIds.Contains(attachedGrid.EntityId)) return;

            _constructRefreshPending = true;
            Clock.AddCoroutine(AttachGridsCoroutine(attachedGrid));
        }

        /// <summary>
        /// Handle a mechanical block detachment. Crawl from Mother's grid
        /// and prune all grids no longer connected to the main construct.
        /// </summary>
        public void OnMechanicalBlockDetached()
        {
            if (_constructRefreshPending) return;

            _constructRefreshPending = true;
            Clock.AddCoroutine(DetachGridsCoroutine());
        }

        /// <summary>
        /// Coroutine to attach grids starting from a newly connected grid.
        /// Only adds grids not already part of the construct.
        /// </summary>
        /// <param name="startGrid">The newly attached grid to start crawling from.</param>
        /// <returns></returns>
        IEnumerable<double> AttachGridsCoroutine(IMyCubeGrid startGrid)
        {
            // Rebuild adjacency to include new connections
            BuildMechanicalAdjacency();

            // BFS from the attached grid to find all newly connected grids
            gridBfsQueue.Clear();
            visitedGrids.Clear();

            var newGridIds = new HashSet<long>();
            gridBfsQueue.Enqueue(startGrid);

            while (gridBfsQueue.Count > 0)
            {
                int processed = 0;

                while (gridBfsQueue.Count > 0 && processed < GRIDS_PER_TICK)
                {
                    var g = gridBfsQueue.Dequeue();
                    long gid = g.EntityId;

                    if (visitedGrids.Contains(gid)) { processed++; continue; }

                    visitedGrids.Add(gid);

                    // Only add if not already in construct
                    if (!ConstructGridIds.Contains(gid))
                        newGridIds.Add(gid);

                    HashSet<long> neighbors;

                    if (gridAdjacencyMap.TryGetValue(gid, out neighbors))
                    {
                        foreach (long nid in neighbors)
                        {
                            // Stop crawling at grids already in construct
                            if (ConstructGridIds.Contains(nid)) continue;

                            var nGrid = TryGetGridFromId(nid);
                            if (nGrid != null && !visitedGrids.Contains(nid))
                                gridBfsQueue.Enqueue(nGrid);
                        }
                    }

                    processed++;
                }

                yield return 0;
            }

            // Add new grid IDs to the construct
            foreach (var gid in newGridIds)
                ConstructGridIds.Add(gid);

            // Load blocks from new grids
            if (newGridIds.Count > 0)
            {
                var allBlocks = new List<IMyTerminalBlock>();
                Mother.GridTerminalSystem.GetBlocks(allBlocks);

                var newBlocks = allBlocks
                    .Where(b => newGridIds.Contains(b.CubeGrid.EntityId))
                    .ToList();

                int index = 0;
                while (index < newBlocks.Count)
                {
                    int take = Math.Min(BLOCKS_PER_TICK_LOAD, newBlocks.Count - index);

                    for (int i = 0; i < take; i++)
                    {
                        var block = newBlocks[index + i];
                        TerminalBlocks.Add(block);

                        var ini = new MyIni();
                        MyIniParseResult res;

                        if (ini.TryParse(block.CustomData, out res))
                        {
                            BlockConfigs[block] = ini;

                            if (ini.ToString().Length == 0)
                                SetDefaultConfiguration(block, ini);

                            if (ini.ContainsSection(SECTION_GENERAL))
                                LoadBlockTags(block, ini);

                            if (ini.ContainsSection(SECTION_HOOKS))
                                LoadBlockHooks(block, ini);
                        }
                    }

                    index += take;
                    yield return 0;
                }
            }

            LoadBlockGroups();
            _constructRefreshPending = false;

            yield return 0;
        }

        /// <summary>
        /// Coroutine to detach grids. Crawls from Mother's grid and prunes
        /// all grids no longer connected to the main construct.
        /// </summary>
        /// <returns></returns>
        IEnumerable<double> DetachGridsCoroutine()
        {
            // Rebuild adjacency without the now-broken connection
            BuildMechanicalAdjacency();

            // BFS from Mother's grid to find all grids still connected
            gridBfsQueue.Clear();
            visitedGrids.Clear();

            var stillConnectedGrids = new HashSet<long>();
            gridBfsQueue.Enqueue(Mother.CubeGrid);

            while (gridBfsQueue.Count > 0)
            {
                int processed = 0;

                while (gridBfsQueue.Count > 0 && processed < GRIDS_PER_TICK)
                {
                    var g = gridBfsQueue.Dequeue();
                    long gid = g.EntityId;

                    if (visitedGrids.Contains(gid)) { processed++; continue; }

                    visitedGrids.Add(gid);
                    stillConnectedGrids.Add(gid);

                    HashSet<long> neighbors;

                    if (gridAdjacencyMap.TryGetValue(gid, out neighbors))
                    {
                        foreach (long nid in neighbors)
                        {
                            var nGrid = TryGetGridFromId(nid);

                            if (nGrid != null && !visitedGrids.Contains(nid))
                                gridBfsQueue.Enqueue(nGrid);
                        }
                    }

                    processed++;
                }

                yield return 0;
            }

            // Determine which grids were removed (in old construct but not in new)
            var removedGrids = new HashSet<long>(ConstructGridIds);
            removedGrids.ExceptWith(stillConnectedGrids);

            // Update construct grid IDs
            ConstructGridIds = stillConnectedGrids;

            // Remove blocks from pruned grids
            if (removedGrids.Count > 0)
            {
                // Use RemoveAll for O(n) instead of O(n²)
                TerminalBlocks.RemoveAll(b =>
                {
                    if (!removedGrids.Contains(b.CubeGrid.EntityId)) return false;
                    
                    BlockConfigs.Remove(b);
                    BlocksToMonitor.Remove(b);
                    BlockStates.Remove(b.EntityId);
                    BlockHooks.Remove(b);

                    foreach (var tagSet in BlockTags.Values)
                        tagSet.Remove(b);

                    return true;
                });
            }

            LoadBlockGroups();
            _constructRefreshPending = false;

            yield return 0;
        }

        /// <summary>
        /// Coroutine to refresh the construct. This will re-discover grids and
        /// reload blocks without clearing existing state monitoring.
        /// </summary>
        /// <returns></returns>
        IEnumerable<double> RefreshConstructCoroutine()
        {
            // Build adjacency from mechanical blocks
            BuildMechanicalAdjacency();

            // BFS the construct in batches
            gridBfsQueue.Clear();
            visitedGrids.Clear();

            var newConstructGridIds = new HashSet<long>();
            gridBfsQueue.Enqueue(Mother.CubeGrid);

            while (gridBfsQueue.Count > 0)
            {
                int processed = 0;

                while (gridBfsQueue.Count > 0 && processed < GRIDS_PER_TICK)
                {
                    var g = gridBfsQueue.Dequeue();
                    long gid = g.EntityId;

                    if (visitedGrids.Contains(gid)) { processed++; continue; }

                    visitedGrids.Add(gid);
                    newConstructGridIds.Add(gid);

                    HashSet<long> neighbors;

                    if (gridAdjacencyMap.TryGetValue(gid, out neighbors))
                    {
                        foreach (long nid in neighbors)
                        {
                            var nGrid = TryGetGridFromId(nid);
                            if (nGrid != null && !visitedGrids.Contains(nid))
                                gridBfsQueue.Enqueue(nGrid);
                        }
                    }

                    processed++;
                }

                yield return 0;
            }

            // Determine added and removed grids
            var addedGrids = new HashSet<long>(newConstructGridIds);
            addedGrids.ExceptWith(ConstructGridIds);

            var removedGrids = new HashSet<long>(ConstructGridIds);
            removedGrids.ExceptWith(newConstructGridIds);

            // Update the construct grid IDs
            ConstructGridIds = newConstructGridIds;

            // Handle removed grids - remove blocks that are no longer part of the construct
            if (removedGrids.Count > 0)
            {
                TerminalBlocks.RemoveAll(b =>
                {
                    if (!removedGrids.Contains(b.CubeGrid.EntityId)) return false;
                    
                    BlockConfigs.Remove(b);
                    BlocksToMonitor.Remove(b);
                    BlockStates.Remove(b.EntityId);
                    BlockHooks.Remove(b);

                    foreach (var tagSet in BlockTags.Values)
                        tagSet.Remove(b);

                    return true;
                });
            }

            // Handle added grids - add new blocks to the construct
            if (addedGrids.Count > 0)
            {
                var allBlocks = new List<IMyTerminalBlock>();
                Mother.GridTerminalSystem.GetBlocks(allBlocks);

                var newBlocks = allBlocks
                    .Where(b => addedGrids.Contains(b.CubeGrid.EntityId))
                    .ToList();

                // Add new blocks and parse their configurations
                int index = 0;
                while (index < newBlocks.Count)
                {
                    int take = Math.Min(BLOCKS_PER_TICK_LOAD, newBlocks.Count - index);

                    for (int i = 0; i < take; i++)
                    {
                        var block = newBlocks[index + i];
                        TerminalBlocks.Add(block);

                        var ini = new MyIni();
                        MyIniParseResult res;

                        if (ini.TryParse(block.CustomData, out res))
                        {
                            BlockConfigs[block] = ini;

                            if (ini.ToString().Length == 0)
                                SetDefaultConfiguration(block, ini);

                            if (ini.ContainsSection(SECTION_GENERAL))
                                LoadBlockTags(block, ini);

                            if (ini.ContainsSection(SECTION_HOOKS))
                                LoadBlockHooks(block, ini);
                        }
                    }

                    index += take;
                    yield return 0;
                }
            }

            // Reload block groups
            LoadBlockGroups();

            _constructRefreshPending = false;

            yield return 0;
        }
    }
}
