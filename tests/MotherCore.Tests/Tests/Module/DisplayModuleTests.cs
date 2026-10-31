using IngameScript;
using MotherCore.Tests.Utilities;
using MotherCore.Tests.Utilities.Factories;
using MotherCore.Tests.Utilities.Mocks;
using NUnit.Framework;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame.Utilities;

namespace MotherCore.Tests.Module
{
    [Category(TestCategories.LayerModule)]
    public class DisplayModuleTests : TestBase
    {
        [Test]
        public void LoadTextSurfaces_Registers_Text_Panel_And_Text_Surface_Provider_Surfaces_By_View_Name()
        {
            // an IMyTextPanel block
            var panel =  TextSurfaceFactory.CreatePanel(
                customName: "Status LCD",
                customData: "[surfaces]\n0=MapView\n");

            // a block with one or more IMyTextSurface surfaces
            var providerSurface0 = TextSurfaceFactory.Create();
            var providerSurface1 = TextSurfaceFactory.Create();

            var provider = new FakeProgrammableBlock(customName: "Bridge PB", customData: "[surfaces]\n1=MapView\n")
                .AddSurface(providerSurface0.Surface)
                .AddSurface(providerSurface1.Surface);

            var script = ScriptFactory()
                .WithMother()
                .WithBlock(panel.Panel)
                .WithBlock(provider)
                .Boot();

            var displayModule = script.Mother.GetModule<DisplayModule>();
            var surfaces = displayModule.GetSurfacesForDisplayType("mapview");

            Assert.That(surfaces, Is.EquivalentTo(new[]
            {
                (Sandbox.ModAPI.Ingame.IMyTextSurface)panel.Panel,
                providerSurface1.Surface,
            }));
        }

        [Test]
        public void RenderConsoleSurfaces_Writes_Only_To_Log_Surfaces_Whose_Source_Matches_This_Mother_Instance()
        {
            var matchingPanel = TextSurfaceFactory.CreatePanel(
                customName: "Local Log",
                customData: "[surfaces]\n0=LogView \"MotherCore Test\"");

            var otherPanel = TextSurfaceFactory.CreatePanel(
                customName: "Remote Log",
                customData: "[surfaces]\n0=LogView \"Other System\"");

            var script = ScriptFactory()
                .WithMother()
                .WithBlock(matchingPanel.Panel)
                .WithBlock(otherPanel.Panel)
                .Boot();

            var displayModule = script.Mother.GetModule<DisplayModule>();

            displayModule.RenderConsoleSurfaces();

            Assert.That(matchingPanel.ContentType, Is.EqualTo(VRage.Game.GUI.TextPanel.ContentType.TEXT_AND_IMAGE));
            Assert.That(otherPanel.ContentType, Is.EqualTo(VRage.Game.GUI.TextPanel.ContentType.NONE));
            Assert.That(matchingPanel.WrittenText, Does.Contain("MotherCore Test - LOG"));
            Assert.That(otherPanel.WrittenText, Is.EqualTo(string.Empty));
        }

        [Test]
        public void When_Block_Surface_Config_Changes_Display_Registrations_Are_Reloaded()
        {
            var panel = TextSurfaceFactory.CreatePanel(
                customName: "Flexible LCD",
                customData: "[surfaces]\n0=OldView\n");

            var script = ScriptFactory()
                .WithMother()
                .WithBlock(panel.Panel)
                .Boot();

            var displayModule = script.Mother.GetModule<DisplayModule>();
            var catalogue = script.Mother.GetModule<BlockCatalogue>();

            Assert.That(displayModule.GetSurfacesForDisplayType("oldview"), Contains.Item(panel.Panel));
            Assert.That(displayModule.GetSurfacesForDisplayType("newview"), Is.Empty);

            var updatedConfig = new MyIni();
            updatedConfig.TryParse("[surfaces]\n0=NewView\n");
            catalogue.BlockConfigs[panel.Panel] = updatedConfig;

            displayModule.HandleEvent(new BlockConfigChangedEvent(), panel.Panel);

            Assert.That(displayModule.GetSurfacesForDisplayType("oldview"), Is.Empty);
            Assert.That(displayModule.GetSurfacesForDisplayType("newview"), Contains.Item(panel.Panel));
        }

        [Test]
        public void Claim_Sets_Type_And_Clears_Script_Only_For_Script()
        {
            var displayModule = ScriptFactory().WithMother().Boot().Mother.GetModule<DisplayModule>();

            var scriptSurface = TextSurfaceFactory.Create();
            scriptSurface.Script = "SomeScript";
            displayModule.Claim("A", scriptSurface, ContentType.SCRIPT);

            Assert.That(scriptSurface.ContentType, Is.EqualTo(ContentType.SCRIPT));
            Assert.That(scriptSurface.Script, Is.Empty);

            var textSurface = TextSurfaceFactory.Create();
            textSurface.Script = "SomeScript";
            displayModule.Claim("B", textSurface, ContentType.TEXT_AND_IMAGE);

            Assert.That(textSurface.ContentType, Is.EqualTo(ContentType.TEXT_AND_IMAGE));
            Assert.That(textSurface.Script, Is.EqualTo("SomeScript"));
        }

        [Test]
        public void Holds_Is_True_After_Claim_And_False_After_Release_Or_Foreign_Change()
        {
            var displayModule = ScriptFactory().WithMother().Boot().Mother.GetModule<DisplayModule>();
            var surface = TextSurfaceFactory.Create();

            Assert.That(displayModule.Holds("A", surface), Is.False);

            displayModule.Claim("A", surface, ContentType.SCRIPT);
            Assert.That(displayModule.Holds("A", surface), Is.True);

            surface.Script = "Other";
            Assert.That(displayModule.Holds("A", surface), Is.False);

            displayModule.Claim("A", surface, ContentType.SCRIPT);
            surface.ContentType = ContentType.TEXT_AND_IMAGE;
            Assert.That(displayModule.Holds("A", surface), Is.False);

            displayModule.Claim("A", surface, ContentType.TEXT_AND_IMAGE);
            Assert.That(displayModule.Holds("A", surface), Is.True);

            displayModule.Release("A");
            Assert.That(displayModule.Holds("A", surface), Is.False);
        }

        [Test]
        public void Second_Claim_Replaces_The_First()
        {
            var displayModule = ScriptFactory().WithMother().Boot().Mother.GetModule<DisplayModule>();
            var surface = TextSurfaceFactory.Create();

            displayModule.Claim("A", surface, ContentType.SCRIPT);
            displayModule.Claim("A", surface, ContentType.TEXT_AND_IMAGE);

            Assert.That(surface.ContentType, Is.EqualTo(ContentType.TEXT_AND_IMAGE));
            Assert.That(displayModule.Holds("A", surface), Is.True);

            surface.ContentType = ContentType.SCRIPT;
            Assert.That(displayModule.Holds("A", surface), Is.False);
        }
    }
}
