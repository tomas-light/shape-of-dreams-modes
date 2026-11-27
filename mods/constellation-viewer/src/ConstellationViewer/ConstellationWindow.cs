using System;
using ShapeOfDreamsMods;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ConstellationViewer
{
    internal sealed class ConstellationWindow : MonoBehaviour
    {
        private static readonly Color PanelColor = new Color32(24, 26, 42, 255);
        private static readonly Color RowColor = new Color32(41, 44, 66, 255);
        private static readonly Color HighlightColor = new Color32(62, 64, 100, 255);
        private static readonly Color MutedText = new Color32(190, 194, 217, 255);
        private static readonly Color Accent = new Color32(197, 182, 250, 255);

        private InGameUIManager _ui;
        private GlobalUIManager _global;
        private BackHandler _backHandler;
        private DewInputTrigger _menuInput;
        private Hero _hero;
        private HeroLoadoutData _shownLoadout;
        private RectTransform _launcher;
        private RectTransform _overlay;
        private TextMeshProUGUI _detailTitle;
        private TextMeshProUGUI _description;
        private ScrollRect _detailScroll;
        private ConstellationRow _hoveredRow;
        private bool _inputBlocked;

        public UI_InGame_HeroDetailWindow Stats { get; private set; }

        private static string Localize(string key) => TooltipText.Get(TooltipText.GetCulture(DewSave.profileMain?.language), key);

        public void Initialize(UI_InGame_HeroDetailWindow stats, InGameUIManager ui, GlobalUIManager global)
        {
            Stats = stats;
            _ui = ui;
            _global = global;
            _backHandler = global.AddBackHandler(this, 3000, Close);
            _menuInput = new DewInputTrigger
            {
                owner = this,
                binding = () => DewSave.profileMain.controls.menu,
                isValidCheck = () => _overlay != null,
                priority = -100
            };

            _launcher = CreateButton("Constellations", stats.transform, Open);
            _launcher.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            _launcher.gameObject.AddComponent<UI_GameInputPassThrough>().exceptLeftClick = true;
            _launcher.anchorMin = _launcher.anchorMax = new Vector2(1f, 1f);
            _launcher.pivot = new Vector2(0f, 1f);
            _launcher.anchoredPosition = new Vector2(8f, 0f);
            _launcher.sizeDelta = new Vector2(154f, 36f);
            var label = AddText(_launcher, Localize("Launcher"), 18, TextAlignmentOptions.Center);
            label.enableAutoSizing = true;
            label.fontSizeMin = 14;
            label.fontSizeMax = 18;
            Fill(label.rectTransform, 6f, 2f, 6f, 2f);
        }

        public void Refresh(Hero hero)
        {
            bool hasRoomForModal = CanShowModal();
            if (_overlay != null && (_hero != hero || _shownLoadout != hero.loadout ||
                !hasRoomForModal || _menuInput.down))
                Close();

            _hero = hero;
            _launcher.gameObject.SetActive(Stats.isShown && hero.loadout != null && hasRoomForModal);
        }

        private bool CanShowModal()
        {
            var messages = MessageManager.softInstance;
            return !_global.isTutorialHighlighting && _ui.isWorldDisplayed == WorldDisplayStatus.None &&
                !_ui.isScoreboardDisplayed && (messages == null || !messages.isShowingMessage);
        }

        private void Open()
        {
            if (_overlay != null || _hero == null || _hero.loadout == null || !Stats.isShown || !CanShowModal())
                return;

            _shownLoadout = _hero.loadout;
            BuildModal();
            _ui.DisablePlayingInput();
            _inputBlocked = true;
        }

        private bool Close()
        {
            if (_overlay == null)
                return false;

            _overlay.gameObject.SetActive(false);
            Destroy(_overlay.gameObject);
            _overlay = null;
            _hoveredRow = null;
            _shownLoadout = null;
            ReleaseInput();
            return true;
        }

        private void ReleaseInput()
        {
            if (_inputBlocked && _ui != null)
                _ui.EnablePlayingInput();

            _inputBlocked = false;
        }

        private void OnDisable()
        {
            Close();
            ReleaseInput();
        }

        public void Detach()
        {
            Close();
            if (_global != null && _backHandler != null)
                _global.RemoveBackHandler(_backHandler);
            _backHandler = null;

            if (_menuInput != null && InputManager.softInstance != null)
                InputManager.softInstance.RemoveTriggersByOwner(this);
            _menuInput = null;

            if (_launcher != null)
            {
                _launcher.gameObject.SetActive(false);
                Destroy(_launcher.gameObject);
                _launcher = null;
            }
        }

        private void OnDestroy() => Detach();

        private void BuildModal()
        {
            _overlay = CreateRect("Constellation Viewer", null);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(_overlay.gameObject, gameObject.scene);
            var canvas = _overlay.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var scaler = _overlay.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            _overlay.gameObject.AddComponent<GraphicRaycaster>();

            var shade = CreateRect("Backdrop", _overlay);
            Fill(shade);
            var backdrop = AddImage(shade, new Color32(6, 8, 17, 180));
            var dismiss = shade.gameObject.AddComponent<Button>();
            dismiss.targetGraphic = backdrop;
            dismiss.transition = Selectable.Transition.None;
            dismiss.navigation = new Navigation { mode = Navigation.Mode.None };
            dismiss.onClick.AddListener(() => Close());

            var panel = CreateRect("Panel", _overlay);
            panel.sizeDelta = new Vector2(960, 614);
            AddImage(panel, PanelColor);

            var title = AddText(panel, Localize("Equipped"), 34);
            TopLeft(title.rectTransform, 30, 18, 760, 56);
            title.color = Accent;

            var close = CreateButton("Close", panel, () => Close());
            TopLeft(close, 882, 22, 48, 44);
            var closeLabel = AddText(close, "×", 30, TextAlignmentOptions.Center);
            Fill(closeLabel.rectTransform);

            var listArea = CreateRect("Selected stars", panel);
            TopLeft(listArea, 30, 92, 400, 492);
            var listScroll = CreateScroll(listArea);
            var detailArea = CreateRect("Star details", panel);
            TopLeft(detailArea, 450, 92, 480, 492);
            AddImage(detailArea, new Color32(31, 34, 53, 255));

            _detailTitle = AddText(detailArea, string.Empty, 27);
            TopLeft(_detailTitle.rectTransform, 24, 20, 432, 64);
            _detailTitle.enableAutoSizing = true;
            _detailTitle.fontSizeMin = 18;
            _detailTitle.fontSizeMax = 27;

            var descriptionArea = CreateRect("Description", detailArea);
            Fill(descriptionArea, 24, 90, 24, 24);
            _detailScroll = CreateScroll(descriptionArea);
            _description = AddText(_detailScroll.content, string.Empty, 23, TextAlignmentOptions.TopLeft);
            _description.color = MutedText;

            int starCount = 0;
            if (EquippedStars.IsRestoredLoadout(_shownLoadout))
            {
                foreach (var star in _hero.Status.GetStatusEffects<StarEffect>())
                {
                    if (star == null)
                        continue;

                    var entry = new LoadoutStarItem { name = star.GetType().Name, level = star.skillLevel };
                    AddStarRow(listScroll.content, star.type, entry, star, star.strength);
                    starCount++;
                }
            }
            else
            {
                foreach (var (category, entry) in EquippedStars.Enumerate(_shownLoadout))
                {
                    var star = DewResources.GetByShortTypeName(entry.name) as StarEffect;
                    var strength = star != null ? EquippedStars.GetStrength(category, star.type) : 1f;
                    AddStarRow(listScroll.content, category, entry, star, strength);
                    starCount++;
                }
            }

            if (starCount == 0)
                AddText(listScroll.content, Localize("Empty"), 22);
            else
                _description.text = Localize("HoverHint");
        }

        private void AddStarRow(RectTransform list, StarType category, LoadoutStarItem entry, StarEffect star, float strength)
        {
            var color = Color.Lerp(Dew.GetStarCategoryColor(star != null ? star.type : category), Color.white, 0.4f);
            var name = star != null ? DewLocalization.GetStarName(entry.name) : entry.name;
            int level = star != null ? EquippedStars.GetDisplayLevel(entry, star.maxStarLevel) : entry.level;
            var row = CreateRect("Star", list);
            var background = AddImage(row, RowColor);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 58f;

            var stripe = CreateRect("Category", row);
            TopLeft(stripe, 0, 0, 4, 58);
            AddImage(stripe, color, false);

            if (star != null && star.starIcon != null)
            {
                var icon = CreateRect("Icon", row);
                TopLeft(icon, 12, 9, 40, 40);
                var image = AddImage(icon, color, false);
                image.sprite = star.starIcon;
                image.preserveAspect = true;
            }

            var label = AddText(row, name, 21);
            label.richText = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            Fill(label.rectTransform, 62, 4, 65, 4);

            if (star != null && star.maxStarLevel > 1)
            {
                var levelLabel = AddText(row, $"{level}/{star.maxStarLevel}", 17, TextAlignmentOptions.Center);
                levelLabel.color = color;
                TopLeft(levelLabel.rectTransform, 335, 0, 57, 58);
            }

            var hover = row.gameObject.AddComponent<ConstellationRow>();
            hover.Window = this;
            hover.Background = background;
            hover.Title = name;
            hover.TitleColor = color;
            hover.Description = star != null
                ? DewLocalization.ConvertDescriptionNodesToText(DewLocalization.GetStarDescription(entry.name),
                    new DewLocalization.DescriptionSettings { currentLevel = level, starStrength = strength })
                : Localize("Unavailable");
        }

        internal void ShowDetails(ConstellationRow row)
        {
            if (_overlay == null || _hoveredRow == row)
                return;

            if (_hoveredRow != null)
                _hoveredRow.Background.color = RowColor;

            _hoveredRow = row;
            row.Background.color = HighlightColor;
            // Keep the last description visible so the player can scroll a long one.
            _detailTitle.text = row.Title;
            _detailTitle.color = row.TitleColor;
            _description.text = row.Description;
            _detailScroll.StopMovement();
            _detailScroll.content.anchoredPosition = Vector2.zero;
        }

        private TextMeshProUGUI AddText(Transform parent, string text, float size,
            TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
        {
            var label = CreateRect("Text", parent).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = _global.fontBody;
            label.fontSize = size;
            label.alignment = alignment;
            label.color = Color.white;
            label.raycastTarget = false;
            label.text = text;
            return label;
        }

        private static ScrollRect CreateScroll(RectTransform area)
        {
            var scroll = area.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 38f;

            var viewport = CreateRect("Viewport", area);
            Fill(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            AddImage(viewport, Color.clear);
            scroll.viewport = viewport;

            var content = CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 5f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            return scroll;
        }

        private static RectTransform CreateButton(string name, Transform parent, Action click)
        {
            var rect = CreateRect(name, parent);
            var image = AddImage(rect, Color.white);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = button.colors;
            colors.normalColor = RowColor;
            colors.highlightedColor = HighlightColor;
            colors.pressedColor = PanelColor;
            button.colors = colors;
            button.onClick.AddListener(() => click());
            return rect;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static Image AddImage(RectTransform rect, Color color, bool raycast = true)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        private static void TopLeft(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void Fill(RectTransform rect, float left = 0, float top = 0, float right = 0, float bottom = 0)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }
    }

    internal sealed class ConstellationRow : MonoBehaviour, IPointerEnterHandler
    {
        public ConstellationWindow Window;
        public Image Background;
        public string Title;
        public Color TitleColor;
        public string Description;

        public void OnPointerEnter(PointerEventData eventData) => Window.ShowDetails(this);
    }
}
