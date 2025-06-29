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
    /// The Clock class is our system timer.  All actions and commands run by Mother are 
    /// controlled by this class.  It uses the yield keyword to operate across game 
    /// cycles to enable fine-tuned delays down to a fidelity of 0.166_ 
    /// seconds (Update10, 6 ticks per second).
    /// </summary>
    public class Clock : BaseCoreModule
    {
        /// <summary>
        /// A ScheduledTask to be run at a later time.
        /// </summary>
        class ScheduledTask
            {
            /// <summary>
            /// The delay before the task should run, in seconds.
            /// </summary>
            public double Interval;

            /// <summary>
            /// The time remaining before the task should be run.
            /// </summary>
            public double TimeRemaining;

            /// <summary>
            /// The task to be run when time remaining reaches 0.
            /// </summary>
            public Action Task;
        }

        /// <summary>
        /// The system task to run at continuous interval.
        /// ie. Update displays, update Almanac, etc.
        /// </summary>
        readonly List<ScheduledTask> SystemTasks = new List<ScheduledTask>();

        /// <summary>
        /// The tasks queued for future execution.
        /// </summary>
        readonly Queue<ScheduledTask> QueuedTasks = new Queue<ScheduledTask>();

        // NEW: Simple coroutine class
        class Coroutine
        {
            public IEnumerator<double> Enumerator;
            public double WaitTime;
        }

        // NEW: List to hold active coroutines
        readonly List<Coroutine> Coroutines = new List<Coroutine>();

        /// <summary>
        /// The Program instance.
        /// </summary>
        readonly MyGridProgram Program;

        /// <summary>
        /// The loader indicator to assist with visual clock feedback.
        /// </summary>
        bool LoaderLeft = true;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="mother"></param>
        public Clock(Mother mother) : base (mother)
        {
            Program = mother.Program;
            //Program.Runtime.UpdateFrequency = UpdateFrequency.Update1;
            Program.Runtime.UpdateFrequency = UpdateFrequency.Update10;

            // Schedule loader to update each second to assist with visual feedback.
            Schedule(UpdateLoader, 1);
        }

        /// <summary>
        /// Schedule a system task for execution continuously over an interval.
        /// </summary>
        /// <param name="task"></param>
        /// <param name="interval"></param>
        public void Schedule(Action task, double interval = 0)
        {
            SystemTasks.Add(new ScheduledTask { Task = task, Interval = interval, TimeRemaining = interval });
        }
            
        /// <summary>
        /// Queue a task for execution after a time delay (wait time).
        /// </summary>
        /// <param name="task"></param>
        /// <param name="waitTime"></param>
        public void QueueForLater(Action task, double waitTime)
        {
            QueuedTasks.Enqueue(new ScheduledTask { Task = task, Interval = waitTime, TimeRemaining = waitTime });
        }

        /// <summary>
        /// Start a coroutine that runs over multiple game cycles.
        /// </summary>
        /// <param name="routine"></param>
        public void StartCoroutine(IEnumerable<double> routine)
        {
            Coroutines.Add(new Coroutine { Enumerator = routine.GetEnumerator(), WaitTime = 0 });
        }

        /// <summary>
        /// Run the Clock eac program cycle. The clock will run scheduled 
        /// tasks and any tasks queued for execution with a time delay.
        /// </summary>
        public override void Run()
        {
            double deltaTime = Program.Runtime.TimeSinceLastRun.TotalSeconds;

            // Execute scheduled tasks
            foreach (var task in SystemTasks)
            {
                task.TimeRemaining -= deltaTime;

                if (task.TimeRemaining <= 0)
                {
                    task.Task.Invoke();
                    // Reset the timer for continuous tasks
                    task.TimeRemaining = task.Interval; 
                }
            }

            // Execute queued tasks based on their timers
            for (int i = QueuedTasks.Count - 1; i >= 0; i--)
            {
                // We access the task by index to avoid dequeuing during iteration
                var task = QueuedTasks.ElementAt(i);  
                task.TimeRemaining -= deltaTime;

                if (task.TimeRemaining <= 0)
                {
                    task.Task.Invoke();
                    // Remove task after execution
                    QueuedTasks.Dequeue();
                }
            }

            // NEW: Execute coroutines
            for (int i = Coroutines.Count - 1; i >= 0; i--)
            {
                var coroutine = Coroutines[i];
                coroutine.WaitTime -= deltaTime;

                if (coroutine.WaitTime <= 0)
                {
                    if (coroutine.Enumerator.MoveNext())
                    {
                        coroutine.WaitTime = coroutine.Enumerator.Current;
                    }
                    else
                    {
                        coroutine.Enumerator.Dispose();
                        Coroutines.RemoveAt(i);
                    }
                }
            }
        }

        /// <summary>
        /// Get the count of queued tasks.
        /// </summary>
        public int QueuedTaskCount
        {
            get { return QueuedTasks.Count; }
        }

        /// <summary>
        /// Update the loader indicator state.
        /// </summary>
        void UpdateLoader()
        {
            LoaderLeft = !LoaderLeft;
        }

        /// <summary>
        /// Get the loader indicator.
        /// </summary>
        /// <returns></returns>
        public string GetLoader()
        {
            return LoaderLeft ? "/" : "\\";
        }
    }
}
