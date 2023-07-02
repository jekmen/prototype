using System.Threading.Tasks;
using UnityEngine;

namespace SpaceAI.SceneTools
{
    public class SA_ObjDestroyer : MonoBehaviour
    {
        public float lifeTime;

        private void OnEnable()
        {
            Deactivate(gameObject, Random.Range(1, 4));
        }

        public async void Deactivate(GameObject gameObject, float time)
        {
            if (!Application.isPlaying) return;

            if (GetComponent<AudioSource>()) GetComponent<AudioSource>().Play();

                await Task.Delay((int)time * 1000);

            if (!Application.isPlaying) return;

            gameObject.SetActive(false);
        }
    }
}