using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MotherCore.Tests.Utilities;
using MotherCore.Tests.Utilities.Factories;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components.Interfaces;
using VRage.Game.ModAPI.Ingame;
using VRage.ObjectBuilders;
using VRageMath;

namespace MotherCore.Tests.Utilities.Mocks
{
    public sealed class FakeTerminalBlockExtras
    {
        public string CustomInfo { get; set; } = string.Empty;

        public string CustomNameWithFaction { get; set; }

        public string DetailedInfo { get; set; } = string.Empty;

        public SerializableDefinitionId BlockDefinition { get; set; }

        public float DisassembleRatio { get; set; }

        public bool IsBeingHacked { get; set; }

        public float Mass { get; set; }

        public Vector3I Max { get; set; }

        public Vector3I Min { get; set; }

        public int NumberInGrid { get; set; }

        public MyBlockOrientation Orientation { get; set; }

        public long OwnerId { get; set; }

        public Vector3I Position { get; set; }

        public IMyEntityComponentContainer Components { get; set; }

        public string EntityName { get; set; }

        public BoundingBoxD WorldAABB { get; set; }

        public BoundingBoxD WorldAABBHr { get; set; }

        public MatrixD WorldMatrix { get; set; } = MatrixD.Identity;

        public BoundingSphereD WorldVolume { get; set; }

        public BoundingSphereD WorldVolumeHr { get; set; }
    }

    /// <summary>
    /// Shared concrete base for fake terminal blocks used by the MotherCore harness.
    /// Only exposes the mutable state MotherCore currently uses directly; the rest of the
    /// Space Engineers interface is implemented explicitly with inert defaults so tests can
    /// opt into extra setup only when they truly need it.
    /// </summary>
    /// <remarks>
    /// API surface, member naming, and behavior intent are aligned with the programmable
    /// block reference for <see cref="IMyTerminalBlock"/>:
    /// https://malforge.github.io/spaceengineers/pbapi/Sandbox.ModAPI.Ingame.IMyTerminalBlock.html
    /// </remarks>
    public abstract class FakeTerminalBlock : IMyTerminalBlock
    {
        readonly List<IMyInventory> _inventories = new List<IMyInventory>();
        readonly Dictionary<string, ITerminalAction> _terminalActions =
            new Dictionary<string, ITerminalAction>(StringComparer.OrdinalIgnoreCase);
        readonly List<string> _requestedActionNames = new List<string>();
        readonly List<string> _requestedPropertyIds = new List<string>();
        static readonly object NameCounterLock = new object();
        static readonly Dictionary<string, Dictionary<string, int>> DefaultNameCountersByScope =
            new Dictionary<string, Dictionary<string, int>>();

        /// <summary>
        /// Initializes a fake terminal block with optional custom identity and grid placement.
        /// </summary>
        /// <param name="customName">
        /// Optional explicit custom name. When omitted, a deterministic default name is generated.
        /// </param>
        /// <param name="customData">Optional custom data payload exposed via <see cref="CustomData"/>.</param>
        /// <param name="entityId">Optional explicit entity ID. When omitted, a synthetic ID is generated.</param>
        /// <param name="cubeGrid">Optional owning grid. Can also be assigned later by the harness.</param>
        protected FakeTerminalBlock(
            string customName = null,
            string customData = "",
            long? entityId = null,
            IMyCubeGrid cubeGrid = null)
        {
            IsCustomNameExplicit = !string.IsNullOrWhiteSpace(customName);
            CustomName = IsCustomNameExplicit
                ? customName
                : BuildDefaultCustomName(GetType().Name);
            CustomData = customData ?? string.Empty;
            EntityId = entityId ?? CreateEntityId();
            CubeGrid = cubeGrid;
            Extra.EntityName = CustomName;
        }

        /// <summary>
        /// Additional mutable metadata backing explicit interface members that are
        /// not commonly used in tests.
        /// </summary>
        public FakeTerminalBlockExtras Extra { get; } = new FakeTerminalBlockExtras();

        /// <summary>
        /// Optional override for construct-membership checks used by
        /// <see cref="IsSameConstructAs(IMyTerminalBlock)"/>.
        /// </summary>
        public Func<IMyTerminalBlock, bool> SameConstructEvaluator { get; set; }

        /// <summary>
        /// Gets or sets whether this block is enabled.
        /// Mirrors the concept exposed by PB terminal blocks.
        /// </summary>
        public virtual bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets whether <see cref="CustomName"/> was supplied explicitly by the caller.
        /// </summary>
        public bool IsCustomNameExplicit { get; }

