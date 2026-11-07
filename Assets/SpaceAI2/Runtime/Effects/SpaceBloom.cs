using UnityEngine;

namespace SpaceAI2
{
    [RequireComponent(typeof(Camera)), DisallowMultipleComponent]
    public class SpaceBloom : MonoBehaviour
    {
        [SerializeField, Range(0f, 2f)] private float intensity = 0.65f;
        private Material material;
        private void OnEnable()
        {
            GetComponent<Camera>().allowHDR = true;
            Shader shader = Shader.Find("Hidden/SpaceAI2/Bloom");
            if (shader != null && shader.isSupported) material = new Material(shader);
        }
        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (material == null) { Graphics.Blit(source, destination); return; }
            var a = RenderTexture.GetTemporary(Mathf.Max(1, source.width / 4), Mathf.Max(1, source.height / 4), 0, source.format);
            var b = RenderTexture.GetTemporary(a.width, a.height, 0, source.format);
            a.filterMode = b.filterMode = FilterMode.Bilinear;
            Graphics.Blit(source, a, material, 0);
            for (int i = 0; i < 3; i++)
            {
                Graphics.Blit(a, b, material, 1);
                Graphics.Blit(b, a, material, 1);
            }
            material.SetTexture("_Bloom", a);
            material.SetFloat("_Intensity", intensity);
            Graphics.Blit(source, destination, material, 2);
            RenderTexture.ReleaseTemporary(a);
            RenderTexture.ReleaseTemporary(b);
        }
        private void OnDisable() { if (material != null) Destroy(material); }
    }
}
