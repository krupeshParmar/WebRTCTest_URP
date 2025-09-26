using UnityEngine;
using UnityEngine.UI;

public class ShowDeterministicRTOnUI : MonoBehaviour
{
    public RawImage targetImage;
    private void LateUpdate()
    {
        var tex = Shader.GetGlobalTexture("_DeterministicColor") as Texture;

        if (tex == null)
        {
            Debug.Log("_DeterministicColor is NULL");
        }
        if (tex && targetImage && targetImage.texture != tex)
            targetImage.texture = tex;  
    }
}