        /// <summary>
        /// Gets or sets terminal custom data for this block.
        /// </summary>
        public string CustomData { get; set; }

        /// <summary>
        /// Gets or sets the terminal custom name for this block.
        /// </summary>
        public string CustomName { get; set; }

        /// <summary>
        /// Gets or sets the owning cube grid for this block.
        /// </summary>
        public IMyCubeGrid CubeGrid { get; set; }

        /// <summary>
        /// Gets or sets whether this block is functional.
        /// </summary>
        public bool IsFunctional { get; set; } = true;

        /// <summary>
        /// Gets or sets whether this block is currently working.
        /// </summary>
        public bool IsWorking { get; set; } = true;

        /// <summary>
        /// Gets or sets whether this block has been closed/disposed.
        /// </summary>
        public bool Closed { get; set; }

        /// <summary>
        /// Gets or sets the synthetic or explicit entity ID for this block.
        /// </summary>
        public long EntityId { get; set; }

        /// <summary>
        /// Gets or sets the world position returned by <see cref="GetPosition"/>.
        /// </summary>
        public Vector3D WorldPosition { get; set; }

        /// <summary>
        /// Gets the ordered list of action names requested via
        /// <see cref="IMyTerminalBlock.GetActionWithName(string)"/>.
        /// </summary>
        public IReadOnlyList<string> RequestedActionNames => _requestedActionNames;

        /// <summary>
        /// Gets the ordered list of property ids requested via
        /// <see cref="IMyTerminalBlock.GetProperty(string)"/>.
        /// </summary>
        public IReadOnlyList<string> RequestedPropertyIds => _requestedPropertyIds;

        /// <summary>
        /// Gets the number of times actions were enumerated via
        /// <see cref="IMyTerminalBlock.GetActions(List{ITerminalAction}, Func{ITerminalAction, bool})"/>.
        /// </summary>
        public int GetActionsCallCount { get; private set; }

        /// <summary>
        /// Gets the number of times properties were enumerated via
        /// <see cref="IMyTerminalBlock.GetProperties(List{ITerminalProperty}, Func{ITerminalProperty, bool})"/>.
        /// </summary>
        public int GetPropertiesCallCount { get; private set; }

        /// <summary>
        /// Gets the number of terminal action applications performed against this block.
        /// </summary>
        public int ApplyActionCallCount { get; private set; }

        /// <summary>
        /// Gets the last action id applied to this block, or <see langword="null"/>
        /// when no action has been applied.
        /// </summary>
        public string LastAppliedActionId { get; private set; }

        /// <summary>
        /// Gets the parameter count from the last applied action invocation.
        /// </summary>
        public int LastAppliedActionParameterCount { get; private set; }

        /// <summary>
        /// Applies an enable/disable request to this fake block.
        /// </summary>
        /// <param name="enable">The requested enabled state.</param>
        public virtual void RequestEnable(bool enable)
        {
            Enabled = enable;
        }

        /// <summary>
        /// Registers a terminal action id on this fake so action-based module paths
        /// can resolve and invoke it.
        /// </summary>
        /// <param name="actionId">Terminal action id.</param>
        public void RegisterTerminalAction(string actionId)
        {
            if (string.IsNullOrWhiteSpace(actionId))
                return;

            _terminalActions[actionId] = new FakeTerminalAction(actionId, this);
        }

        /// <summary>
        /// Clears tracked action/property interaction history.
        /// </summary>
        public void ClearTerminalInteractionHistory()
        {
            _requestedActionNames.Clear();
            _requestedPropertyIds.Clear();
            GetActionsCallCount = 0;
            GetPropertiesCallCount = 0;
            ApplyActionCallCount = 0;
            LastAppliedActionId = null;
            LastAppliedActionParameterCount = 0;
        }

