using IngameScript;
using NUnit.Framework;
using System.Collections.Generic;
using VRage.Game.ModAPI.Ingame.Utilities;

namespace MotherCore.Tests.Tests
{
    public class DisplayTypeResolverTests
    {
        // ----------------------------------------------------------------
        // Helpers
        // ----------------------------------------------------------------

        static MyIni BuildConfig(params string[] surfaceValues)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[surfaces]");
            for (int i = 0; i < surfaceValues.Length; i++)
                sb.AppendLine($"{i}={surfaceValues[i]}");

            var ini = new MyIni();
            ini.TryParse(sb.ToString());
            return ini;
        }

        // ----------------------------------------------------------------
        // Unquoted view name — no parameter
        // ----------------------------------------------------------------

        [Test]
        public void It_Parses_A_Simple_View_Name()
        {
            MyIni config = BuildConfig("MainMenu");
            List<SurfaceEntry> entries = DisplayTypeResolver.GetSurfaceEntries(config);

            Assert.That(entries.Count, Is.EqualTo(1));
            Assert.That(entries[0].Index,    Is.EqualTo(0));
            Assert.That(entries[0].ViewName, Is.EqualTo("MainMenu"));
            Assert.That(entries[0].Parameter, Is.Null);
        }

        // ----------------------------------------------------------------
        // Unquoted view name with a parameter
        // ----------------------------------------------------------------

        [Test]
        public void It_Parses_An_Unquoted_View_Name_With_A_Parameter()
        {
            MyIni config = BuildConfig("RotorView TestRotor");
            List<SurfaceEntry> entries = DisplayTypeResolver.GetSurfaceEntries(config);

            Assert.That(entries[0].ViewName,  Is.EqualTo("RotorView"));
            Assert.That(entries[0].Parameter, Is.EqualTo("TestRotor"));
        }

        // ----------------------------------------------------------------
        // Quoted view name with spaces — no parameter
        // ----------------------------------------------------------------

        [Test]
        public void It_Parses_A_Quoted_View_Name_With_Spaces()
        {
            MyIni config = BuildConfig("\"Main Menu\"");
            List<SurfaceEntry> entries = DisplayTypeResolver.GetSurfaceEntries(config);

            Assert.That(entries[0].ViewName,  Is.EqualTo("Main Menu"));
            Assert.That(entries[0].Parameter, Is.Null);
        }

        // ----------------------------------------------------------------
        // Quoted view name with spaces and an unquoted parameter
        // ----------------------------------------------------------------

        [Test]
        public void It_Parses_A_Quoted_View_Name_With_A_Parameter()
        {
            MyIni config = BuildConfig("\"Main Menu\" SomeParam");
            List<SurfaceEntry> entries = DisplayTypeResolver.GetSurfaceEntries(config);

            Assert.That(entries[0].ViewName,  Is.EqualTo("Main Menu"));
            Assert.That(entries[0].Parameter, Is.EqualTo("SomeParam"));
        }

        // ----------------------------------------------------------------
        // Quoted view name with spaces and a quoted parameter
        // ----------------------------------------------------------------

        [Test]
        public void It_Parses_A_Quoted_View_Name_With_A_Quoted_Parameter()
        {
            MyIni config = BuildConfig("\"Main Menu\" \"Mother OS\"");
            List<SurfaceEntry> entries = DisplayTypeResolver.GetSurfaceEntries(config);

            Assert.That(entries[0].ViewName,  Is.EqualTo("Main Menu"));
            Assert.That(entries[0].Parameter, Is.EqualTo("Mother OS"));
        }

        // ----------------------------------------------------------------
        // Multiple surfaces parsed independently
        // ----------------------------------------------------------------

        [Test]
        public void It_Parses_Multiple_Surface_Entries()
        {
            MyIni config = BuildConfig("\"Main Menu\"", "RotorView TestRotor");
            List<SurfaceEntry> entries = DisplayTypeResolver.GetSurfaceEntries(config);

            Assert.That(entries.Count, Is.EqualTo(2));

            Assert.That(entries[0].Index,    Is.EqualTo(0));
            Assert.That(entries[0].ViewName, Is.EqualTo("Main Menu"));

            Assert.That(entries[1].Index,    Is.EqualTo(1));
            Assert.That(entries[1].ViewName, Is.EqualTo("RotorView"));
            Assert.That(entries[1].Parameter, Is.EqualTo("TestRotor"));
        }

        // ----------------------------------------------------------------
        // Empty and invalid entries are skipped
        // ----------------------------------------------------------------

        [Test]
        public void It_Skips_Empty_Surface_Values()
        {
            MyIni config = BuildConfig("");
            List<SurfaceEntry> entries = DisplayTypeResolver.GetSurfaceEntries(config);

            Assert.That(entries.Count, Is.EqualTo(0));
        }

        [Test]
        public void It_Skips_Non_Integer_Keys()
        {
            var ini = new MyIni();
            ini.TryParse("[surfaces]\nname=MainMenu\n");
            List<SurfaceEntry> entries = DisplayTypeResolver.GetSurfaceEntries(ini);

            Assert.That(entries.Count, Is.EqualTo(0));
        }
    }
}
