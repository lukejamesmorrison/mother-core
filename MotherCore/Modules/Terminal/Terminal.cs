using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRage;
using VRageMath;
using System.Collections.Immutable;

namespace IngameScript
{

    /// <summary>
    /// The Terminal class is used to manage the programmable block terminal window.
    /// We print indicators of core systems and display system feedback to the 
    /// player during actions like command execution docking procedures.
    /// </summary>
    public class Terminal : BaseCoreModule
	{
        /// <summary>
        /// Mother reference.
        /// </summary>
		//readonly Mother Mother;

        /// <summary>
        /// The Clock core module.
        /// </summary>
        Clock Clock;

        /// <summary>
        /// Full print strings collection.
        /// </summary>
        readonly List<string> FullPrintStrings = new List<string>();

        /// <summary>
        /// Trimmed print strings collection. These are limited in length to print 
        /// nicely with the console window width of approx. 40 characters.
        /// </summary>
        readonly List<string> TrimmedPrintStrings = new List<string>();

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="mother"></param>
        public Terminal(Mother mother) : base(mother)
		{
			//Mother = mother;
		}

        /// <summary>
        /// Boot the module. We schedule the terminal update every program cycle.
        /// </summary>
        public override void Boot()
        {
            // Modules
            Clock = Mother.GetModule<Clock>();
            CommandBus CommandBus = Mother.GetModule<CommandBus>();

            // Commands
            CommandBus.RegisterCommand(new ClearCommand(this));
            CommandBus.RegisterCommand(new PrintCommand(this));

            Clock.Schedule(UpdateTerminal);
        }

        /// <summary>
        /// The highlights string to enable the printing of important information 
        /// to the top of the programmable block terminal window.
        /// </summary>
        string Highlights = "";

        /// <summary>
        /// Get the highlights string. This is used to print important information 
        /// to the top of the programmable block terminal window.
        /// </summary>
        /// <returns></returns>
        public string GetHighlights()
        {
            return Highlights;
        }

        /// <summary>
        /// Add a highlight message to the highlights string for display near the 
        /// top of the programmable block terminal window.
        /// </summary>
        /// <param name="message"></param>
        public void Highlight(string message)
        {
            Highlights += message + "\n";
        }

        /// <summary>
        /// Returns a string of console header output.
        /// </summary>
        /// <returns></returns>
        public virtual string GetConsoleHeader()
        {
            string output = "";

            output +=
                //"------------------------------------------------------\n" +
                $" {Mother.SYSTEM_NAME}           {GetIndicators()}   ({Clock.GetLoader()})\n" +
                $" {Mother.Name} *{Mother.ShortId}\n" +
                $"------------------------------------------------------\n" +
                //$"{GetTestStrings()}\n";
                "";

            if(Highlights != "")
                output +=
                    $"{GetHighlights()}" +
                    $"------------------------------------------------------\n" +
                    $"";

            return output;

                
        }

        /// <summary>
        /// Update the terminal window with the current print strings.
        /// </summary>
        public void UpdateTerminal()
        {
            string consoleOutput =
                $"{GetConsoleHeader()}\n" +
                $"{String.Join("\n", TrimmedPrintStrings.AsEnumerable().Reverse())}"
                ;

            Echo(consoleOutput);

            // reset highlights
            Highlights = "";
        }

        /// <summary>
        /// Get indicators from Mother's core modules.
        /// </summary>
        /// <returns></returns>
        public virtual string GetIndicators()
        {
            string activityMonitorIndicator = Mother.GetModule<ActivityMonitor>().ActiveBlocks.Count() > 0 ? "M" : "   ";
            string activeRequests = Mother.GetModule<IntergridMessageService>().activeRequests.Count() > 0 ? "C" : "    ";
            string almanacCount = $"{Mother.GetModule<Almanac>().Records.Count()}";
            string autopilotIndication = Mother.AutopilotEngaged ? "A" : "   ";
            string commandQueueIndicator = Mother.GetModule<CommandBus>().WaypointRoutineQueue.WaypointRoutines.Count > 0 ? "Q" : "   ";
            string waitQueueIndicator = Clock.QueuedTaskCount > 0 ? "W" : "   ";

            return String.Join(
                "  ",
                waitQueueIndicator,
                commandQueueIndicator,
                autopilotIndication,
                activeRequests,
                activityMonitorIndicator,
                almanacCount
            );
        }

        /// <summary>
        /// Add a new print string to the collection. We also save a trimmed version 
        /// for better legibility in the console. A width of 40 chars seems to fit 
        /// nicely in the console window. We also restrict the total size 
        /// of the collections for performance.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="trim"></param>
        public void Print(string message, bool trim = true)
        {
            FullPrintStrings.Add(message);

            string trimmedString = trim && message.Length > 37
                    ? message.Substring(0, 32) + "..." + message.Substring(message.Length - 5)
                    : message;

            TrimmedPrintStrings.Add(trimmedString);

            if (TrimmedPrintStrings.Count > 20)
                TrimmedPrintStrings.RemoveRange(0, TrimmedPrintStrings.Count - 20);
        }

        /// <summary>
        /// Echo a message to the console. 
        /// Accessor for Program.Echo()
        /// </summary>
        /// <param name="message"></param>
        public virtual void Echo(string message)
        {
            Mother.Program.Echo(message);
        }

        /// <summary>
        /// Clear the print string collections to refresh the console display.
        /// </summary>
        public bool ClearConsole()
        {
            FullPrintStrings.Clear();
            TrimmedPrintStrings.Clear();

            return FullPrintStrings.Count == 0 && TrimmedPrintStrings.Count == 0;
        }
    }
}
