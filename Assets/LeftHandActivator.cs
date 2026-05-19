using UnityEngine;

public class LeftHandActivator : MonoBehaviour
{
    // TRAGE AICI SCRIPTUL DE PE MANA DREAPTA DIN INSPECTOR
    public MonoBehaviour astroLaserScript; 
    
    private bool laserStatus = true;
    private float cooldown = 0.6f;
    private float lastTimePressed;

    private void OnTriggerEnter(Collider other)
    {
        // Verificăm dacă "Ceva" de la mâna dreaptă ne-a atins ceasul
        // Verificăm numele obiectului sau pur și simplu dacă e mâna dreaptă
        if (other.name.Contains("right") && Time.time > lastTimePressed + cooldown)
        {
            ToggleRightLaser();
            lastTimePressed = Time.time;
        }
    }

    void ToggleRightLaser()
    {
        if (astroLaserScript != null)
        {
            // Inversăm starea scriptului de pe dreapta
            laserStatus = !laserStatus;
            astroLaserScript.enabled = laserStatus;

            // Opțional: Forțăm și LineRenderer-ul de pe dreapta să se stingă
            LineRenderer lr = astroLaserScript.GetComponent<LineRenderer>();
            if (lr != null)
            {
                lr.enabled = laserStatus;
            }

            Debug.Log("Comandă trimisă către Dreapta: Laser " + (laserStatus ? "PORNIT" : "OPRIT"));
        }
    }
}
