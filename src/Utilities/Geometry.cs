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
    /// Utility class for geometric calculations.
    /// </summary>
    public class Geometry
    {
        /// <summary>
        /// Calculates the bounding box of a set of 3D points.
        /// </summary>
        /// <param name="positions"></param>
        /// <returns></returns>
        public static BoundingBoxD CalculateBoundingBox(IEnumerable<Vector3D> positions)
        {
            if (!positions.Any())
                return new BoundingBoxD(Vector3D.Zero, Vector3D.Zero);

            double maxValue = double.MaxValue;
            double minValue = double.MinValue;

            Vector3D min = new Vector3D(maxValue, maxValue, maxValue);
            Vector3D max = new Vector3D(minValue, minValue, minValue);

            foreach (var position in positions)
            {
                min = Vector3D.Min(min, position);
                max = Vector3D.Max(max, position);
            }

            return new BoundingBoxD(min, max);
        }

        /// <summary>
        /// Get the closest point on a bounding box to a given point.
        /// </summary>
        /// <param name="point"></param>
        /// <param name="box"></param>
        /// <returns></returns>
        public static Vector3D ClampToBoundingBox(Vector3D point, BoundingBoxD box)
        {
            return new Vector3D(
                MathHelper.Clamp(point.X, box.Min.X, box.Max.X),
                MathHelper.Clamp(point.Y, box.Min.Y, box.Max.Y),
                MathHelper.Clamp(point.Z, box.Min.Z, box.Max.Z)
            );
        }

        /// <summary>
        /// Get the closest point on a bounding sphere to a given point.
        /// </summary>
        /// <param name="sphere"></param>
        /// <param name="externalPoint"></param>
        /// <returns></returns>
        public static Vector3D GetPointOnSphere(BoundingSphereD sphere, Vector3D externalPoint)
        {
            Vector3D direction = Vector3D.Normalize(sphere.Center - externalPoint);

            return sphere.Center - (direction * sphere.Radius);
        }

        /// <summary>
        /// Calculates the angle between two 3D vectors in radians.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static double GetAngleBetween(Vector3D a, Vector3D b)
        {
            if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
                return 0;

            return Math.Acos(MathHelper.Clamp(
                Vector3D.Dot(a, b) / (a.Length() * b.Length()), 
                -1, 
                1
            ));
        }

        /// <summary>
        /// Computes two arbitrary perpendicular vectors to a given input 
        /// vector with orthogonality.
        /// </summary>
        /// <param name="referenceVector"></param>
        /// <param name="perpendicular1"></param>
        /// <param name="perpendicular2"></param>
        public static void GetPerpendicularVectors(Vector3D referenceVector, out Vector3D perpendicular1, out Vector3D perpendicular2)
        {
            if (Math.Abs(referenceVector.X) > Math.Abs(referenceVector.Y))
                perpendicular1 = new Vector3D(-referenceVector.Z, 0, referenceVector.X);

            else
                perpendicular1 = new Vector3D(0, referenceVector.Z, -referenceVector.Y);

            perpendicular1 = Vector3D.Normalize(perpendicular1);
            perpendicular2 = Vector3D.Normalize(Vector3D.Cross(referenceVector, perpendicular1));
        }

        /// <summary>
        /// Normalizes the angle to be between -180 and 180 degrees.
        /// </summary>
        /// <param name="angle"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <returns></returns>
        public static float ClampAngle(float angle, float min, float max)
        {
            return Math.Max(min, Math.Min(max, angle));
        }

        /// <summary>
        /// Converts a GPS string into a Vector3D.
        /// 
        /// Supports long and short GPS formats:
        /// 1. Long format: "GPS:TopSecretBase:1234.56:7890.12:3456.78:#1F2B3D"
        /// 2. Short format: "1234.56:7890.12:3456.78"
        /// 
        /// </summary>
        /// <param name="gpsString"></param>
        /// <returns></returns>
        public static Vector3D GetVectorFromGPSString(string gpsString)
        {
            string[] parts = gpsString.Split(':');

            int offset = (parts[0] == "GPS") ? 2 : 0;

            return new Vector3D(
                double.Parse(parts[offset]),
                double.Parse(parts[offset + 1]),
                double.Parse(parts[offset + 2])
            );
        }

        /// <summary>
        /// Projects vectorA onto vectorB.
        /// </summary>
        /// <param name="vectorA"></param>
        /// <param name="vectorB"></param>
        /// <returns></returns>
        //public static Vector3D ProjectVector(Vector3D vectorA, Vector3D vectorB)
        //{
        //    double dot = Vector3D.Dot(vectorA, vectorB);
        //    double magSq = vectorB.LengthSquared();

        //    return (magSq < 1e-6) ? Vector3D.Zero : vectorB * (dot / magSq);
        //}
    }
}
