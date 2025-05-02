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
    partial class Program
    {
        /// <summary>
        /// A simple PID controller class for controlling a mechanical system.
        /// </summary>
        /// <see href="https://en.wikipedia.org/wiki/Proportional%E2%80%93integral%E2%80%93derivative_controller"/>
        public class PID
        {
            /// <summary>
            /// The proportional gain of the controller.
            /// </summary>
            public double Kp { get; private set; } = 0;

            /// <summary>
            /// The integral gain of the controller.
            /// </summary>
            public double Ki { get; private set; } = 0;

            /// <summary>
            /// The derivative gain of the controller.
            /// </summary>
            public double Kd { get; private set; } = 0;

            /// <summary>
            /// The current output value of the PID controller.
            /// </summary>
            public double Value { get; private set; }

            /// <summary>
            /// The time step used for the PID calculations. This is based on ticks.
            /// Update1 => 1/60
            /// Update10 => 1/6
            /// Update100 => 1/0.6
            /// </summary>
            double TimeStep = 1 / 6;

            /// <summary>
            /// The inverse of the time step used for the PID calculations.
            /// </summary>
            double InverseTimeStep = 6;

            /// <summary>
            /// The sum of all errors (Integral term) used to prevent integral windup.
            /// </summary>
            double ErrorSum = 0;

            /// <summary>
            /// The last error value used for the derivative term.
            /// </summary>
            double LastError = 0;

            /// <summary>
            /// Flag to indicate if this is the first run of the PID controller.
            /// </summary>
            bool FirstRun = true;

            /// <summary>
            /// Constructor. We initialize with gains.
            /// </summary>
            /// <param name="kp"></param>
            /// <param name="ki"></param>
            /// <param name="kd"></param>
            public PID(double kp, double ki, double kd)
            {
                Kp = kp;
                Ki = ki;
                Kd = kd;
            }

            /// <summary>
            /// Calculates the PID output based on the error value.
            /// </summary>
            /// <param name="error"></param>
            /// <returns></returns>
            /// <exception cref="InvalidOperationException"></exception>
            public double Control(double error)
            {
                if (FirstRun)
                {
                    FirstRun = false;
                    LastError = error;
                    ErrorSum = 0;
                }

                // Clamp the error sum to prevent integral windup
                double integralLimit = 1000.0;
                ErrorSum += error * TimeStep;
                ErrorSum = MathHelper.Clamp(ErrorSum, -integralLimit, integralLimit);

                // Calculate the derivative term, ensuring InverseTimeStep is set
                if (InverseTimeStep == 0)
                    throw new InvalidOperationException("Inverse time step cannot be zero.");

                double errorDerivative = (error - LastError) * InverseTimeStep;
                LastError = error;

                // Calculate the PID output
                Value = (Kp * error) + (Ki * ErrorSum) + (Kd * errorDerivative);

                // Check for NaN or infinity and reset if found
                if (double.IsNaN(Value) || double.IsInfinity(Value))
                    Value = 0;

                return Value;
            }

            /// <summary>
            /// Calculates the PID output based on the error value and time step.
            /// </summary>
            /// <param name="error"></param>
            /// <param name="timeStep"></param>
            /// <returns></returns>
            public double Control(double error, double timeStep)
            {
                if (timeStep != TimeStep)
                    TimeStep = timeStep;
                    InverseTimeStep = 1 / TimeStep;

                return Control(error);
            }

            /// <summary>
            /// Resets the PID controller to its initial state.
            /// </summary>
            public void Reset()
            {
                ErrorSum = 0;
                LastError = 0;
                FirstRun = true;
                Value = 0;
            }
        }
    }
}
