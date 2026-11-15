using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace SpaceMissionDesign.UI
{
    /// <summary>
    /// Plays the five act videos full-screen over the game.
    ///
    /// The clips live in Resources/Videos/Act1..Act5. While an act runs the game clock is
    /// frozen (Time.timeScale = 0) so the launch or flight does not advance behind the
    /// cutscene, and playback still runs in real time because the wait loops use unscaled
    /// time. Click / Space / Esc skips an act.
    /// </summary>
    public sealed class CinematicPlayer : MonoBehaviour
    {
        public static CinematicPlayer Instance { get; private set; }

        public bool IsPlaying { get; private set; }

        private Canvas canvas;
        private RawImage screen;
        private VideoPlayer player;
        private RenderTexture target;
        private Coroutine running;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            Build();
        }

        private void Build()
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;   // above every game screen
            gameObject.AddComponent<GraphicRaycaster>();

            // Opaque backdrop; its image is a raycast target, which is what stops clicks
            // from reaching the buttons of the screen underneath during an act.
            var backdrop = new GameObject("Cinematic Backdrop", typeof(RectTransform));
            backdrop.transform.SetParent(canvas.transform, false);
            var backdropRect = backdrop.GetComponent<RectTransform>();
            backdropRect.anchorMin = Vector2.zero;
            backdropRect.anchorMax = Vector2.one;
            backdropRect.offsetMin = Vector2.zero;
            backdropRect.offsetMax = Vector2.zero;
            var backdropImage = backdrop.AddComponent<Image>();
            backdropImage.color = Color.black;
            backdropImage.raycastTarget = true;

            var go = new GameObject("Cinematic Screen", typeof(RectTransform));
            go.transform.SetParent(backdrop.transform, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            screen = go.AddComponent<RawImage>();
            screen.raycastTarget = false;

            // Letterboxes the clip instead of stretching it to the window.
            var fitter = go.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 16f / 9f;

            player = gameObject.AddComponent<VideoPlayer>();
            player.playOnAwake = false;
            player.isLooping = false;
            player.renderMode = VideoRenderMode.RenderTexture;
            player.audioOutputMode = VideoAudioOutputMode.Direct;
            player.skipOnDrop = true;
            player.waitForFirstFrame = true;
            player.errorReceived += (source, message) => Debug.LogWarning("CinematicPlayer: " + message);

            canvas.enabled = false;
        }

        /// <summary>
        /// Plays Resources/Videos/<paramref name="act"/> and calls
        /// <paramref name="onDone"/> when it ends or is skipped. If the clip is missing the
        /// callback still runs, so a missing file can never stall the game.
        /// </summary>
        public void Play(string act, Action onDone = null)
        {
            if (IsPlaying)
            {
                onDone?.Invoke();
                return;
            }

            var clip = Resources.Load<VideoClip>("Videos/" + act);
            if (clip == null)
            {
                Debug.LogWarning($"CinematicPlayer: 'Resources/Videos/{act}' not found - continuing.");
                onDone?.Invoke();
                return;
            }

            running = StartCoroutine(Run(clip, onDone));
        }

        private IEnumerator Run(VideoClip clip, Action onDone)
        {
            IsPlaying = true;
            Time.timeScale = 0f;   // freeze the flight behind the cutscene

            ReleaseTarget();
            int width = Mathf.Max((int)clip.width, 16);
            int height = Mathf.Max((int)clip.height, 16);
            target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32) { name = clip.name };
            target.Create();
            screen.texture = target;

            var fitter = screen.GetComponent<AspectRatioFitter>();
            if (fitter != null) fitter.aspectRatio = width / (float)height;

            canvas.enabled = true;
            player.clip = clip;
            player.targetTexture = target;
            player.Prepare();

            while (!player.isPrepared) yield return null;
            player.Play();

            // Play out on the unscaled clock so a paused game does not stop the video.
            float started = Time.unscaledTime;
            while (!SkipRequested())
            {
                bool ended = player == null || !player.isPlaying;
                if (ended && Time.unscaledTime - started > .5f) break;
                if (Time.unscaledTime - started > (float)clip.length + 1f) break;
                yield return null;
            }

            if (player != null) player.Stop();
            canvas.enabled = false;
            ReleaseTarget();

            Time.timeScale = 1f;
            IsPlaying = false;
            running = null;
            onDone?.Invoke();
        }

        private static bool SkipRequested()
        {
            return Input.GetMouseButtonDown(0)
                || Input.GetKeyDown(KeyCode.Space)
                || Input.GetKeyDown(KeyCode.Escape)
                || Input.GetKeyDown(KeyCode.Return)
                || Input.GetKeyDown(KeyCode.KeypadEnter);
        }

        private void ReleaseTarget()
        {
            if (player != null) player.targetTexture = null;
            if (target == null) return;
            target.Release();
            Destroy(target);
            target = null;
            if (screen != null) screen.texture = null;
        }

        private void OnDestroy()
        {
            if (running != null) StopCoroutine(running);
            ReleaseTarget();
            Time.timeScale = 1f;
            if (Instance == this) Instance = null;
        }
    }
}
