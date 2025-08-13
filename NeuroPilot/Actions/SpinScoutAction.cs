
using Cysharp.Threading.Tasks;
using NeuroSdk.Actions;
using NeuroSdk.Json;
using NeuroSdk.Websocket;

namespace NeuroPilot.Actions
{
    public class SpinScoutAction : NeuroAction<SurveyorProbe>
    {
        public override string Name => "spin_scout";

        protected override string Description => "Spin the scout.";
        protected override JsonSchema Schema => new();
        protected override ExecutionResult Validate(ActionJData actionData, out SurveyorProbe realSurveyorProbe)
        {
            realSurveyorProbe = Locator.GetProbe();
            if (realSurveyorProbe == null || !realSurveyorProbe.IsAnchored())
            {
                return ExecutionResult.Failure("Scout not found or not anchored. Please wait until the probe has landed after launching it before trying to spin it.");
            }
            return ExecutionResult.Success("Scout is spinning.");
        }

        protected override async UniTask ExecuteAsync(SurveyorProbe surveyorProbe)
        {
            await ScoutPatches.TurnSurveyorProbeAsync(surveyorProbe, "right", 12);
        }
    }
}