using IngameScript;
using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;

namespace MotherCore.Tests.Utilities
{
    /// <summary>
    /// Fluent builder for configuring and creating world harness instances.
    /// Create via <c>new WorldFactory()</c> or the <see cref="TestBase"/> helper.
    /// Example:
    /// <code>
    /// var world = new WorldFactory().Boot();
    /// var seeded = new WorldFactory().WithScript("ShipA").WithScript("ShipB").Boot();
    /// </code>
    /// </summary>
    public class WorldFactory
    {
        readonly List<Action<World>> _configure = new List<Action<World>>();

        /// <summary>
        /// Appends custom world configuration logic before creation.
        /// </summary>
        public WorldFactory Configure(Action<World> configure)
        {
            if (configure == null)
                throw new ArgumentNullException(nameof(configure));

            _configure.Add(configure);
            return this;
        }

        /// <summary>
        /// Adds and boots a default-program script in the world during creation.
        /// </summary>
        public WorldFactory WithScript(string scriptName = null)
        {
            return Configure(world => world.CreateScript(scriptName).Boot());
        }

        /// <summary>
        /// Adds and boots a typed script in the world during creation.
        /// </summary>
        public WorldFactory WithScript<TProgram>(string scriptName = null)
            where TProgram : MyGridProgram, new()
        {
            return Configure(world => world.CreateScript<TProgram>(scriptName).Boot());
        }

        /// <summary>
        /// Adds a world grid during creation.
        /// Mirrors <see cref="World.CreateGrid(string, long?)"/>.
        /// </summary>
        public WorldFactory WithGrid(string gridName = null, long? entityId = null)
        {
            return Configure(world => world.CreateGrid(gridName, entityId));
        }

        /// <summary>
        /// Creates an unbooted world harness with all queued configuration applied.
        /// </summary>
        public World Create()
        {
            var world = new World();

            foreach (var configure in _configure)
                configure(world);

            return world;
        }

        /// <summary>
        /// Creates and boots a world harness in one step.
        /// </summary>
        public World Boot()
        {
            return Create().Boot();
        }
    }
}