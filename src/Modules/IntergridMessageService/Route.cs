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
    /// The Route class is responsible for handling route definition for use in 
    /// the Router. A route allows other grids to send specialized messages 
    /// to our grid.
    /// </summary>
    public class Route
    {
        /// <summary>
        /// The path of the route.
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// The Handler function that will be executed when the route is matched.
        /// </summary>
        public Func<Request, Response> Handler { get; }

        /// <summary>
        /// Constructor for the Route class. It accepts a path and a handler function
        /// for action when a request to the route path is received.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="handler"></param>
        public Route(string path, Func<Request, Response> handler)
        {
            Path = path;
            Handler = handler;
        }
    }
}
