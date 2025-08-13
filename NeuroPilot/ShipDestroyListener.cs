using UnityEngine;

namespace NeuroPilot
{
    public class ShipDestroyListener : MonoBehaviour
    {
        protected void OnDisable()
        {
            var autopilot = EnhancedAutoPilot.GetInstance();
            autopilot.TryAbortTravel(out _);
            autopilot.OnAutopilotMessage.Invoke("The ship has been destroyed", false);
            NotificationManager.SharedInstance.PostNotification(new NotificationData(NotificationTarget.All, $"Connection with ship is lost".ToUpper()));
        }
    }
}