        /// <summary>
        /// Asserts that this fake block received a terminal action/property request.
        /// When <paramref name="parameters"/> is provided, also validates action invocation
        /// with the expected parameter count.
        /// </summary>
        /// <param name="actionOrPropertyName">The action id or property id expected to be requested.</param>
        /// <param name="parameters">Optional expected action parameters.</param>
        public void ShouldHaveRunAction(string actionOrPropertyName, IEnumerable<string> parameters = null)
        {
            Assert.That(string.IsNullOrWhiteSpace(actionOrPropertyName), Is.False,
                "Expected an action/property name to assert against.");

            var expectedParameters = parameters?.ToList();

            var requestedAsAction = RequestedActionNames
                .Any(name => string.Equals(name, actionOrPropertyName, StringComparison.OrdinalIgnoreCase));

            var requestedAsProperty = RequestedPropertyIds
                .Any(id => string.Equals(id, actionOrPropertyName, StringComparison.OrdinalIgnoreCase));

            Assert.That(requestedAsAction || requestedAsProperty, Is.True,
                $"Expected terminal action/property '{actionOrPropertyName}' to be requested.");

            if (!requestedAsAction)
                return;

            Assert.That(ApplyActionCallCount, Is.GreaterThan(0),
                $"Expected action '{actionOrPropertyName}' to be applied at least once.");
            Assert.That(LastAppliedActionId, Is.EqualTo(actionOrPropertyName),
                "Unexpected last applied terminal action id.");

            if (expectedParameters != null)
                Assert.That(LastAppliedActionParameterCount, Is.EqualTo(expectedParameters.Count),
                    "Unexpected terminal action parameter count.");
        }

        /// <summary>
        /// Asserts how many times this block was asked to enumerate terminal actions.
        /// </summary>
        /// <param name="count">Expected number of list calls.</param>
        public void ShouldHaveListedActions(int count = 1)
        {
            Assert.That(GetActionsCallCount, Is.EqualTo(count));
        }

        /// <summary>
        /// Reports whether this block and <paramref name="other"/> are in the same construct.
        /// </summary>
        /// <param name="other">The other block to compare against.</param>
        /// <returns>
        /// <see langword="true"/> when both blocks are on the same grid construct;
        /// otherwise <see langword="false"/>.
        /// </returns>
        public bool IsSameConstructAs(IMyTerminalBlock other)
        {
            if (SameConstructEvaluator != null)
                return SameConstructEvaluator(other);

            return other != null
                && CubeGrid != null
                && other.CubeGrid != null
                && CubeGrid.EntityId == other.CubeGrid.EntityId;
        }

        /// <summary>
        /// Sets the block custom name and updates the backing entity display name.
        /// </summary>
        /// <param name="text">The new custom name value.</param>
        public void SetCustomName(string text)
        {
            CustomName = text;
            Extra.EntityName = text;
            OnCustomNameChanged();
        }

        /// <summary>
        /// Sets the block custom name from a <see cref="StringBuilder"/> payload.
        /// </summary>
        /// <param name="text">The new custom name value.</param>
        public void SetCustomName(StringBuilder text)
        {
            SetCustomName(text?.ToString());
        }

        /// <summary>
        /// Builds a deterministic default name from the runtime fake type.
        /// Example: <c>FakeLightingBlock</c> -> <c>Lighting Block N</c>.
        /// </summary>
        /// <param name="runtimeTypeName">The concrete runtime type name.</param>
        /// <returns>A generated default block name.</returns>
        static string BuildDefaultCustomName(string runtimeTypeName)
        {
            var typeName = runtimeTypeName ?? string.Empty;

            if (typeName.StartsWith("Fake", StringComparison.Ordinal))
                typeName = typeName.Substring("Fake".Length);

            if (string.IsNullOrEmpty(typeName))
                return "Block 1";

            var words = new StringBuilder(typeName.Length + 8);

            for (int i = 0; i < typeName.Length; i++)
            {
                var current = typeName[i];

                if (i > 0 && char.IsUpper(current) && !char.IsUpper(typeName[i - 1]))
                    words.Append(' ');

                words.Append(current);
            }

            words.Append(" ");
            words.Append(GetNextDefaultNameIndex(typeName));

            return words.ToString();
        }

        /// <summary>
        /// Gets the next 1-based default-name index for the supplied fake block type
        /// within the current naming scope.
        /// </summary>
        /// <param name="typeName">The normalized fake block type name (without the Fake prefix).</param>
        /// <returns>The next sequence number for the current scope and block type.</returns>
        static int GetNextDefaultNameIndex(string typeName)
        {
            lock (NameCounterLock)
            {
                var scopeKey = GetCurrentCounterScope();
                Dictionary<string, int> counters;

                if (!DefaultNameCountersByScope.TryGetValue(scopeKey, out counters))
                {
                    counters = new Dictionary<string, int>();
                    DefaultNameCountersByScope[scopeKey] = counters;
                }

                int nextIndex;

                if (!counters.TryGetValue(typeName, out nextIndex))
                    nextIndex = 1;

                counters[typeName] = nextIndex + 1;

                return nextIndex;
            }
        }

