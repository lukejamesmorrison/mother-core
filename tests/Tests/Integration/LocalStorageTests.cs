using IngameScript;
using NUnit.Framework;
using MotherCore.Tests.Utilities;

namespace MotherCore.Tests.Integration
{
    public class LocalStorageTests
    {
        [Test]
        public void Set_Stores_A_Value_That_Get_Returns()
        {
            var script = new Script().Boot();
            var storage = script.Mother.GetModule<LocalStorage>();

            bool changed = storage.Set("ship", "Frigate");

            Assert.That(changed, Is.True);
            Assert.That(storage.Get("ship"), Is.EqualTo("Frigate"));
        }

        [Test]
        public void Get_Returns_Empty_String_For_A_Missing_Key()
        {
            var script = new Script().Boot();
            var storage = script.Mother.GetModule<LocalStorage>();

            Assert.That(storage.Get("missing"), Is.EqualTo(string.Empty));
        }

        [Test]
        public void Clear_Removes_Stored_Values()
        {
            var script = new Script().Boot();
            var storage = script.Mother.GetModule<LocalStorage>();

            storage.Set("ship", "Frigate");

            bool changed = storage.Clear();

            Assert.That(changed, Is.True);
            Assert.That(storage.Get("ship"), Is.EqualTo(string.Empty));
            Assert.That(storage.StorageString, Is.EqualTo(string.Empty));
        }

        [Test]
        public void Program_Save_Serializes_LocalStorage_Into_Program_Storage()
        {
            var script = new Script().Boot();
            var storage = script.Mother.GetModule<LocalStorage>();

            storage.Set("ship", "Frigate");
            storage.Set("status", "Ready");

            script.Program.Save();

            Assert.That(script.Program.Storage, Is.EqualTo("{\"ship\":\"Frigate\",\"status\":\"Ready\"}"));
            Assert.That(storage.StorageString, Is.EqualTo(script.Program.Storage));
        }

        [Test]
        public void Boot_Loads_Existing_Program_Storage_Into_LocalStorage()
        {
            var script = new Script()
                .WithStorage("{\"ship\":\"Frigate\",\"status\":\"Ready\"}")
                .Boot();

            var storage = script.Mother.GetModule<LocalStorage>();

            Assert.That(storage.Get("ship"), Is.EqualTo("Frigate"));
            Assert.That(storage.Get("status"), Is.EqualTo("Ready"));
        }

        [Test]
        public void Set_Command_Stores_A_Key_And_Value()
        {
            var script = new Script().Boot();
            var storage = script.Mother.GetModule<LocalStorage>();

            script.Bus.RunTerminalCommand("set ship Frigate");
            script.Clock.RunToIdle();

            Assert.That(storage.Get("ship"), Is.EqualTo("Frigate"));
        }

        [Test]
        public void Get_Command_Prints_The_Stored_Value()
        {
            var script = new Script().Boot();
            var storage = script.Mother.GetModule<LocalStorage>();
            var terminal = script.Mother.GetModule<Terminal>();
            var echo = script.CaptureEcho();

            storage.Set("ship", "Frigate");

            script.Bus.RunTerminalCommand("get ship");
            script.Clock.RunToIdle();
            terminal.UpdateTerminal();

            echo.ShouldHavePrinted("Frigate");
        }
    }
}