using UnityEngine;

/// <summary>
/// Marcheaza un buton de raspuns din quiz. Pus pe radacina butonului;
/// laserul il detecteaza prin GetComponentInParent la raycast.
/// </summary>
public class QuizOption : MonoBehaviour
{
    public int index;
    public MeshRenderer background;
}
