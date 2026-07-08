using UnityEngine;

public class logoLight : MonoBehaviour
{
    public float maxIntensity = 4.5f;
    public float offDuration = 3f;    // 꺼져있는 시간 (초)
    public float fadeDuration = 0.5f; // 켜지고 꺼지는 데 걸리는 시간 (초)

    private Material mat;
    private float timer = 0f;
    private enum State { Off, FadeIn, FadeOut }
    private State state = State.Off;

    void Start()
    {
        mat = GetComponent<Renderer>().material;
    }

    void Update()
    {
        timer += Time.deltaTime;

        if (state == State.Off && timer >= offDuration)
        {
            state = State.FadeIn;
            timer = 0f;
        }
        else if (state == State.FadeIn)
        {
            float intensity = Mathf.Lerp(0f, maxIntensity, timer / fadeDuration);
            mat.SetColor("_EmissionColor", Color.white * intensity);
            if (timer >= fadeDuration)
            {
                state = State.FadeOut;
                timer = 0f;
            }
        }
        else if (state == State.FadeOut)
        {
            float intensity = Mathf.Lerp(maxIntensity, 0f, timer / fadeDuration);
            mat.SetColor("_EmissionColor", Color.white * intensity);
            if (timer >= fadeDuration)
            {
                state = State.Off;
                timer = 0f;
            }
        }
    }
}