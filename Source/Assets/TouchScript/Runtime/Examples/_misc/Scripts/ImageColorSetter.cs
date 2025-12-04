using TouchScript.Debugging.Loggers;
using UnityEngine;
using UnityEngine.UI;

public class ImageColorSetter : MonoBehaviour
{
    [SerializeField] private Image image;
    [SerializeField] private Color[] colors;

    public void SetColor(int index)
    {
        if (index < 0 || index >= colors.Length)
        {
            ConsoleLogger.LogWarning($"{this}: SetColor index out of bounds");
            return;
        }
        
        SetColor(colors[index]);
    }

    public void SetColor(Color value)
    {
        image.color = value;
    }
}
