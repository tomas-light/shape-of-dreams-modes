using UnityEngine;

namespace ConstellationViewer
{
    public sealed class ConstellationViewerMod : ModBehaviour
    {
        private ConstellationWindow _window;

        private void Update()
        {
            var stats = UI_InGame_HeroDetailWindow.softInstance;
            var ui = InGameUIManager.softInstance;
            var player = DewPlayer.local;
            var hero = player != null ? player.hero : null;
            var global = GlobalUIManager.softInstance;

            if (stats == null || ui == null || !ui.IsState("Playing") || ui.isDoingEnding ||
                hero == null || global == null)
            {
                RemoveWindow();
                return;
            }

            if (_window != null && _window.Stats != stats)
                RemoveWindow();

            if (_window == null && stats.isShown)
            {
                _window = stats.gameObject.AddComponent<ConstellationWindow>();
                _window.Initialize(stats, ui, global);
            }

            if (_window != null)
                _window.Refresh(hero);
        }

        private void OnDestroy() => RemoveWindow();

        private void RemoveWindow()
        {
            if (_window == null)
                return;

            _window.Detach();
            Destroy(_window);
            _window = null;
        }
    }
}
