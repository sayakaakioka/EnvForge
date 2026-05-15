using UnityEngine;
using UnityEngine.InputSystem;

namespace EnvForge.Mvp0
{
    [RequireComponent(typeof(AgentMotor))]
    public sealed class ManualAgentController : MonoBehaviour
    {
        private AgentMotor motor;
        private EpisodeManager episodeManager;

        private void Awake()
        {
            motor = GetComponent<AgentMotor>();
        }

        public void Configure(EpisodeManager manager)
        {
            episodeManager = manager;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                motor.SetInput(0f, 0f);
                return;
            }

            float forward = ReadAxis(keyboard.wKey.isPressed, keyboard.sKey.isPressed);
            float turn = ReadAxis(keyboard.dKey.isPressed, keyboard.aKey.isPressed);
            motor.SetInput(forward, turn);

            if (keyboard.rKey.wasPressedThisFrame)
            {
                episodeManager?.RequestManualReset();
            }
        }

        private static float ReadAxis(bool positive, bool negative)
        {
            if (positive == negative)
            {
                return 0f;
            }

            return positive ? 1f : -1f;
        }
    }
}
