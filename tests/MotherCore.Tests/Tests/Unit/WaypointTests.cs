using IngameScript;
using NUnit.Framework;
using VRageMath;

namespace MotherCore.Tests.Tests.Unit
{
    [Category("Layer:Unit")]
    public class WaypointTests
    {
        [Test]
        public void GPSWaypoint_GetName_Returns_Name_From_Valid_Gps_String()
        {
            var waypoint = new GPSWaypoint("GPS:Outpost:123.5:456.75:-9.25:#FFAA33");

            Assert.That(waypoint.GetName(), Is.EqualTo("Outpost"));
        }

        [Test]
        public void GPSWaypoint_GetVector_Returns_Vector_From_Valid_Gps_String()
        {
            var waypoint = new GPSWaypoint("GPS:Outpost:123.5:456.75:-9.25:#FFAA33");

            Assert.That(waypoint.GetVector(), Is.EqualTo(new Vector3D(123.5, 456.75, -9.25)));
        }

        [Test]
        public void GPSWaypoint_Public_Methods_Return_Defaults_For_Invalid_Gps_Format()
        {
            var waypoint = new GPSWaypoint("GPS:Incomplete:123.5");

            Assert.That(waypoint.GetName(), Is.Null);
            Assert.That(waypoint.GetVector(), Is.EqualTo(Vector3D.Zero));
        }

        [Test]
        public void NamedWaypoint_GetName_Returns_Default_Null_When_Uninitialized()
        {
            var waypoint = new NamedWaypoint();

            Assert.That(waypoint.GetName(), Is.Null);
        }

        [Test]
        public void NamedWaypoint_GetVector_Returns_Default_Zero_When_Uninitialized()
        {
            var waypoint = new NamedWaypoint();

            Assert.That(waypoint.GetVector(), Is.EqualTo(Vector3D.Zero));
        }
    }
}