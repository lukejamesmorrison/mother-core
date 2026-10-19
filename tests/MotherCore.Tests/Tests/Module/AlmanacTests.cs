using IngameScript;
using NUnit.Framework;
using MotherCore.Tests.Utilities;
using System;
using System.Collections.Generic;
using System.Reflection;
using VRageMath;

namespace MotherCore.Tests.Module
{
    [Category(TestCategories.LayerModule)]
    public class AlmanacTests : TestBase
    {

        // --- Construction ---

        [Test]
        public void It_Can_Be_Accessed_Via_Mother()
        {
            var almanac = ModuleFactory<Almanac>().Boot();

            Assert.That(almanac, Is.Not.Null);
        }

        // --- GetRecord ---

        [Test]
        public void GetRecord_Returns_Null_When_Record_Does_Not_Exist()
        {
            var almanac = ModuleFactory<Almanac>().Boot();
            var result = almanac.GetRecord("nonexistent");

            Assert.That(result, Is.Null);
        }

        [Test]
        public void GetRecord_Returns_Record_By_Id()
        {
            var almanac = ModuleFactory<Almanac>().Boot();

            var record = new AlmanacRecord("ship-001", "grid", new Vector3D(10, 20, 30), 0)
            {
                UnicastId = 1
            };
            almanac.AddRecord(record);

            var result = almanac.GetRecord("ship-001");

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Id, Is.EqualTo("ship-001"));
        }

