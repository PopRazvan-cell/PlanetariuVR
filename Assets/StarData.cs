using UnityEngine;


public class StarData : MonoBehaviour
{
    public string starName;
    

    public string TIC_ID; // Adăugat pentru a stoca TIC ID
    public string ra;     // Adăugat pentru a stoca Ascensiunea Dreaptă
    public string dec;    // Adăugat pentru a stoca Declinația
    public string desc;   // Adăugat pentru a stoca Descrierea

    private SphereCollider col;

    void Start()
    {
        // 1. Scriptul "țipă" ca să știm că a pornit
        Debug.Log("🟢 StarData a pornit pe obiectul: " + gameObject.name);

        col = GetComponent<SphereCollider>();

        if (col == null)
        {
            Debug.LogError("🔴 EROARE: Nu există SphereCollider!");
        }
        else
        {
            col.radius = 1.5f; // 👈 mic și rezonabil
        }
    }

    
}