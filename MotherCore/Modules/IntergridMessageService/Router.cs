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
    /// The Router class is responsible for handling custom endpoints used 
    /// by intergrid communication. Other grids running Mother may 
    /// access routes via the 'path' Header value in the Request.
    /// </summary>
    public class Router
    {
        /// <summary>
        /// Dictionary of routes.
        /// </summary>
        public readonly List<Route> Routes = new List<Route>();

        /// <summary>
        /// Handles a route by invoking the callback defined 
        /// for the route with the incoming Request.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        public Response HandleRoute(string path, Request request)
        {
            var route = Routes.FirstOrDefault(r => r.Path == path);
            return route?.Handler?.Invoke(request);
        }

        /// <summary>
        /// Adds a route to the Router.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="route"></param>
        public void AddRoute(string path, Func<Request, Response> route)
        {
            Routes.Add(new Route(path, route));
        }
    }
}