        [Test]
        public void GetRecord_Returns_Record_By_DisplayName()
        {
            var almanac = ModuleFactory<Almanac>().Boot();

            var record = new AlmanacRecord("ship-002", "grid", new Vector3D(0, 0, 0), 0)
            {
                UnicastId = 2,
                DisplayName = "Frigate"
            };
            almanac.AddRecord(record);

            var result = almanac.GetRecord("Frigate");

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Id, Is.EqualTo("ship-002"));
        }

        // --- AddRecord ---

        [Test]
        public void AddRecord_Adds_New_Record()
        {
            var almanac = ModuleFactory<Almanac>().Boot();
            int countBefore = almanac.Records.Count;

            var record = new AlmanacRecord("waypoint-A", "waypoint", new Vector3D(100, 200, 300));
            almanac.AddRecord(record);

            Assert.That(almanac.Records.Count, Is.EqualTo(countBefore + 1));
        }

        [Test]
        public void AddRecord_Does_Not_Duplicate_Existing_Record()
        {
            var almanac = ModuleFactory<Almanac>().Boot();
            var record = new AlmanacRecord("waypoint-B", "waypoint", new Vector3D(0, 0, 0));
            almanac.AddRecord(record);

            int countAfterFirst = almanac.Records.Count;

            // Add same record again — same Id, same or older timestamp
            var duplicate = new AlmanacRecord("waypoint-B", "waypoint", new Vector3D(1, 2, 3));
            almanac.AddRecord(duplicate);

            Assert.That(almanac.Records.Count, Is.EqualTo(countAfterFirst));
        }

        [Test]
        public void AddRecord_Replaces_Existing_Record_When_New_Record_Is_Newer()
        {
            var almanac = ModuleFactory<Almanac>().Boot();
            var original = new AlmanacRecord("ship-003", "grid", new Vector3D(0, 0, 0), 10f)
            {
                UnicastId = 3
            };
            almanac.AddRecord(original);

            var newer = new AlmanacRecord("ship-003", "grid", new Vector3D(50, 50, 50), 20f)
            {
                UnicastId = 3
            };
            newer.UpdatedAt = DateTime.Now.AddSeconds(1);
            almanac.AddRecord(newer);

            var result = almanac.GetRecord("ship-003");
            Assert.That(result.Speed, Is.EqualTo(20f));
            Assert.That(result.Position.X, Is.EqualTo(50).Within(0.001));
        }

        [Test]
        public void AddRecord_Does_Not_Replace_Existing_Record_When_New_Record_Is_Older()
        {
            var almanac = ModuleFactory<Almanac>().Boot();
            var original = new AlmanacRecord("ship-004", "grid", new Vector3D(99, 0, 0), 15f)
            {
                UnicastId = 4
            };
            almanac.AddRecord(original);

            var older = new AlmanacRecord("ship-004", "grid", new Vector3D(1, 1, 1), 5f)
            {
                UnicastId = 4
            };
            older.UpdatedAt = DateTime.Now.AddSeconds(-60);
            almanac.AddRecord(older);

            var result = almanac.GetRecord("ship-004");
            Assert.That(result.Speed, Is.EqualTo(15f));
        }

        // --- GetRecordsByType ---

        [Test]
        public void GetRecordsByType_Returns_Only_Records_Of_Requested_Type()
        {
            var almanac = ModuleFactory<Almanac>().Boot();
            almanac.Clear();

            var waypoint = new AlmanacRecord("wp-1", "waypoint", new Vector3D(0, 0, 0));
            var grid = new AlmanacRecord("grid-1", "grid", new Vector3D(0, 0, 0), 0f) { UnicastId = 10 };

            almanac.AddRecord(waypoint);
            almanac.AddRecord(grid);

            var waypoints = almanac.GetRecordsByType("waypoint");
            var grids = almanac.GetRecordsByType("grid");

            Assert.That(waypoints, Has.All.Matches<AlmanacRecord>(r => r.EntityType == "waypoint"));
            Assert.That(grids, Has.All.Matches<AlmanacRecord>(r => r.EntityType == "grid"));
        }

        [Test]
        public void GetRecordsByType_Returns_Empty_List_When_No_Matching_Records()
        {
            var almanac = ModuleFactory<Almanac>().Boot();
            almanac.Clear();

            var results = almanac.GetRecordsByType("waypoint");

            Assert.That(results, Is.Empty);
        }

        // --- Clear ---

        [Test]
        public void Clear_Removes_All_Records()
        {
            var almanac = ModuleFactory<Almanac>().Boot();
            almanac.AddRecord(new AlmanacRecord("wp-2", "waypoint", new Vector3D(0, 0, 0)));
            almanac.AddRecord(new AlmanacRecord("wp-3", "waypoint", new Vector3D(0, 0, 0)));

            almanac.Clear();

            Assert.That(almanac.Records, Is.Empty);
        }

        // --- UpdateCurrentPosition ---

        [Test]
        public void UpdateCurrentPosition_Creates_Record_For_Own_Grid()
        {
            var almanac = ModuleFactory<Almanac>().Boot();
            string ownId = $"{almanac.Mother.Id}";

            // Remove any pre-existing self record to test creation path
            almanac.Records.RemoveAll(r => r.Id == ownId);

            almanac.UpdateCurrentPosition();

            var record = almanac.GetRecord(ownId);
            Assert.That(record, Is.Not.Null);
            Assert.That(record.EntityType, Is.EqualTo("grid"));
        }

        [Test]
        public void UpdateCurrentPosition_Updates_Existing_Own_Grid_Record()
        {
            var almanac = ModuleFactory<Almanac>().Boot();
            string ownId = $"{almanac.Mother.Id}";

            // Ensure a record exists first
            almanac.UpdateCurrentPosition();
            var first = almanac.GetRecord(ownId);
            DateTime firstUpdate = first.UpdatedAt;

            // Slight delay to ensure UpdatedAt advances
            System.Threading.Thread.Sleep(10);
            almanac.UpdateCurrentPosition();

            var updated = almanac.GetRecord(ownId);
            Assert.That(updated.UpdatedAt, Is.GreaterThan(firstUpdate));
        }

        [Test]
        public void Boot_Registers_Two_Scheduled_Tasks_With_Clock()
        {
            var almanac = ModuleFactory<Almanac>().Boot();

            // After Script.Boot(), the clock is NOT reset, so Almanac's tasks remain.
            // Clock itself registers UpdateLoader (1), Almanac adds UpdateCurrentPosition (2)
            // and RemoveStaleRecords (3).
            var field = typeof(Clock).GetField("SystemTasks",
                BindingFlags.NonPublic | BindingFlags.Instance);

            var tasks = (System.Collections.IList)field.GetValue(almanac.Mother.GetModule<Clock>());

            Assert.That(tasks.Count, Is.GreaterThanOrEqualTo(3),
                "Clock should have at least 3 system tasks: UpdateLoader + UpdateCurrentPosition + RemoveStaleRecords.");
        }

        // --- UpdateOrCreateFromMessage ---

        [Test]
        public void UpdateOrCreateFromMessage_Creates_New_Record_From_Remote_Grid()
        {
            var almanac = ModuleFactory<Almanac>().Boot();
            almanac.Clear();

            var result = almanac.UpdateOrCreateFromMessage(
                recordId: "remote-001",
                originId: 999L,
                name: "RemoteShip",
                position: new Vector3D(500, 500, 500),
                speed: 30f,
                channels: new HashSet<string> { "*" },
                isOnConstruct: false
            );

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Id, Is.EqualTo("remote-001"));
            Assert.That(result.UnicastId, Is.EqualTo(999L));
            Assert.That(result.DisplayName, Is.EqualTo("RemoteShip"));
            Assert.That(result.Speed, Is.EqualTo(30f));
        }

        [Test]
        public void UpdateOrCreateFromMessage_Updates_Position_And_Speed_For_Existing_Record()
        {
            var almanac = ModuleFactory<Almanac>().Boot();
            almanac.UpdateOrCreateFromMessage(
                "remote-002", 888L, "Rover",
                new Vector3D(0, 0, 0), 0f,
                new HashSet<string> { "*" }, false
            );

            almanac.UpdateOrCreateFromMessage(
                "remote-002", 888L, "Rover",
                new Vector3D(100, 200, 300), 55f,
                new HashSet<string> { "*" }, false
            );

            var record = almanac.GetRecord("remote-002");
            Assert.That(record.Speed, Is.EqualTo(55f));
            Assert.That(record.Position.X, Is.EqualTo(100).Within(0.001));
        }

        [Test]
        public void UpdateOrCreateFromMessage_Sets_Friendly_IFF_For_Non_Public_Channel()
        {
            var almanac = ModuleFactory<Almanac>().Boot();

            almanac.Clear();

            var result = almanac.UpdateOrCreateFromMessage(
                "remote-003", 777L, "FriendlyShip",
                new Vector3D(0, 0, 0), 0f,
                channels: new HashSet<string> { "secure-channel" },
                isOnConstruct: false
            );

            Assert.That(result.IFFCode, Is.EqualTo(AlmanacRecord.TransponderCode.Friendly));
        }

        [Test]
        public void UpdateOrCreateFromMessage_Sets_Neutral_IFF_For_Public_Channel()
        {
            var almanac = ModuleFactory<Almanac>().Boot();

            almanac.Clear();

            var result = almanac.UpdateOrCreateFromMessage(
                "remote-004", 666L, "UnknownShip",
                new Vector3D(0, 0, 0), 0f,
                channels: new HashSet<string> { "*" },
                isOnConstruct: false
            );

            Assert.That(result.IFFCode, Is.EqualTo(AlmanacRecord.TransponderCode.Neutral));
        }

        [Test]
        public void UpdateOrCreateFromMessage_Sets_Construct_IFF_When_On_Same_Construct()
        {
            var almanac = ModuleFactory<Almanac>().Boot();

            almanac.Clear();

            var result = almanac.UpdateOrCreateFromMessage(
                "remote-005", 555L, "SiblingScript",
                new Vector3D(0, 0, 0), 0f,
                channels: new HashSet<string> { "*" },
                isOnConstruct: true
            );

            Assert.That(result.IFFCode, Is.EqualTo(AlmanacRecord.TransponderCode.Construct));
        }

        [Test]
        public void UpdateOrCreateFromMessage_Merges_Channels_For_Existing_Record()
        {
            var almanac = ModuleFactory<Almanac>().Boot();

            almanac.UpdateOrCreateFromMessage(
                "remote-006", 444L, "MultiChannel",
                new Vector3D(0, 0, 0), 0f,
                channels: new HashSet<string> { "ch-1" },
                isOnConstruct: false
            );

            almanac.UpdateOrCreateFromMessage(
                "remote-006", 444L, "MultiChannel",
                new Vector3D(0, 0, 0), 0f,
                channels: new HashSet<string> { "ch-2" },
                isOnConstruct: false
            );

            var record = almanac.GetRecord("remote-006");

            Assert.That(record.Channels, Contains.Item("ch-1"));
            Assert.That(record.Channels, Contains.Item("ch-2"));
        }

        [Test]
        public void UpdateOrCreateFromMessage_Sets_Forward_And_Up_On_New_Record()
        {
            var almanac = ModuleFactory<Almanac>().Boot();

            almanac.Clear();

            var forward = new Vector3D(1, 0, 0);
            var up = new Vector3D(0, 1, 0);

            var result = almanac.UpdateOrCreateFromMessage(
                "remote-007", 333L, "OrientedShip",
                new Vector3D(0, 0, 0), 0f,
                channels: new HashSet<string> { "*" },
                isOnConstruct: false,
                forward: forward,
                up: up
            );

            Assert.That(result.Forward.X, Is.EqualTo(1).Within(0.001));
            Assert.That(result.Up.Y, Is.EqualTo(1).Within(0.001));
        }

        // --- Stale record removal ---

        [Test]
        public void Stale_Grid_Records_Are_Removed_Via_Scheduled_Cleanup()
        {
            var almanac = ModuleFactory<Almanac>().Boot();

            almanac.Clear();

            var stale = new AlmanacRecord("stale-grid", "grid", new Vector3D(0, 0, 0), 0f)
            {
                UnicastId = 1
            };
            stale.UpdatedAt = DateTime.Now.AddSeconds(-400);

            almanac.Records.Add(stale);

            // Invoke the private RemoveStaleRecords method directly
            var method = typeof(Almanac).GetMethod("RemoveStaleRecords",
                BindingFlags.NonPublic | BindingFlags.Instance);

            method.Invoke(almanac, null);

            Assert.That(almanac.GetRecord("stale-grid"), Is.Null);
        }

        [Test]
        public void Waypoint_Records_Are_Never_Removed_By_Stale_Cleanup()
        {
            var almanac = ModuleFactory<Almanac>().Boot();

            almanac.Clear();

            var waypoint = new AlmanacRecord("old-waypoint", "waypoint", new Vector3D(0, 0, 0));
            waypoint.UpdatedAt = DateTime.Now.AddSeconds(-9999);

            almanac.Records.Add(waypoint);

            var method = typeof(Almanac).GetMethod("RemoveStaleRecords",
                BindingFlags.NonPublic | BindingFlags.Instance);

            method.Invoke(almanac, null);

            Assert.That(almanac.GetRecord("old-waypoint"), Is.Not.Null);
        }

        [Test]
        public void Own_Grid_Record_Is_Never_Removed_By_Stale_Cleanup()
        {
            var almanac = ModuleFactory<Almanac>().Boot();
            string ownId = $"{almanac.Mother.Id}";

            almanac.UpdateCurrentPosition();

            var own = almanac.GetRecord(ownId);
            own.UpdatedAt = DateTime.Now.AddSeconds(-9999);

            var method = typeof(Almanac).GetMethod("RemoveStaleRecords",
                BindingFlags.NonPublic | BindingFlags.Instance);

            method.Invoke(almanac, null);

            Assert.That(almanac.GetRecord(ownId), Is.Not.Null);
        }
    }
}
