using UnityEngine;

namespace CosmicCrush
{
    /// <summary>
    /// Starts the game automatically when any scene loads, so no scene setup is required.
    /// </summary>
    static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            if (Object.FindAnyObjectByType<GameManager>() != null) return;
            new GameObject("Cosmic Crush").AddComponent<GameManager>();
        }
    }
}
