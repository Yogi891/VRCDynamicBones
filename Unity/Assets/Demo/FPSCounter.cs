using System;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Text))]
public class FPSCounter : MonoBehaviour
{
    const float fpsMeasurePeriod = 1.0f;

    int   fpsAccumulator = 0;
    float fpsNextMeasureTime = 0;
    Text  fpsText;
    
    void Start()
    {
        fpsNextMeasureTime = Time.realtimeSinceStartup + fpsMeasurePeriod;
        fpsText = GetComponent<Text>();
    }
    
    void Update()
    {
        fpsAccumulator++;
        if (Time.realtimeSinceStartup > fpsNextMeasureTime) {
            float fps = fpsAccumulator / fpsMeasurePeriod;
            fpsAccumulator = 0;
            fpsNextMeasureTime += fpsMeasurePeriod;
            fpsText.text = "FPS: " + Mathf.RoundToInt(fps);
        }
    }
}
