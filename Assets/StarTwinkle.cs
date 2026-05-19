using UnityEngine;

public class StarTwinkle : MonoBehaviour
{
    private float baseScale;
    private float twinkleSpeed;
    private float twinkleAmount;
    private float randomOffset;

    void Start()
    {
        // Salvăm mărimea calculată inițial în PlanetariumManager
        baseScale = transform.localScale.x;
        
        // Fiecărei stele i se atribuie o viteză și o intensitate unică de pâlpâire
        twinkleSpeed = Random.Range(0.5f, 2.5f);
        twinkleAmount = Random.Range(0.1f, 0.4f); // Variază între 10% și 40% din mărime
        randomOffset = Random.Range(0f, 100f);    // Ca să nu pâlpâie toate sincronizat
    }

    void Update()
    {
        // Folosim funcția PerlinNoise pentru o fluctuație organică, naturală, nu mecanică
        float noise = Mathf.PerlinNoise(Time.time * twinkleSpeed, randomOffset);
        
        // Calculăm noua mărime
        float currentScale = baseScale - (baseScale * twinkleAmount * noise);
        
        transform.localScale = new Vector3(currentScale, currentScale, currentScale);
    }
}