using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class TutorialManager : MonoBehaviour
{
    [Header("Audio Settings (Nhạc nền BGM)")]
    [Tooltip("Kéo file nhạc nền .mp3 hoặc .wav của bạn thả vào ô này")]
    public AudioClip backgroundMusic;
    [Range(0f, 1f)] public float musicVolume = 0.5f;

    [Header("UI Elements")]
    public TextMeshProUGUI dialogText;
    public Button nextButton;
    public Button attackButton;
    public Button skillButton;
    public Button defendButton;
    public Button itemButton;
    public GameObject pointer;
    public GameObject victoryPanel;
    public GameObject defeatPanel;

    [Header("Pointer Positions")]
    public Transform nextBtnPos;
    public Transform attackBtnPos;
    public Transform skillBtnPos;
    public Transform defendBtnPos;
    public Transform itemBtnPos;

    [Header("HP & MP Sliders")]
    public Slider playerHPSlider;
    public Slider playerMPSlider; 
    public Slider enemyHPSlider;

    [Header("Character Animators")]
    public Animator playerAnim;
    public Animator enemyAnim;

    [Header("Speed Settings (Chỉnh tốc độ)")]
    public float textSpeed = 0.045f;
    public float animSpeed = 0.65f;
    public float dashDuration = 0.55f;

    private int currentStep = 0;
    private bool isBusy = false;
    private bool battleEnded = false;
    private bool isFreeCombat = false;
    private bool isDefending = false;

    private float playerHP = 100f;
    private float playerMP = 60f;
    private float enemyHP = 100f;

    private int potionsLeft = 2;
    private int manaPotionsLeft = 2;
    private int bombsLeft = 2;

    private int atkBuffTurns = 0;
    private int defDebuffTurns = 0;
    private int burnTurns = 0;
    private bool atkBuffAppliedThisAction;
    private bool armorBreakAppliedThisAction;

    private Vector3 playerStartPos;
    private Vector3 enemyStartPos;
    private Vector3 camStartPos;
    private RectTransform pointerTarget;
    private Canvas hudCanvas;
    private Vector3 playerHeadOffset;
    private Vector3 enemyHeadOffset;
    private Texture2D pointerTexture;
    private Sprite pointerSprite;

    private TextMeshProUGUI playerHPText;
    private TextMeshProUGUI playerMPText;
    private TextMeshProUGUI enemyHPText;
    private TextMeshProUGUI turnBannerText;
    private Coroutine typewriterCoroutine;

    private GameObject skillMenuPanel;
    private GameObject itemMenuPanel;

    private AudioSource bgmSource;
    private AudioSource sfxSource;

    void Start()
    {
        battleEnded = false;
        if (defeatPanel != null) defeatPanel.SetActive(false);
        if (playerAnim != null)
        {
            playerStartPos = playerAnim.transform.position;
            playerAnim.speed = animSpeed;
        }
        if (enemyAnim != null)
        {
            enemyStartPos = enemyAnim.transform.position;
            enemyAnim.transform.rotation = Quaternion.Euler(0, -90, 0);
            enemyAnim.speed = animSpeed;
        }
        if (Camera.main != null) camStartPos = Camera.main.transform.position;

        SetupAudioSystem();
        BuildAncientArena3D();
        SetupEnhancedUI();
        BuildSkillAndItemMenus();
        playerHeadOffset = GetHeadOffset(playerAnim);
        enemyHeadOffset = GetHeadOffset(enemyAnim);
        CreateGuidePointer();
        StartTutorialStep(0);
    }

    void Update()
    {
        if (playerAnim != null) playerAnim.speed = animSpeed;
        if (enemyAnim != null) enemyAnim.speed = animSpeed;

        if (bgmSource != null)
        {
            bgmSource.volume = musicVolume;
        }

        if (playerHPSlider != null)
        {
            playerHPSlider.maxValue = 100f;
            playerHPSlider.value = Mathf.Lerp(playerHPSlider.value, playerHP, Time.deltaTime * 6f);
        }
        if (playerMPSlider != null)
        {
            playerMPSlider.maxValue = 60f;
            playerMPSlider.value = Mathf.Lerp(playerMPSlider.value, playerMP, Time.deltaTime * 6f);
        }
        if (enemyHPSlider != null)
        {
            enemyHPSlider.maxValue = 100f;
            enemyHPSlider.value = Mathf.Lerp(enemyHPSlider.value, enemyHP, Time.deltaTime * 6f);
        }

    }

    void LateUpdate()
    {
        if (hudCanvas == null || Camera.main == null) return;
        FollowCharacterSlider(playerMPSlider, playerAnim, playerHeadOffset, 4f);
        FollowCharacterSlider(playerHPSlider, playerAnim, playerHeadOffset, 34f);
        FollowCharacterSlider(enemyHPSlider, enemyAnim, enemyHeadOffset, 18f);
        UpdateGuidePointer();
    }

    Vector3 GetHeadOffset(Animator animator)
    {
        if (animator == null) return Vector3.up * 1.5f;
        float top = animator.transform.position.y;
        foreach (Renderer renderer in animator.GetComponentsInChildren<Renderer>())
        {
            if (renderer.enabled && !(renderer is ParticleSystemRenderer))
                top = Mathf.Max(top, renderer.bounds.max.y);
        }
        Vector3 head = animator.transform.position;
        head.y = top;
        return animator.transform.InverseTransformPoint(head);
    }

    void FollowCharacterSlider(Slider slider, Animator animator, Vector3 headOffset, float rise)
    {
        if (slider == null || animator == null || !slider.gameObject.activeSelf) return;
        RectTransform parent = slider.transform.parent as RectTransform;
        if (parent == null) return;
        Vector3 screen = Camera.main.WorldToScreenPoint(animator.transform.TransformPoint(headOffset));
        if (screen.z <= 0f) return;
        Camera uiCamera = hudCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null : hudCanvas.worldCamera;
        Vector2 local;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,
            new Vector2(screen.x, screen.y), uiCamera, out local))
        {
            // Local coordinates account for Canvas scaling and the UI camera.
            RectTransform rect = slider.GetComponent<RectTransform>();
            // Reserve the top 62 UI units for the turn banner, moving the whole
            // player group together so HP and MP keep their spacing.
            float groupTop = animator == playerAnim ? 34f : 18f;
            float headY = Mathf.Min(local.y, parent.rect.yMax - 62f - groupTop - 22f);
            float safeX = Mathf.Clamp(local.x, parent.rect.xMin + 124f, parent.rect.xMax - 124f);
            rect.localPosition = new Vector3(safeX, headY + rise, 0f);
        }
    }

    void CreateGuidePointer()
    {
        if (hudCanvas == null) return;
        // Replace the old visual, which may have child offsets or animations.
        if (pointer != null) pointer.SetActive(false);
        pointer = new GameObject("TutorialGuideArrow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        pointer.transform.SetParent(hudCanvas.transform, false);
        RectTransform rect = pointer.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(22f, 22f);
        pointerTexture = new Texture2D(24, 24, TextureFormat.RGBA32, false);
        pointerTexture.filterMode = FilterMode.Bilinear;
        for (int y = 0; y < 24; y++)
            for (int x = 0; x < 24; x++)
            {
                bool inside = x >= 3 && x <= 21 && Mathf.Abs(y - 11.5f) <= (21 - x) * 0.6f;
                pointerTexture.SetPixel(x, y, inside ? Color.white : Color.clear);
            }
        pointerTexture.Apply();
        pointerSprite = Sprite.Create(pointerTexture, new Rect(0, 0, 24, 24), new Vector2(0.5f, 0.5f));
        Image arrow = pointer.GetComponent<Image>();
        arrow.sprite = pointerSprite;
        arrow.color = new Color(1f, 0.85f, 0.15f);
        arrow.raycastTarget = false;
        pointer.SetActive(false);
    }

    void UpdateGuidePointer()
    {
        if (pointer == null || !pointer.activeSelf || pointerTarget == null) return;
        RectTransform parent = pointer.transform.parent as RectTransform;
        if (parent == null) return;
        bool isNext = nextButton != null && pointerTarget == nextButton.transform;
        Vector3 targetPoint = isNext
            ? new Vector3(pointerTarget.rect.xMin, pointerTarget.rect.center.y, 0f)
            : new Vector3(pointerTarget.rect.center.x, pointerTarget.rect.yMax, 0f);
        Vector3 local = parent.InverseTransformPoint(pointerTarget.TransformPoint(targetPoint));
        float bounce = Mathf.Sin(Time.unscaledTime * 4f) * 3f;
        if (isNext)
        {
            local.x -= 17f + bounce;
            pointer.transform.localRotation = Quaternion.identity;
        }
        else
        {
            local.y += 18f + bounce;
            pointer.transform.localRotation = Quaternion.Euler(0f, 0f, -90f);
        }
        // Keep the full arrow inside the Canvas, including its animation.
        local.x = Mathf.Clamp(local.x, parent.rect.xMin + 14f, parent.rect.xMax - 14f);
        local.y = Mathf.Clamp(local.y, parent.rect.yMin + 14f, parent.rect.yMax - 14f);
        local.z = 0f;
        pointer.transform.localPosition = local;
    }

    void OnDestroy()
    {
        if (pointerSprite != null) Destroy(pointerSprite);
        if (pointerTexture != null) Destroy(pointerTexture);
    }

    void BuildAncientArena3D()
    {
        GameObject oldGround = GameObject.Find("Ground");
        if (oldGround != null) oldGround.SetActive(false);

        GameObject arenaRoot = new GameObject("AncientArena_3D");
        CreateArenaPart(PrimitiveType.Cylinder, arenaRoot.transform, new Vector3(0, -0.15f, 0), new Vector3(7.5f, 0.15f, 7.5f), new Color(0.22f, 0.24f, 0.28f));
        CreateArenaPart(PrimitiveType.Cylinder, arenaRoot.transform, new Vector3(0, -0.35f, 0), new Vector3(9.2f, 0.2f, 9.2f), new Color(0.15f, 0.16f, 0.2f));
        CreateArenaPart(PrimitiveType.Cylinder, arenaRoot.transform, new Vector3(0, -0.01f, 0), new Vector3(4.2f, 0.015f, 4.2f), new Color(0.85f, 0.68f, 0.2f));
        CreateArenaPart(PrimitiveType.Cylinder, arenaRoot.transform, new Vector3(0, 0f, 0), new Vector3(3.8f, 0.018f, 3.8f), new Color(0.28f, 0.3f, 0.36f));

        float[] angles = { 25f, 55f, 85f, 95f, 125f, 155f };
        foreach (float deg in angles)
        {
            float rad = deg * Mathf.Deg2Rad;
            Vector3 pillarPos = new Vector3(Mathf.Cos(rad) * 3.4f, 0f, Mathf.Sin(rad) * 3.4f);
            CreateArenaPart(PrimitiveType.Cube, arenaRoot.transform, pillarPos + new Vector3(0, 0.15f, 0), new Vector3(0.55f, 0.3f, 0.55f), new Color(0.2f, 0.22f, 0.26f));
            CreateArenaPart(PrimitiveType.Cylinder, arenaRoot.transform, pillarPos + new Vector3(0, 1.2f, 0), new Vector3(0.38f, 1.0f, 0.38f), new Color(0.32f, 0.34f, 0.4f));
            CreateArenaPart(PrimitiveType.Cube, arenaRoot.transform, pillarPos + new Vector3(0, 2.25f, 0), new Vector3(0.55f, 0.2f, 0.55f), new Color(0.2f, 0.22f, 0.26f));
            CreateArenaPart(PrimitiveType.Sphere, arenaRoot.transform, pillarPos + new Vector3(0, 2.55f, 0), new Vector3(0.28f, 0.28f, 0.28f), new Color(1f, 0.55f, 0.1f));
        }
    }

    GameObject CreateArenaPart(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Color color)
    {
        GameObject obj = GameObject.CreatePrimitive(type);
        obj.transform.SetParent(parent, false);
        obj.transform.position = pos;
        obj.transform.localScale = scale;
        Renderer r = obj.GetComponent<Renderer>();
        if (r != null) r.material.color = color;
        return obj;
    }

    // Bo tri HUD tu dong khi bat dau Play.
    void SetupEnhancedUI()
    {
        if (dialogText == null || dialogText.canvas == null) return;
        hudCanvas = dialogText.canvas;
        Transform canvasRoot = hudCanvas.transform;

        ConfigureStatusSlider(playerHPSlider, canvasRoot, 0.2f, -90f, 100f,
            new Color(0.2f, 0.9f, 0.25f));
        ConfigureStatusSlider(playerMPSlider, canvasRoot, 0.2f, -135f, 60f,
            new Color(0f, 0.75f, 1f));
        ConfigureStatusSlider(enemyHPSlider, canvasRoot, 0.8f, -90f, 100f,
            new Color(0.95f, 0.2f, 0.2f));

        if (playerHPSlider != null)
            playerHPText = CreateStatusLabel(playerHPSlider, "Kael: 100 / 100 HP", Color.white);

        if (playerMPSlider != null)
            playerMPText = CreateStatusLabel(playerMPSlider, "MP: 60 / 60",
                new Color(0.3f, 0.9f, 1f));

        if (enemyHPSlider != null)
            enemyHPText = CreateStatusLabel(enemyHPSlider, "Rua Bong Toi: 100 / 100 HP",
                new Color(1f, 0.85f, 0.85f));

        // Dua nut Next vao trong mep Canvas, khong bi cat chu.
        if (nextButton != null)
        {
            RectTransform rt = nextButton.GetComponent<RectTransform>();
            rt.SetParent(canvasRoot, false);
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.localScale = Vector3.one;
            rt.anchoredPosition = new Vector2(-24f, 16f);
            rt.sizeDelta = new Vector2(160f, 38f);
            TextMeshProUGUI label = nextButton.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
            {
                label.rectTransform.anchorMin = Vector2.zero;
                label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.offsetMin = new Vector2(8f, 4f);
                label.rectTransform.offsetMax = new Vector2(-8f, -4f);
                label.enableAutoSizing = true;
                label.fontSizeMin = 14f;
                label.fontSizeMax = 20f;
                label.alignment = TextAlignmentOptions.Center;
                label.raycastTarget = false;
            }
        }

        GameObject bannerObj = new GameObject("TurnBanner");
        bannerObj.transform.SetParent(canvasRoot, false);
        Image bg = bannerObj.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.1f, 0.18f, 0.92f);
        bg.raycastTarget = false;
        RectTransform bannerRect = bannerObj.GetComponent<RectTransform>();
        bannerRect.anchorMin = new Vector2(0.25f, 1f);
        bannerRect.anchorMax = new Vector2(0.75f, 1f);
        bannerRect.pivot = new Vector2(0.5f, 1f);
        bannerRect.anchoredPosition = new Vector2(0f, -12f);
        bannerRect.sizeDelta = new Vector2(0f, 42f);
        turnBannerText = CreateOverlayText(bannerObj.transform,
            "HUONG DAN TAN THU", 18, new Color(1f, 0.9f, 0.3f), Vector2.zero);
        RectTransform titleRect = turnBannerText.rectTransform;
        titleRect.anchorMin = Vector2.zero;
        titleRect.anchorMax = Vector2.one;
        titleRect.offsetMin = new Vector2(10f, 4f);
        titleRect.offsetMax = new Vector2(-10f, -4f);
        turnBannerText.enableAutoSizing = true;
        turnBannerText.fontSizeMin = 12f;
        turnBannerText.fontSizeMax = 18f;

        if (pointer != null)
        {
            foreach (Graphic graphic in pointer.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;
        }
        if (victoryPanel != null) victoryPanel.transform.SetAsLastSibling();
        Canvas.ForceUpdateCanvases();
        UpdateHPTexts();
    }

    void ConfigureStatusSlider(Slider slider, Transform parent, float anchorX,
        float y, float maximum, Color fillColor)
    {
        if (slider == null) return;
        slider.gameObject.SetActive(true);
        RectTransform rt = slider.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(anchorX, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localScale = Vector3.one;
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta = new Vector2(160f, 10f);
        slider.interactable = false;
        slider.minValue = 0f;
        slider.maxValue = maximum;
        slider.value = maximum;
        if (slider.fillRect != null)
        {
            Image fill = slider.fillRect.GetComponent<Image>();
            if (fill != null) fill.color = fillColor;
        }
        foreach (Graphic graphic in slider.GetComponentsInChildren<Graphic>(true))
            graphic.raycastTarget = false;
    }

    TextMeshProUGUI CreateStatusLabel(Slider slider, string label, Color color)
    {
        TextMeshProUGUI text = CreateOverlayText(slider.transform, label, 13,
            color, new Vector2(0f, 14f));
        text.rectTransform.sizeDelta = new Vector2(240f, 16f);
        text.enableAutoSizing = true;
        text.fontSizeMin = 12f;
        text.fontSizeMax = 13f;
        return text;
    }

    void BuildSkillAndItemMenus()
    {
        if (dialogText == null || dialogText.canvas == null) return;
        Transform canvasTrans = dialogText.canvas.transform;

        skillMenuPanel = CreateMenuPanel(canvasTrans, "SkillMenuPanel", new Color(0.12f, 0.15f, 0.25f, 0.95f));
        CreateMenuButton(skillMenuPanel.transform, "[1] Lien Hoan Tram (25 MP)", new Color(0.9f, 0.3f, 0.2f), new Vector2(0, 45), () => ExecuteSkill(1));
        CreateMenuButton(skillMenuPanel.transform, "[2] Chien Hong / Buff ATK (20 MP)", new Color(0.2f, 0.6f, 0.9f), new Vector2(0, 0), () => ExecuteSkill(2));
        CreateMenuButton(skillMenuPanel.transform, "[3] Don Pha Giap (30 MP)", new Color(0.8f, 0.5f, 0.1f), new Vector2(0, -45), () => ExecuteSkill(3));
        skillMenuPanel.SetActive(false);

        itemMenuPanel = CreateMenuPanel(canvasTrans, "ItemMenuPanel", new Color(0.15f, 0.22f, 0.15f, 0.95f));
        CreateMenuButton(itemMenuPanel.transform, $"Binh Mau (+45 HP) [Con: {potionsLeft}]", new Color(0.2f, 0.8f, 0.3f), new Vector2(0, 45), () => ExecuteItem(1));
        CreateMenuButton(itemMenuPanel.transform, $"Binh Mana (+50 MP) [Con: {manaPotionsLeft}]", new Color(0.2f, 0.5f, 0.9f), new Vector2(0, 0), () => ExecuteItem(2));
        CreateMenuButton(itemMenuPanel.transform, $"Bom Lua (Gay chay) [Con: {bombsLeft}]", new Color(0.9f, 0.4f, 0.1f), new Vector2(0, -45), () => ExecuteItem(3));
        itemMenuPanel.SetActive(false);
    }

    GameObject CreateMenuPanel(Transform parent, string name, Color bgColor)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent, false);
        Image img = panel.AddComponent<Image>();
        img.color = bgColor;
        Outline outline = panel.AddComponent<Outline>();
        outline.effectColor = Color.yellow;
        outline.effectDistance = new Vector2(2, -2);

        RectTransform rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0, -30);
        rt.sizeDelta = new Vector2(340, 160);
        return panel;
    }

    void CreateMenuButton(Transform parent, string label, Color btnColor, Vector2 pos, UnityEngine.Events.UnityAction action)
    {
        GameObject btnObj = new GameObject("SubBtn");
        btnObj.transform.SetParent(parent, false);
        Image img = btnObj.AddComponent<Image>();
        img.color = btnColor;
        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(action);

        RectTransform rt = btnObj.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(310, 38);

        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(btnObj.transform, false);
        TextMeshProUGUI tmp = txtObj.AddComponent<TextMeshProUGUI>();
        if (dialogText != null) tmp.font = dialogText.font;
        tmp.text = label;
        tmp.fontSize = 15;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;

        RectTransform txtRt = txtObj.GetComponent<RectTransform>();
        txtRt.sizeDelta = new Vector2(310, 38);
    }

    TextMeshProUGUI CreateOverlayText(Transform parent, string content, int fontSize, Color color, Vector2 offset, bool addOutline = false)
    {
        GameObject txtObj = new GameObject("AutoLabel");
        txtObj.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = txtObj.AddComponent<TextMeshProUGUI>();
        if (dialogText != null) tmp.font = dialogText.font;
        tmp.text = content;
        tmp.fontSize = fontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;

        if (addOutline)
        {
            Outline outline = txtObj.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(2, -2);
        }

        RectTransform rt = txtObj.GetComponent<RectTransform>();
        rt.anchoredPosition = offset;
        rt.sizeDelta = new Vector2(320, 30);
        return tmp;
    }

    void UpdateHPTexts()
    {
        if (playerHPText != null) playerHPText.text = $"Kael: {playerHP:0.#} / 100 HP";
        if (playerMPText != null) playerMPText.text = $"MP: {Mathf.CeilToInt(playerMP)} / 60";
        if (enemyHPText != null) enemyHPText.text = $"Rua Bong Toi: {enemyHP:0.#} / 100 HP";
    }

    void ShowDialog(string message)
    {
        if (typewriterCoroutine != null) StopCoroutine(typewriterCoroutine);
        typewriterCoroutine = StartCoroutine(TypewriterRoutine(message));
    }

    IEnumerator TypewriterRoutine(string message)
    {
        dialogText.text = "";
        int charIndex = 0;
        foreach (char c in message)
        {
            dialogText.text += c;
            if (c != ' ' && charIndex % 2 == 0)
                PlaySynthSFX(680f, 720f, 0.025f, 0.12f);
            charIndex++;
            yield return new WaitForSeconds(textSpeed);
        }
    }

    void SetPointerTarget(Transform target, float yOffset = 65f)
    {
        if (pointer == null) return;
        // Use the actual button, independent of old Pointer Positions assignments.
        Button button = null;
        switch (currentStep)
        {
            case 0:
            case 1: button = nextButton; break;
            case 2: button = attackButton; break;
            case 3: button = defendButton; break;
            case 4: button = itemButton; break;
            case 5: button = skillButton; break;
        }
        pointerTarget = button != null ? button.GetComponent<RectTransform>() : target as RectTransform;
        pointer.SetActive(pointerTarget != null);
        Canvas.ForceUpdateCanvases();
        UpdateGuidePointer();
    }

    void SetCombatButtonsVisible(bool visible)
    {
        attackButton.gameObject.SetActive(visible);
        skillButton.gameObject.SetActive(visible);
        defendButton.gameObject.SetActive(visible);
        itemButton.gameObject.SetActive(visible);
    }

    void DisableAllCombatButtons()
    {
        attackButton.interactable = false;
        skillButton.interactable = false;
        defendButton.interactable = false;
        itemButton.interactable = false;
    }

    void HideSubMenus()
    {
        if (skillMenuPanel != null) skillMenuPanel.SetActive(false);
        if (itemMenuPanel != null) itemMenuPanel.SetActive(false);
    }

    public void OnNextButtonClicked()
    {
        if (isBusy || battleEnded) return;
        PlaySynthSFX(520f, 780f, 0.08f, 0.35f);
        if (currentStep == 0) StartTutorialStep(1);
        else if (currentStep == 1) StartTutorialStep(2);
    }

    public void OnAttackButtonClicked()
    {
        if (isBusy || battleEnded) return;
        if (!isFreeCombat && currentStep != 2) return;
        HideSubMenus();
        PlaySynthSFX(500f, 750f, 0.08f, 0.4f);
        StartCoroutine(PlayerAttackRoutine());
    }

    public void OnDefendButtonClicked()
    {
        if (isBusy || battleEnded) return;
        if (!isFreeCombat && currentStep != 3) return;
        HideSubMenus();
        PlaySynthSFX(450f, 680f, 0.08f, 0.4f);
        StartCoroutine(PlayerDefendRoutine());
    }

    public void OnItemButtonClicked()
    {
        if (isBusy || battleEnded) return;
        if (!isFreeCombat && currentStep != 4) return;
        bool open = itemMenuPanel != null && !itemMenuPanel.activeSelf;
        HideSubMenus();
        RefreshTutorialMenus();
        PlaySynthSFX(550f, 880f, 0.08f, 0.4f);
        if (itemMenuPanel != null) itemMenuPanel.SetActive(open);
    }

    public void OnSkillButtonClicked()
    {
        if (isBusy || battleEnded) return;
        if (!isFreeCombat && currentStep != 5) return;
        bool open = skillMenuPanel != null && !skillMenuPanel.activeSelf;
        HideSubMenus();
        RefreshTutorialMenus();
        PlaySynthSFX(600f, 950f, 0.1f, 0.45f);
        if (skillMenuPanel != null) skillMenuPanel.SetActive(open);
    }

    public void OnContinueButtonClicked()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void ExecuteSkill(int skillIndex)
    {
        if (isBusy || battleEnded || currentStep == 7) return;
        if (!isFreeCombat && (currentStep != 5 || skillIndex != 1)) return;
        HideSubMenus();
        if (skillIndex == 1)
        {
            if (playerMP < 25f) { ShowDialog("Khong du MP de dung Lien Hoan Tram! Hay danh thuong hoi MP."); return; }
            playerMP -= 25f;
            StartCoroutine(PlayerSkillRoutine());
        }
        else if (skillIndex == 2)
        {
            if (playerMP < 20f) { ShowDialog("Khong du MP!"); return; }
            playerMP -= 20f;
            StartCoroutine(PlayerBuffAtkRoutine()); // <--- KÍCH HOẠT HIỆU ỨNG SUPER SAIYAN CHIẾN HỐNG
        }
        else if (skillIndex == 3)
        {
            if (playerMP < 30f) { ShowDialog("Khong du MP!"); return; }
            playerMP -= 30f;
            StartCoroutine(PlayerArmorBreakRoutine());
        }
        UpdateHPTexts();
    }

    void ExecuteItem(int itemIndex)
    {
        if (isBusy || battleEnded || currentStep == 7) return;
        if (!isFreeCombat && (currentStep != 4 || itemIndex != 1)) return;
        HideSubMenus();
        if (itemIndex == 1)
        {
            if (potionsLeft <= 0) { ShowDialog("Da het Binh Mau!"); return; }
            potionsLeft--;
            StartCoroutine(PlayerItemRoutine());
        }
        else if (itemIndex == 2)
        {
            if (manaPotionsLeft <= 0) { ShowDialog("Da het Binh Mana!"); return; }
            manaPotionsLeft--;
            StartCoroutine(PlayerManaItemRoutine());
        }
        else if (itemIndex == 3)
        {
            if (bombsLeft <= 0) { ShowDialog("Da het Bom Lua!"); return; }
            bombsLeft--;
            StartCoroutine(PlayerThrowBombRoutine());
        }
    }

    // During the lesson only the requested option can be selected.
    // In free combat all available options are unlocked.
    void RefreshTutorialMenus()
    {
        if (skillMenuPanel != null)
        {
            Button[] buttons = skillMenuPanel.GetComponentsInChildren<Button>(true);
            float[] costs = { 25f, 20f, 30f };
            for (int i = 0; i < buttons.Length && i < costs.Length; i++)
                buttons[i].interactable = !isBusy && currentStep != 7
                    && (isFreeCombat || (currentStep == 5 && i == 0)) && playerMP >= costs[i];
        }
        if (itemMenuPanel != null)
        {
            Button[] buttons = itemMenuPanel.GetComponentsInChildren<Button>(true);
            int[] counts = { potionsLeft, manaPotionsLeft, bombsLeft };
            string[] names = { "Binh Mau (+45 HP)", "Binh Mana (+50 MP)", "Bom Lua (Gay chay)" };
            for (int i = 0; i < buttons.Length && i < counts.Length; i++)
            {
                buttons[i].interactable = !isBusy && currentStep != 7
                    && (isFreeCombat || (currentStep == 4 && i == 0)) && counts[i] > 0;
                TextMeshProUGUI label = buttons[i].GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null) label.text = $"{names[i]} [Con: {counts[i]}]";
            }
        }
    }

    IEnumerator PlayerManaItemRoutine()
    {
        isBusy = true;
        isDefending = false;
        DisableAllCombatButtons();
        pointer.SetActive(false);
        float restored = Mathf.Min(50f, 60f - playerMP);
        playerMP += restored;
        UpdateHPTexts();
        ShowDialog($"Uong Binh Mana! Hoi phuc +{restored} MP.");
        PlaySynthSFX(400f, 950f, 0.3f, 0.5f);
        SpawnFloatingText(playerStartPos + Vector3.up * 1.2f, $"+{restored} MP",
            new Color(0.2f, 0.6f, 1f), 32);
        yield return new WaitForSeconds(1.5f);
        yield return StartCoroutine(CheckEnemyOrNextTurn());
    }

    void StartTutorialStep(int stepIndex)
    {
        currentStep = stepIndex;
        HideSubMenus();

        switch (currentStep)
        {
            case 0:
                if (turnBannerText != null) turnBannerText.text = "COT TRUYEN ASTERIA";
                ShowDialog("Chao mung den voi Asteria! The luc bong toi da troi day, ban se vao vai chien binh Kael giai cuu vung quoc.");
                SetCombatButtonsVisible(false);
                nextButton.gameObject.SetActive(true);
                victoryPanel.SetActive(false);
                SetPointerTarget(nextBtnPos, 45f);
                break;

            case 1:
                if (turnBannerText != null) turnBannerText.text = "LUAT CHOI & THANH MP";
                ShowDialog("He thong RPG Tactic: Ngoai HP (Mau), ban co thanh MP (Nang luong) mau xanh de tung 3 ky nang chien thuat dac biet!");
                SetPointerTarget(nextBtnPos, 45f);
                break;

            case 2:
                if (turnBannerText != null) turnBannerText.text = "LUOT CUA BAN (1/4 - TAN CONG)";
                ShowDialog("BUOC 1 - DANH THUONG: Bam [Attack] de chem quai vat tich luy lai nang luong MP!");
                nextButton.gameObject.SetActive(false);
                SetCombatButtonsVisible(true);
                DisableAllCombatButtons();
                attackButton.interactable = true;
                SetPointerTarget(attackBtnPos, 65f);
                break;

            case 3:
                if (turnBannerText != null) turnBannerText.text = "LUOT CUA BAN (2/4 - PHONG THU)";
                ShowDialog("CANH BAO: Quai vat dang tu luc tuyet ky! Hay bam [Defend] de gio khien do don giam 70% sat thuong!");
                DisableAllCombatButtons();
                defendButton.interactable = true;
                SetPointerTarget(defendBtnPos, 65f);
                break;

            case 4:
                if (turnBannerText != null) turnBannerText.text = "LUOT CUA BAN (3/4 - VAT PHAM)";
                ShowDialog("Gio hay bam [Item] va chon Binh Mau de hoi phuc lai luong HP da mat.");
                DisableAllCombatButtons();
                itemButton.interactable = true;
                SetPointerTarget(itemBtnPos, 65f);
                break;

            case 5:
                if (turnBannerText != null) turnBannerText.text = "LUOT CUA BAN (4/4 - KY NANG)";
                ShowDialog("Bam [Skill], chon Lien Hoan Tram (25 MP) de danh hai don. Sau do ban se tu chien dau!");
                DisableAllCombatButtons();
                skillButton.interactable = true;
                SetPointerTarget(skillBtnPos, 65f);
                break;

            case 6:
                isFreeCombat = true;
                pointer.SetActive(false);
                if (turnBannerText != null) turnBannerText.text = "THUC CHIEN TU DO - TUY CHON CHIEN THUAT";
                ShowDialog("DA MO KHOA TOAN BO! Ban co the tu do dung Danh thuong, 3 Ky nang, Phong thu va Vat pham de chien dau!");
                EnableFreeCombatButtons();
                break;

            case 7:
                battleEnded = true;
                if (defeatPanel != null) defeatPanel.SetActive(false);
                isBusy = true;
                DisableAllCombatButtons();
                SetCombatButtonsVisible(false);
                nextButton.gameObject.SetActive(false);
                if (typewriterCoroutine != null) StopCoroutine(typewriterCoroutine);
                dialogText.text = "";
                victoryPanel.transform.SetAsLastSibling();
                if (playerHPSlider != null) playerHPSlider.gameObject.SetActive(false);
                if (enemyHPSlider != null) enemyHPSlider.gameObject.SetActive(false);
                if (playerMPSlider != null) playerMPSlider.gameObject.SetActive(false);
                if (turnBannerText != null) turnBannerText.transform.parent.gameObject.SetActive(false);
                pointer.SetActive(false);
                victoryPanel.SetActive(true);
                PlaySynthSFX(440f, 880f, 0.45f, 0.6f);
                break;
        }
    }

    void EnableFreeCombatButtons()
    {
        attackButton.interactable = true;
        defendButton.interactable = true;
        skillButton.interactable = true;
        itemButton.interactable = true;
    }

    float CalculatePlayerDamage(float baseDamage)
    {
        float damage = baseDamage;
        if (atkBuffTurns > 0) damage *= 1.5f;
        if (defDebuffTurns > 0) damage *= 1.4f;
        return Mathf.Round(damage * 10f) / 10f;
    }

    float CalculateEnemyDamage(float baseDamage)
    {
        return Mathf.Round(baseDamage * (isDefending ? 0.3f : 1f) * 10f) / 10f;
    }

    IEnumerator PlayerAttackRoutine()
    {
        isBusy = true;
        isDefending = false;
        DisableAllCombatButtons();
        pointer.SetActive(false);

        ShowDialog("Kael vung kiem chem thuong! Gay sat thuong va hoi +15 MP!");
        playerMP = Mathf.Min(60f, playerMP + 15f);
        UpdateHPTexts();

        Vector3 targetPos = enemyStartPos + new Vector3(-0.75f, 0, 0);
        yield return StartCoroutine(MoveCharacter(playerAnim.transform, targetPos, dashDuration));

        if (playerAnim != null) playerAnim.Play("Attack");
        PlaySynthSFX(420f, 180f, 0.18f, 0.5f);
        yield return new WaitForSeconds(0.55f);

        if (enemyAnim != null) enemyAnim.Play("GetHit");
        PlaySynthSFX(160f, 65f, 0.25f, 0.7f);
        SpawnHitSparks(enemyStartPos + Vector3.up * 0.5f, new Color(1f, 0.5f, 0.1f));
        StartCoroutine(ShakeCamera(0.2f, 0.08f));

        float finalDmg = CalculatePlayerDamage(25f);
        enemyHP = Mathf.Max(0, enemyHP - finalDmg);
        UpdateHPTexts();
        SpawnFloatingText(enemyStartPos + Vector3.up * 1.1f, $"-{finalDmg} HP", new Color(1f, 0.3f, 0.2f), 32);

        yield return new WaitForSeconds(0.95f);
        yield return StartCoroutine(MoveCharacter(playerAnim.transform, playerStartPos, dashDuration));
        yield return new WaitForSeconds(0.6f);

        yield return StartCoroutine(CheckEnemyOrNextTurn());
    }

    IEnumerator PlayerDefendRoutine()
    {
        isBusy = true;
        isDefending = true;
        DisableAllCombatButtons();
        pointer.SetActive(false);

        ShowDialog("Kael bat Giap Khien Thanh! Giam 70% sat thuong nhan vao o luot toi!");
        if (playerAnim != null) playerAnim.Play("Defend");
        PlaySynthSFX(500f, 820f, 0.25f, 0.5f);
        SpawnHitSparks(playerStartPos + Vector3.up * 0.5f, new Color(0.2f, 0.7f, 1f));
        SpawnFloatingText(playerStartPos + Vector3.up * 1.2f, "PHONG THU", new Color(0.3f, 0.85f, 1f), 28);

        yield return new WaitForSeconds(1.8f);
        yield return StartCoroutine(CheckEnemyOrNextTurn());
    }

    IEnumerator PlayerItemRoutine()
    {
        isBusy = true;
        isDefending = false;
        DisableAllCombatButtons();
        pointer.SetActive(false);

        ShowDialog("Kael uong Binh Mau (HP Potion)! Hoi phuc +45 HP!");
        playerHP = Mathf.Min(100f, playerHP + 45f);
        UpdateHPTexts();
        PlaySynthSFX(350f, 920f, 0.35f, 0.55f);
        SpawnHitSparks(playerStartPos + Vector3.up * 0.5f, new Color(0.2f, 1f, 0.4f));
        SpawnFloatingText(playerStartPos + Vector3.up * 1.2f, "+45 HP", new Color(0.2f, 1f, 0.3f), 32);

        yield return new WaitForSeconds(2.0f);
        if (!isFreeCombat && currentStep == 4)
        {
            isBusy = false;
            StartTutorialStep(5);
        }
        else
        {
            yield return StartCoroutine(CheckEnemyOrNextTurn());
        }
    }

    IEnumerator PlayerSkillRoutine()
    {
        isBusy = true;
        isDefending = false;
        DisableAllCombatButtons();
        pointer.SetActive(false);

        ShowDialog("LIEN HOAN TRAM! Hai don chem lien tiep, sat thuong goc 25 va 35!");
        Vector3 targetPos = enemyStartPos + new Vector3(-0.75f, 0, 0);
        yield return StartCoroutine(MoveCharacter(playerAnim.transform, targetPos, dashDuration));

        if (playerAnim != null) playerAnim.Play("Skill");

        yield return new WaitForSeconds(0.45f);
        if (enemyAnim != null) enemyAnim.Play("GetHit");
        PlaySynthSFX(220f, 80f, 0.22f, 0.7f);
        SpawnHitSparks(enemyStartPos + Vector3.up * 0.5f, new Color(1f, 0.8f, 0.1f));
        StartCoroutine(ShakeCamera(0.2f, 0.12f));
        float firstHit = CalculatePlayerDamage(25f);
        enemyHP = Mathf.Max(0, enemyHP - firstHit);
        UpdateHPTexts();
        SpawnFloatingText(enemyStartPos + Vector3.up * 1.0f, $"-{firstHit:0.#}!", new Color(1f, 0.8f, 0.2f), 30);

        yield return new WaitForSeconds(0.55f);
        if (enemyAnim != null) enemyAnim.Play("GetHit");
        PlaySynthSFX(140f, 45f, 0.35f, 0.85f);
        SpawnHitSparks(enemyStartPos + Vector3.up * 0.6f, new Color(1f, 0.2f, 0.1f));
        StartCoroutine(ShakeCamera(0.3f, 0.18f));
        float secondHit = CalculatePlayerDamage(35f);
        enemyHP = Mathf.Max(0, enemyHP - secondHit);
        UpdateHPTexts();
        SpawnFloatingText(enemyStartPos + Vector3.up * 1.35f, $"-{secondHit:0.#}!", new Color(1f, 0.2f, 0.1f), 36);

        yield return new WaitForSeconds(1.0f);
        yield return StartCoroutine(MoveCharacter(playerAnim.transform, playerStartPos, dashDuration));
        yield return new WaitForSeconds(0.6f);

        // Resolve actual damage and the enemy turn; step 5 then advances to free combat.
        yield return StartCoroutine(CheckEnemyOrNextTurn());
    }

    // ================= HIỆU ỨNG BIẾN HÌNH "SUPER SAIYAN" KHI DÙNG CHIẾN HỐNG =================
    IEnumerator PlayerBuffAtkRoutine()
    {
        isBusy = true;
        isDefending = false;
        DisableAllCombatButtons();
        pointer.SetActive(false);

        atkBuffTurns = 2;
        atkBuffAppliedThisAction = true;
        ShowDialog("CHIEN HONG! Tang 50% sat thuong Attack va Lien Hoan Tram trong 2 luot tiep theo. Dung lai se lam moi thoi han.");
        PlaySynthSFX(250f, 950f, 0.5f, 0.7f);

        // Hiệu ứng 1: Đổi màu nhân vật / tóc sang màu vàng rực rỡ (Super Saiyan)
        Renderer[] renderers = playerAnim.GetComponentsInChildren<Renderer>();
        Color[] originalColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r.material != null)
            {
                originalColors[i] = r.material.color;
                r.material.color = new Color(1f, 0.85f, 0.1f); // Màu vàng chuẩn Super Saiyan
            }
        }

        // Hiệu ứng 2: Tạo các luồng hạt "Ki" năng lượng bay vụt lên xung quanh người Kael
        SpawnKiAuraEffect(playerStartPos);
        SpawnHitSparks(playerStartPos + Vector3.up * 0.5f, new Color(1f, 0.9f, 0.2f));
        StartCoroutine(ShakeCamera(0.3f, 0.12f));
        SpawnFloatingText(playerStartPos + Vector3.up * 1.3f, "SUPER SAIYAN KI BURST!", new Color(1f, 0.85f, 0.1f), 34);

        yield return new WaitForSeconds(2.2f);

        // Trả lại màu sắc bình thường sau khi gồng xong
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r != null && r.material != null)
                r.material.color = originalColors[i];
        }

        yield return StartCoroutine(CheckEnemyOrNextTurn());
    }

    // Hàm tạo luồng Ki năng lượng bay lên giống 7 Viên Ngọc Rồng
    void SpawnKiAuraEffect(Vector3 centerPos)
    {
        for (int i = 0; i < 25; i++)
        {
            GameObject kiParticle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(kiParticle.GetComponent<Collider>());
            
            Vector3 randomOffset = new Vector3(Random.Range(-0.45f, 0.45f), Random.Range(0f, 1.4f), Random.Range(-0.45f, 0.45f));
            kiParticle.transform.position = centerPos + randomOffset;
            kiParticle.transform.localScale = Vector3.one * Random.Range(0.12f, 0.28f);

            Renderer rend = kiParticle.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.material.color = new Color(1f, 0.9f, 0.1f, 0.95f); // Màu vàng năng lượng ki
            }

            StartCoroutine(AnimateKiParticle(kiParticle.transform));
        }
    }

    IEnumerator AnimateKiParticle(Transform pTransform)
    {
        float elapsed = 0f;
        float duration = 0.9f;
        while (elapsed < duration && pTransform != null)
        {
            elapsed += Time.deltaTime;
            // Hạt Ki vụt bay lên trời với tốc độ cao
            pTransform.position += Vector3.up * (Time.deltaTime * 3.5f);
            pTransform.localScale = Vector3.Lerp(pTransform.localScale, Vector3.zero, elapsed / duration);
            yield return null;
        }
        if (pTransform != null) Destroy(pTransform.gameObject);
    }

    IEnumerator PlayerArmorBreakRoutine()
    {
        isBusy = true;
        isDefending = false;
        DisableAllCombatButtons();
        pointer.SetActive(false);

        defDebuffTurns = 2;
        armorBreakAppliedThisAction = true;
        ShowDialog("PHA GIAP! Quai nhan them 40% sat thuong Attack va Lien Hoan Tram trong 2 luot tiep theo.");
        if (playerAnim != null) playerAnim.Play("Attack");
        PlaySynthSFX(600f, 200f, 0.3f, 0.6f);
        SpawnHitSparks(enemyStartPos + Vector3.up * 0.5f, new Color(0.8f, 0.8f, 0.8f));
        SpawnFloatingText(enemyStartPos + Vector3.up * 1.2f, "DEF DOWN!", new Color(0.9f, 0.9f, 0.2f), 32);

        yield return new WaitForSeconds(2.0f);
        yield return StartCoroutine(CheckEnemyOrNextTurn());
    }

    IEnumerator PlayerThrowBombRoutine()
    {
        isBusy = true;
        isDefending = false;
        DisableAllCombatButtons();
        pointer.SetActive(false);

        burnTurns = 2;
        ShowDialog("NEM BOM LUA! Gay 30 sat thuong chuan va thieu dot quai vat [BURN]!");
        PlaySynthSFX(200f, 600f, 0.35f, 0.7f);
        SpawnHitSparks(enemyStartPos + Vector3.up * 0.5f, Color.red);
        enemyHP = Mathf.Max(0, enemyHP - 30f);
        UpdateHPTexts();
        SpawnFloatingText(enemyStartPos + Vector3.up * 1.2f, "BURN -30 HP", Color.red, 34);

        yield return new WaitForSeconds(2.0f);
        yield return StartCoroutine(CheckEnemyOrNextTurn());
    }

    IEnumerator CheckEnemyOrNextTurn()
    {
        if (battleEnded) yield break;
        if (burnTurns > 0)
        {
            burnTurns--;
            enemyHP = Mathf.Max(0, enemyHP - 15f);
            UpdateHPTexts();
            SpawnFloatingText(enemyStartPos + Vector3.up * 1.4f, "Thieu dot -15 HP", Color.red, 28);
            yield return new WaitForSeconds(0.8f);
        }

        // Casting does not consume the newly applied effect.
        // Each subsequent player action consumes one turn, including Item/Defend.
        if (atkBuffTurns > 0 && !atkBuffAppliedThisAction) atkBuffTurns--;
        if (defDebuffTurns > 0 && !armorBreakAppliedThisAction) defDebuffTurns--;
        atkBuffAppliedThisAction = false;
        armorBreakAppliedThisAction = false;

        if (enemyHP <= 0.1f)
        {
            enemyHP = 0f;
            UpdateHPTexts();
            if (enemyAnim != null) enemyAnim.Play("Die");
            PlaySynthSFX(300f, 70f, 0.5f, 0.7f);
            ShowDialog("ENEMY DEFEATED! Rua Bong Toi đa bi tieu diet hoan toan!");
            SpawnFloatingText(enemyStartPos + Vector3.up * 1.1f, "DEFEATED!", Color.yellow, 36);
            yield return new WaitForSeconds(2.8f);
            isBusy = false;
            StartTutorialStep(7);
            yield break;
        }

        if (turnBannerText != null) turnBannerText.text = "LUOT KE DICH (ENEMY TURN)";
        yield return new WaitForSeconds(1.0f);

        bool useSpinSkill = (!isFreeCombat && currentStep == 3) || (isFreeCombat && Random.value > 0.5f);
        Vector3 targetPos = playerStartPos + new Vector3(0.75f, 0, 0);

        if (useSpinSkill)
        {
            ShowDialog("Luot Ke dich: Rua Bong Toi tung KY NANG XOAY GAI lao thang vao Kael!");
            yield return new WaitForSeconds(0.6f);
            yield return StartCoroutine(MoveCharacter(enemyAnim.transform, targetPos, dashDuration));
            if (enemyAnim != null) enemyAnim.Play("Attack02");
            yield return new WaitForSeconds(0.55f);

            float dmg = CalculateEnemyDamage(35f);
            playerHP = Mathf.Max(0f, playerHP - dmg);
            UpdateHPTexts();

            if (isDefending)
            {
                if (playerAnim != null) playerAnim.Play("Defend");
                PlaySynthSFX(700f, 450f, 0.2f, 0.6f);
                SpawnHitSparks(playerStartPos + Vector3.up * 0.5f, new Color(0.3f, 0.8f, 1f));
                StartCoroutine(ShakeCamera(0.18f, 0.06f));
                SpawnFloatingText(playerStartPos + Vector3.up * 1.2f, $"DO DON! -{dmg} HP", new Color(0.4f, 0.9f, 1f), 28);
            }
            else
            {
                if (playerAnim != null) playerAnim.Play("GetHit");
                PlaySynthSFX(150f, 50f, 0.3f, 0.8f);
                SpawnHitSparks(playerStartPos + Vector3.up * 0.5f, Color.red);
                StartCoroutine(ShakeCamera(0.3f, 0.16f));
                SpawnFloatingText(playerStartPos + Vector3.up * 1.2f, $"-{dmg} HP", Color.red, 34);
            }
        }
        else
        {
            ShowDialog("Luot Ke dich (Enemy AI): Quai vat tien len huc tra!");
            yield return new WaitForSeconds(0.6f);
            yield return StartCoroutine(MoveCharacter(enemyAnim.transform, targetPos, dashDuration));
            if (enemyAnim != null) enemyAnim.Play("Attack01");
            yield return new WaitForSeconds(0.5f);

            float dmg = CalculateEnemyDamage(25f);
            playerHP = Mathf.Max(0f, playerHP - dmg);
            UpdateHPTexts();
            if (playerAnim != null) playerAnim.Play(isDefending ? "Defend" : "GetHit");
            PlaySynthSFX(180f, 70f, 0.25f, 0.7f);
            SpawnHitSparks(playerStartPos + Vector3.up * 0.5f, new Color(1f, 0.4f, 0.2f));
            StartCoroutine(ShakeCamera(0.2f, 0.09f));
            SpawnFloatingText(playerStartPos + Vector3.up * 1.2f, isDefending ? $"DO DON -{dmg} HP" : $"-{dmg} HP", Color.red, 30);
        }

        yield return new WaitForSeconds(1.0f);
        yield return StartCoroutine(MoveCharacter(enemyAnim.transform, enemyStartPos, dashDuration));
        yield return new WaitForSeconds(1.0f);

        // Do not hand control back after a lethal enemy attack.
        if (playerHP <= 0f)
        {
            yield return StartCoroutine(HandleDefeat());
            yield break;
        }

        isBusy = false;

        if (!isFreeCombat)
        {
            if (currentStep == 2) StartTutorialStep(3);
            else if (currentStep == 3) StartTutorialStep(4);
            else if (currentStep == 4) StartTutorialStep(5);
            else if (currentStep == 5) StartTutorialStep(6);
        }
        else
        {
            if (turnBannerText != null) turnBannerText.text = "THUC CHIEN TU DO - LUOT CUA BAN";
            string status = "Den luot ban!";
            if (atkBuffTurns > 0) status += $" ATK +50%: con {atkBuffTurns} luot.";
            if (defDebuffTurns > 0) status += $" Pha giap: con {defDebuffTurns} luot.";
            ShowDialog(status + " Hay chon hanh dong.");
            EnableFreeCombatButtons();
        }
    }

    IEnumerator HandleDefeat()
    {
        if (battleEnded) yield break;
        battleEnded = true;
        isBusy = true;
        isFreeCombat = false;
        isDefending = false;
        playerHP = 0f;
        UpdateHPTexts();
        HideSubMenus();
        DisableAllCombatButtons();
        SetCombatButtonsVisible(false);
        if (nextButton != null) nextButton.gameObject.SetActive(false);
        if (pointer != null) pointer.SetActive(false);
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (typewriterCoroutine != null)
        {
            StopCoroutine(typewriterCoroutine);
            typewriterCoroutine = null;
        }
        if (dialogText != null) dialogText.text = "Kael da nga xuong. Tran dau ket thuc.";
        if (turnBannerText != null) turnBannerText.text = "THAT BAI";

        // Play the existing death state only if it exists in this Animator.
        if (playerAnim != null)
        {
            int dieState = Animator.StringToHash("Base Layer.Die");
            if (playerAnim.HasState(0, dieState)) playerAnim.Play(dieState, 0, 0f);
        }
        yield return new WaitForSeconds(1.5f);

        if (playerHPSlider != null) playerHPSlider.gameObject.SetActive(false);
        if (playerMPSlider != null) playerMPSlider.gameObject.SetActive(false);
        if (enemyHPSlider != null) enemyHPSlider.gameObject.SetActive(false);
        if (turnBannerText != null) turnBannerText.transform.parent.gameObject.SetActive(false);
        if (dialogText != null) dialogText.text = "";
        if (defeatPanel != null)
        {
            defeatPanel.transform.SetAsLastSibling();
            defeatPanel.SetActive(true);
        }
        else
        {
            if (dialogText != null) dialogText.text = "THAT BAI - Hay gan DefeatPanel vao GameController.";
            Debug.LogError("TutorialManager: chua gan Defeat Panel trong Inspector.", this);
        }
    }

    IEnumerator MoveCharacter(Transform charTransform, Vector3 destination, float duration)
    {
        if (charTransform == null) yield break;
        Vector3 start = charTransform.position;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            charTransform.position = Vector3.Lerp(start, destination, elapsed / duration);
            yield return null;
        }
        charTransform.position = destination;
    }

    IEnumerator ShakeCamera(float duration, float magnitude)
    {
        if (Camera.main == null) yield break;
        Transform cam = Camera.main.transform;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            Vector3 offset = Random.insideUnitSphere * magnitude;
            cam.position = camStartPos + new Vector3(offset.x, offset.y, 0);
            yield return null;
        }
        cam.position = camStartPos;
    }

    void SpawnFloatingText(Vector3 worldPos, string text, Color color, int size)
    {
        if (dialogText == null || dialogText.canvas == null || Camera.main == null) return;
        GameObject go = new GameObject("FloatingDamage");
        go.transform.SetParent(dialogText.canvas.transform, false);

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.font = dialogText.font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;

        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2, -2);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(300, 60);
        rt.position = Camera.main.WorldToScreenPoint(worldPos);

        StartCoroutine(AnimateFloatingText(rt, tmp));
    }

    IEnumerator AnimateFloatingText(RectTransform rt, TextMeshProUGUI tmp)
    {
        Vector3 startPos = rt.position;
        Color startColor = tmp.color;
        float elapsed = 0f;
        float duration = 1.4f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            rt.position = startPos + new Vector3(0, t * 65f, 0);
            tmp.color = new Color(startColor.r, startColor.g, startColor.b, 1f - t);
            yield return null;
        }
        Destroy(rt.gameObject);
    }

    void SpawnHitSparks(Vector3 worldPos, Color sparkColor)
    {
        for (int i = 0; i < 10; i++)
        {
            GameObject spark = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(spark.GetComponent<Collider>());
            spark.transform.position = worldPos;
            spark.transform.localScale = Vector3.one * 0.08f;

            Renderer rend = spark.GetComponent<Renderer>();
            if (rend != null) rend.material.color = sparkColor;

            StartCoroutine(AnimateSpark(spark.transform));
        }
    }

    IEnumerator AnimateSpark(Transform spark)
    {
        Vector3 velocity = Random.onUnitSphere * 2.0f;
        velocity.y = Mathf.Abs(velocity.y) + 1.0f;
        float elapsed = 0f;
        while (elapsed < 0.6f && spark != null)
        {
            elapsed += Time.deltaTime;
            spark.position += velocity * Time.deltaTime;
            spark.localScale = Vector3.Lerp(Vector3.one * 0.08f, Vector3.zero, elapsed / 0.6f);
            yield return null;
        }
        if (spark != null) Destroy(spark.gameObject);
    }

    void SetupAudioSystem()
    {
        bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.loop = true;
        bgmSource.volume = musicVolume;

        if (backgroundMusic != null)
        {
            bgmSource.clip = backgroundMusic;
            bgmSource.Play();
        }
        else
        {
            bgmSource.clip = GenerateRetroBGM();
            bgmSource.Play();
        }

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.volume = 0.65f;
    }

    void PlaySynthSFX(float startFreq, float endFreq, float duration, float vol = 0.5f)
    {
        if (sfxSource == null) return;
        int sampleRate = 44100;
        int sampleCount = Mathf.Max(1, (int)(sampleRate * duration));
        float[] samples = new float[sampleCount];

        float phase = 0f;
        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleCount;
            float freq = Mathf.Lerp(startFreq, endFreq, t);
            phase += freq / sampleRate;
            float env = 1f - t;
            samples[i] = Mathf.Sin(phase * 2f * Mathf.PI) * env * vol;
        }

        AudioClip clip = AudioClip.Create("SynthSFX", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        sfxSource.PlayOneShot(clip);
    }

    AudioClip GenerateRetroBGM()
    {
        int sampleRate = 44100;
        float[] notes = { 220f, 261.63f, 329.63f, 293.66f, 220f, 329.63f, 293.66f, 246.94f };
        float noteDuration = 0.45f;
        int totalSamples = (int)(sampleRate * noteDuration * notes.Length);
        float[] data = new float[totalSamples];

        for (int n = 0; n < notes.Length; n++)
        {
            float freq = notes[n];
            int startSample = (int)(n * noteDuration * sampleRate);
            int noteSamples = (int)(noteDuration * sampleRate);
            for (int i = 0; i < noteSamples; i++)
            {
                float t = (float)i / noteSamples;
                float env = Mathf.Sin(t * Mathf.PI);
                float wave = Mathf.Sin(2f * Mathf.PI * freq * i / sampleRate) * 0.6f
                           + Mathf.Sin(2f * Mathf.PI * (freq * 0.5f) * i / sampleRate) * 0.4f;
                data[startSample + i] = wave * env * 0.35f;
            }
        }

        AudioClip bgm = AudioClip.Create("AutoRPG_BGM", totalSamples, 1, sampleRate, false);
        bgm.SetData(data, 0);
        return bgm;
    }
}