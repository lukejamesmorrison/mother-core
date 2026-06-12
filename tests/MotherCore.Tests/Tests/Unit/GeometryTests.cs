using IngameScript;
using NUnit.Framework;
using System;
using VRageMath;

namespace MotherCore.Tests.Tests.Unit
{
    public class GeometryTests
    {
        [Test]
        public void CalculateBoundingBox_With_Empty_Input_Returns_Zero_Box()
        {
            var box = Geometry.CalculateBoundingBox(Array.Empty<Vector3D>());

            Assert.That(box.Min, Is.EqualTo(Vector3D.Zero));
            Assert.That(box.Max, Is.EqualTo(Vector3D.Zero));
        }

        [Test]
        public void CalculateBoundingBox_Returns_Min_And_Max_Across_All_Points()
        {
            var points = new[]
            {
                new Vector3D(10, -2, 3),
                new Vector3D(-5, 6, 20),
                new Vector3D(0, 4, -7),
            };

            var box = Geometry.CalculateBoundingBox(points);

            Assert.That(box.Min, Is.EqualTo(new Vector3D(-5, -2, -7)));
            Assert.That(box.Max, Is.EqualTo(new Vector3D(10, 6, 20)));
        }

        [Test]
        public void ClampToBoundingBox_Clamps_A_Point_Outside_Box()
        {
            var box = new BoundingBoxD(new Vector3D(-1, -1, -1), new Vector3D(1, 1, 1));

            var clamped = Geometry.ClampToBoundingBox(new Vector3D(5, 0.5, -4), box);

            Assert.That(clamped, Is.EqualTo(new Vector3D(1, 0.5, -1)));
        }

        [Test]
        public void GetPointOnSphere_Returns_Surface_Point_Toward_External_Point()
        {
            var sphere = new BoundingSphereD(Vector3D.Zero, 2);

            var point = Geometry.GetPointOnSphere(sphere, new Vector3D(10, 0, 0));

            Assert.That(point.X, Is.EqualTo(2).Within(1e-9));
            Assert.That(point.Y, Is.EqualTo(0).Within(1e-9));
            Assert.That(point.Z, Is.EqualTo(0).Within(1e-9));
        }

        [Test]
        public void GetAngleBetween_Returns_Zero_When_Either_Vector_Is_Zero()
        {
            var angle = Geometry.GetAngleBetween(Vector3D.Zero, new Vector3D(1, 0, 0));

            Assert.That(angle, Is.EqualTo(0));
        }

        [Test]
        public void GetAngleBetween_Returns_PiOverTwo_For_Perpendicular_Vectors()
        {
            var angle = Geometry.GetAngleBetween(new Vector3D(1, 0, 0), new Vector3D(0, 1, 0));

            Assert.That(angle, Is.EqualTo(Math.PI / 2).Within(1e-9));
        }

        [Test]
        public void GetPerpendicularVectors_Returns_Orthogonal_Unit_Vectors()
        {
            var reference = new Vector3D(2, 3, 4);
            Vector3D p1;
            Vector3D p2;

            Geometry.GetPerpendicularVectors(reference, out p1, out p2);

            Assert.That(Vector3D.Dot(reference, p1), Is.EqualTo(0).Within(1e-9));
            Assert.That(Vector3D.Dot(reference, p2), Is.EqualTo(0).Within(1e-9));
            Assert.That(Vector3D.Dot(p1, p2), Is.EqualTo(0).Within(1e-9));
            Assert.That(p1.Length(), Is.EqualTo(1).Within(1e-9));
            Assert.That(p2.Length(), Is.EqualTo(1).Within(1e-9));
        }

        [Test]
        public void ClampAngle_Clamps_To_Min_And_Max_Bounds()
        {
            Assert.That(Geometry.ClampAngle(-250f, -180f, 180f), Is.EqualTo(-180f));
            Assert.That(Geometry.ClampAngle(250f, -180f, 180f), Is.EqualTo(180f));
            Assert.That(Geometry.ClampAngle(45f, -180f, 180f), Is.EqualTo(45f));
        }

        [Test]
        public void GetVectorFromGPSString_Parses_Long_Gps_Format()
        {
            var vector = Geometry.GetVectorFromGPSString("GPS:Base:123.5:456.75:-9.25:#FFFFFF");

            Assert.That(vector.X, Is.EqualTo(123.5).Within(1e-9));
            Assert.That(vector.Y, Is.EqualTo(456.75).Within(1e-9));
            Assert.That(vector.Z, Is.EqualTo(-9.25).Within(1e-9));
        }

        [Test]
        public void GetVectorFromGPSString_Parses_Short_Gps_Format()
        {
            var vector = Geometry.GetVectorFromGPSString("1.5:2.5:3.5");

            Assert.That(vector, Is.EqualTo(new Vector3D(1.5, 2.5, 3.5)));
        }
    }
}