        /// <summary>
        /// Resolves the current naming-counter scope key.
        /// Uses the active NUnit test ID when available so each test gets an
        /// isolated default-name sequence; otherwise falls back to a global scope.
        /// </summary>
        /// <returns>The scope key used to partition default-name counters.</returns>
        static string GetCurrentCounterScope()
        {
            var testId = TestContext.CurrentContext?.Test?.ID;

            if (string.IsNullOrWhiteSpace(testId))
                return "global";

            return testId;
        }

        /// <summary>
        /// Returns the first inventory attached to this block, if present.
        /// </summary>
        /// <returns>The inventory at index 0, or <see langword="null"/>.</returns>
        public IMyInventory GetInventory()
        {
            return GetInventory(0);
        }

        /// <summary>
        /// Returns the inventory at the requested index.
        /// </summary>
        /// <param name="index">The zero-based inventory index.</param>
        /// <returns>The matching inventory, or <see langword="null"/> when out of range.</returns>
        public IMyInventory GetInventory(int index)
        {
            if (index < 0 || index >= _inventories.Count)
                return null;

            return _inventories[index];
        }

        /// <summary>
        /// Returns the current world position for this block.
        /// </summary>
        /// <returns>The value of <see cref="WorldPosition"/>.</returns>
        public Vector3D GetPosition()
        {
            return WorldPosition;
        }

        string IMyTerminalBlock.CustomInfo => Extra.CustomInfo;

        string IMyTerminalBlock.CustomNameWithFaction => Extra.CustomNameWithFaction;

        string IMyTerminalBlock.DetailedInfo => Extra.DetailedInfo;

        bool IMyTerminalBlock.ShowInInventory { get; set; } = true;

        bool IMyTerminalBlock.ShowInTerminal { get; set; } = true;

        bool IMyTerminalBlock.ShowInToolbarConfig { get; set; } = true;

        bool IMyTerminalBlock.ShowOnHUD { get; set; }

        void IMyTerminalBlock.GetActions(List<ITerminalAction> resultList, Func<ITerminalAction, bool> collect)
        {
            GetActionsCallCount++;

            if (resultList == null)
                return;

            foreach (var action in _terminalActions.Values)
            {
                if (collect == null || collect(action))
                    resultList.Add(action);
            }
        }

        ITerminalAction IMyTerminalBlock.GetActionWithName(string name)
        {
            _requestedActionNames.Add(name);

            ITerminalAction action;
            if (_terminalActions.TryGetValue(name ?? string.Empty, out action))
                return action;

            if (string.IsNullOrWhiteSpace(name))
                return null;

            // Accept unregistered action ids so tests can assert requested names/params
            // without extra setup noise.
            action = new FakeTerminalAction(name, this);
            _terminalActions[name] = action;
            return action;
        }

        void IMyTerminalBlock.GetProperties(List<ITerminalProperty> resultList, Func<ITerminalProperty, bool> collect)
        {
            GetPropertiesCallCount++;
        }

        ITerminalProperty IMyTerminalBlock.GetProperty(string id)
        {
            _requestedPropertyIds.Add(id);
            return null;
        }

        bool IMyTerminalBlock.HasLocalPlayerAccess()
        {
            return true;
        }

        bool IMyTerminalBlock.HasNobodyPlayerAccessToBlock()
        {
            return true;
        }

        bool IMyTerminalBlock.HasPlayerAccess(long playerId, MyRelationsBetweenPlayerAndBlock defaultNoUserRelation)
        {
            return true;
        }

        bool IMyTerminalBlock.HasPlayerAccessWithNobodyCheck(long playerId, bool defaultNoUser)
        {
            return true;
        }

        void IMyTerminalBlock.SearchActionsOfName(string name, List<ITerminalAction> resultList, Func<ITerminalAction, bool> collect)
        {
            if (resultList == null || string.IsNullOrEmpty(name))
                return;

            foreach (var action in _terminalActions.Values)
            {
                if (!action.Id.Contains(name))
                    continue;

                if (collect == null || collect(action))
                    resultList.Add(action);
            }
        }

        string IMyCubeBlock.GetOwnerFactionTag()
        {
            return string.Empty;
        }

        MyRelationsBetweenPlayerAndBlock IMyCubeBlock.GetPlayerRelationToOwner()
        {
            return MyRelationsBetweenPlayerAndBlock.NoOwnership;
        }

        MyRelationsBetweenPlayerAndBlock IMyCubeBlock.GetUserRelationToOwner(long playerId, MyRelationsBetweenPlayerAndBlock defaultNoUserRelation)
        {
            return defaultNoUserRelation;
        }

