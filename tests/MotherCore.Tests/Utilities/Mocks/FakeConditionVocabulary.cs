using IngameScript;
using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame;

namespace MotherCore.Tests.Utilities.Mocks
{
    /// <summary>
    /// Stands in for the extension modules that own block vocabulary in a real
    /// script (Mother OS, MAPS), so the condition engine can be tested in Core
    /// without any of them. It registers a small, typical slice of it.
    /// </summary>
    public class FakeConditionVocabulary : BaseExtensionModule
    {
        public FakeConditionVocabulary(Mother mother) : base(mother) { }

        public override void Boot()
        {
            Block<IMyBatteryBlock>().Number("charge", b => b.CurrentStoredPower / b.MaxStoredPower);
            Block<IMyGasTank>().Number("fill", b => b.FilledRatio);
            Block<IMyShipController>()
                .Number("speed", b => b.GetShipSpeed())
                .Number("altitude", b =>
                {
                    double elevation;
                    return b.TryGetPlanetElevation(MyPlanetElevation.Surface, out elevation) ? elevation : double.NaN;
                });
            Block<IMyLandingGear>().Status("status", b => b.LockMode);
            Block<IMyShipConnector>().Status("status", b => b.Status);

            RegisterPredicate("low", "charge < 20%|fill < 15%", "5%");
            RegisterPredicate("critical", "charge < 10%|fill < 10%", "3%");
            RegisterPredicate("full", "charge > 98%|fill > 98%");
            RegisterPredicate("locked", "status == locked");
            RegisterPredicate("unlocked", "status == unlocked");
        }
    }
}
