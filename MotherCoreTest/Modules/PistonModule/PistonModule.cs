using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
//using Sandbox.ModAPI;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using System;
using System.Collections.Generic;
using VRageMath;
namespace IngameScript
{
    partial class Program
    {
        /// <summary>
        /// This module is used to handle piston blocks on the grid.
        /// </summary>
        /// <see href="https://github.com/malware-dev/MDK-SE/wiki/Sandbox.ModAPI.Ingame.IMyPistonBase"/>
        /// <see href="https://github.com/malware-dev/MDK-SE/wiki/Sandbox.ModAPI.Ingame.PistonStatus"/>
        public class PistonModule : BaseExtensionModule
        {
            /// <summary>
            /// The BlockCatalogue core module.
            /// </summary>
            BlockCatalogue BlockCatalogue;

            /// <summary>
            /// Default speed of the piston in m/s.
            /// </summary>
            public const float DEFAULT_SPEED = 0.5f;

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="mother"></param>
            public PistonModule(Mother mother) : base(mother) { }

            /// <summary>
            /// Boots the module. We register commands.
            /// </summary>
            public override void Boot()
            {
                // Modules
                BlockCatalogue = Mother.GetModule<BlockCatalogue>();

                // Commands
                RegisterCommand(new ResetPistonCommand(this));
                RegisterCommand(new SetPistonDistanceCommand(this));
                RegisterCommand(new StopPistonCommand(this));
                RegisterCommand(new SetPistonSpeedCommand(this));

                // Monitor block for state changes
                RegisterBlockTypeForStateMonitoring<IMyPistonBase>(
                   piston => piston.Status,
                   (block, state) => HandlePistonStateChange(block as IMyPistonBase, (PistonStatus) state)
               );
            }

            /// <summary>
            /// Handle the state change of a piston. This method is called when the state of a piston changes.
            /// </summary>
            /// <param name="piston"></param>
            /// <param name="status"></param>
            private void HandlePistonStateChange(IMyPistonBase piston, PistonStatus status)
            {
                if (status == PistonStatus.Extending)
                {
                    Emit<PistonExtendingEvent>(piston);
                    BlockCatalogue.RunHook(piston, "onExtending");
                }

                else if (status == PistonStatus.Extended)
                {
                    Emit<PistonExtendedEvent>(piston);
                    BlockCatalogue.RunHook(piston, "onExtended");
                }

                else if (status == PistonStatus.Retracting)
                {
                    Emit<PistonRetractingEvent>(piston);
                    BlockCatalogue.RunHook(piston, "onRetracting");
                }
                
                else if (status == PistonStatus.Retracted)
                {
                    Emit<PistonRetractedEvent>(piston);
                    BlockCatalogue.RunHook(piston, "onRetracted");
                }
            }

            /// <summary>
            /// Resets the piston to distance 0f 0 meters.
            /// </summary>
            /// <param name="piston"></param>
            public void Reset(IMyPistonBase piston)
            {
                SetDistance(piston, 0);
            }

            /// <summary>
            /// Stops the piston.
            /// </summary>
            /// <param name="piston"></param>
            public void Stop(IMyPistonBase piston)
            {
                piston.Velocity = 0;
            }

            /// <summary>
            /// Sets the speed of the piston in m/s.
            /// </summary>
            /// <param name="piston"></param>
            /// <param name="speed"></param>
            public void SetPistonSpeed(IMyPistonBase piston, float speed)
            {
                piston.Velocity = speed;
            }

            /// <summary>
            /// Move the piston to the desired distance in meters.
            /// </summary>
            /// <param name="piston"></param>
            /// <param name="distance"></param>
            /// <param name="speed"></param>
            public void SetDistance(IMyPistonBase piston, float distance, float speed = DEFAULT_SPEED)
            {
                float currentVelocity = piston.Velocity;
                float currentDistance = piston.CurrentPosition;
                float defaultVelocity = speed;

                if (distance == 0)
                    piston.Retract();

                // if current distance is less than target distance, extend, else retract
                if (currentDistance < distance)
                {        
                    piston.MaxLimit = distance;
                    piston.Velocity = defaultVelocity;
                }
                else if (currentDistance > distance)
                {
                    piston.MinLimit = distance;
                    piston.Velocity = -defaultVelocity;
                }

                // Monitor block while in motion
                Mother.GetModule<ActivityMonitor>().RegisterBlock(
                    piston,
                    block => PistonAtTerminalPosition(block as IMyPistonBase),
                    block => StopPiston(block as IMyPistonBase)
                );
            }
  
            /// <summary>
            /// Check if the piston is at its terminal position. Practically, we are checking 
            /// if the piston has reached its max or min limit.
            /// </summary>
            /// <param name="piston"></param>
            /// <see href="https://github.com/malware-dev/MDK-SE/wiki/Sandbox.ModAPI.Ingame.PistonStatus"/>
            public bool PistonAtTerminalPosition(IMyPistonBase piston)
            {
                return piston.Status == PistonStatus.Extended || piston.Status == PistonStatus.Retracted;
            }

            /// <summary>
            /// Stops the piston.
            /// </summary>
            /// <param name="piston"></param>
            public void StopPiston(IMyPistonBase piston)
            {
                piston.Velocity = 0;
            }
        }
    } 
}