        void IMyCubeBlock.UpdateIsWorking()
        {
        }

        void IMyCubeBlock.UpdateVisual()
        {
        }

        SerializableDefinitionId IMyCubeBlock.BlockDefinition => Extra.BlockDefinition;

        string IMyCubeBlock.DefinitionDisplayNameText => CustomName;

        float IMyCubeBlock.DisassembleRatio => Extra.DisassembleRatio;

        string IMyCubeBlock.DisplayNameText => CustomName;

        bool IMyCubeBlock.IsBeingHacked => Extra.IsBeingHacked;

        float IMyCubeBlock.Mass => Extra.Mass;

        Vector3I IMyCubeBlock.Max => Extra.Max;

        Vector3I IMyCubeBlock.Min => Extra.Min;

        int IMyCubeBlock.NumberInGrid => Extra.NumberInGrid;

        MyBlockOrientation IMyCubeBlock.Orientation => Extra.Orientation;

        long IMyCubeBlock.OwnerId => Extra.OwnerId;

        Vector3I IMyCubeBlock.Position => Extra.Position;

        IMyEntityComponentContainer IMyEntity.Components => Extra.Components;

        string IMyEntity.DisplayName => CustomName;

        bool IMyEntity.HasInventory => _inventories.Count > 0;

        int IMyEntity.InventoryCount => _inventories.Count;

        string IMyEntity.Name
        {
            get { return Extra.EntityName; }
        }

        BoundingBoxD IMyEntity.WorldAABB => Extra.WorldAABB;

        BoundingBoxD IMyEntity.WorldAABBHr => Extra.WorldAABBHr;

        MatrixD IMyEntity.WorldMatrix => Extra.WorldMatrix;

        BoundingSphereD IMyEntity.WorldVolume => Extra.WorldVolume;

        BoundingSphereD IMyEntity.WorldVolumeHr => Extra.WorldVolumeHr;

        /// <summary>
        /// Adds an inventory to this block's internal inventory list.
        /// </summary>
        /// <param name="inventory">The inventory instance to attach.</param>
        protected void AddInventoryInternal(IMyInventory inventory)
        {
            _inventories.Add(inventory);
        }

        /// <summary>
        /// Extension hook invoked whenever <see cref="SetCustomName(string)"/> changes the name.
        /// Derived fakes can override this to synchronize additional state.
        /// </summary>
        protected virtual void OnCustomNameChanged()
        {
        }

        void RecordActionApplied(string actionId, int parameterCount)
        {
            ApplyActionCallCount++;
            LastAppliedActionId = actionId;
            LastAppliedActionParameterCount = parameterCount;
        }

        sealed class FakeTerminalAction : ITerminalAction
        {
            readonly FakeTerminalBlock _owner;

            public FakeTerminalAction(string id, FakeTerminalBlock owner)
            {
                Id = id;
                _owner = owner;
                Name = new StringBuilder(id ?? string.Empty);
            }

            public string Id { get; }

            public StringBuilder Name { get; }

            public string Icon { get; } = string.Empty;

            public bool IsEnabled(IMyTerminalBlock block)
            {
                return true;
            }

            public bool IsEnabled(IMyCubeBlock block)
            {
                return true;
            }

            public void Apply(IMyTerminalBlock block)
            {
                _owner.RecordActionApplied(Id, 0);
            }

            public void Apply(IMyCubeBlock block)
            {
                _owner.RecordActionApplied(Id, 0);
            }

            public void Apply(IMyTerminalBlock block, List<TerminalActionParameter> parameters)
            {
                _owner.RecordActionApplied(Id, parameters != null ? parameters.Count : 0);
            }

            public void Apply(IMyCubeBlock block, List<TerminalActionParameter> parameters)
            {
                _owner.RecordActionApplied(Id, parameters != null ? parameters.Count : 0);
            }

            public void Apply(IMyCubeBlock block, ListReader<TerminalActionParameter> parameters)
            {
                _owner.RecordActionApplied(Id, parameters.Count);
            }

            public void WriteValue(IMyTerminalBlock block, StringBuilder appendTo)
            {
            }

            public void WriteValue(IMyCubeBlock block, StringBuilder appendTo)
            {
            }
        }

        /// <summary>
        /// Creates a unique synthetic entity ID for fake blocks.
        /// </summary>
        /// <returns>A monotonically increasing synthetic entity ID.</returns>
        protected static long CreateEntityId() => EntityIdFactory.Create();
    }
}