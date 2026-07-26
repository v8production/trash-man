using UnityEngine;

public sealed class TitanMovementFeedbackController : MonoBehaviour
{
    private const string FootGroundedSoundPath = "Sounds/SFXs/Game/Loud-thud";
    private const string MotorSoundPath = "Sounds/SFXs/Game/Small_electrical_motor";
    private const float FootGroundedShakeAmplitude = 0.08f;
    private const float FootGroundedShakeDuration = 0.18f;
    private const float FootGroundedVolumeScale = 2.0f;
    private const float MotorVolumeScale = 0.2f;
    private const float MotorActivityHoldSeconds = 0.08f;

    private AudioSource motorAudioSource;
    private float motorActiveUntil;

    public void RequestMotorActivity()
    {
        motorActiveUntil = Mathf.Max(motorActiveUntil, Time.time + MotorActivityHoldSeconds);
        EnsureMotorPlaying();
    }

    public void TickMotorAudio()
    {
        SetMotorActive(Time.time <= motorActiveUntil);
    }

    public void SetMotorActive(bool active)
    {
        if (active)
        {
            EnsureMotorPlaying();
            return;
        }

        StopMotor();
    }

    public void PlayFootGroundedFeedback()
    {
        Managers.Sound.PlayEffect(FootGroundedSoundPath, volumeScale: FootGroundedVolumeScale);
        GameCameraController.ShakeActiveCamera(FootGroundedShakeAmplitude, FootGroundedShakeDuration);
    }

    private void OnDisable()
    {
        StopMotor();
    }

    private void OnDestroy()
    {
        StopMotor();
    }

    private void StopMotor()
    {
        if (motorAudioSource != null)
            Managers.Sound.StopEffect(motorAudioSource);

        motorAudioSource = null;
    }

    private void EnsureMotorPlaying()
    {
        if (motorAudioSource == null)
            motorAudioSource = Managers.Sound.PlayControlledEffect(MotorSoundPath, loop: true, volumeScale: MotorVolumeScale);
    }
}
