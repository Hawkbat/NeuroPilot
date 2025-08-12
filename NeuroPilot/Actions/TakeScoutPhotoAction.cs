
using Cysharp.Threading.Tasks;
using NeuroSdk.Actions;
using NeuroSdk.Json;
using NeuroSdk.Websocket;

namespace NeuroPilot.Actions
{
    public class TakeScoutPhotoAction : NeuroAction<SurveyorProbe>
    {
        public override string Name => "take_scout_photo";
        protected override string Description => "Take a photo with the scout from the scout launcher.";
        protected override JsonSchema Schema => new();
        protected override ExecutionResult Validate(ActionJData actionData, out SurveyorProbe realSurveyorProbe)
        {
            realSurveyorProbe = null;
            if (ScoutPatches.probeLauncher == null)
            {
                return ExecutionResult.Failure("Scout launcher is not equipped.");
            }

            realSurveyorProbe = Locator.GetProbe();
            if (realSurveyorProbe == null || !realSurveyorProbe.IsLaunched())
            {
                return ExecutionResult.Failure("Scout not found.");
            }
            return ExecutionResult.Success("Took a photo with the scout.");
        }

        protected override async UniTask ExecuteAsync(SurveyorProbe surveyorProbe)
        {
            if (surveyorProbe.IsAnchored())
            {
                ScoutPatches.probeLauncher.TakeSnapshotWithCamera(surveyorProbe.GetRotatingCamera());
            }
            else
            {
                ScoutPatches.probeLauncher.TakeSnapshotWithCamera(surveyorProbe.GetForwardCamera());
            }

            await UniTask.CompletedTask;
        }
    }
}