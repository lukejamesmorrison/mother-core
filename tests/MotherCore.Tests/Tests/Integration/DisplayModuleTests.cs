using IngameScript;
using MotherCore.Tests.Utilities;
using MotherCore.Tests.Utilities.Factories;
using MotherCore.Tests.Utilities.Mocks;
using NUnit.Framework;
using VRage.Game.ModAPI.Ingame.Utilities;

namespace MotherCore.Tests.Integration
{
    public class DisplayModuleTests
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

            var script = new Script()
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

            var script = new Script()
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

            var script = new Script()
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
    }
}