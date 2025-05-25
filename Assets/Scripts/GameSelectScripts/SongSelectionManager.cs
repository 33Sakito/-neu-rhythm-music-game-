using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System.Collections;

public class SongSelectionManager : MonoBehaviour
{
    [Header("歌曲数据")]
    public List<SongInfo> availableSongs;

    [Header("UI 引用 - 列表")]
    public GameObject songItemPrefab;
    public RectTransform contentTransform;
    public ScrollRect scrollRect;

    [Header("UI 引用 - 详情")]
    public Image coverArtImage;
    public TextMeshProUGUI songTitleText;
    public TextMeshProUGUI artistText;
    public TextMeshProUGUI bpmText;
    public Button startButton;

    [Header("选中效果")]
    public float selectedItemScale = 1.2f;
    public float normalItemScale = 1.0f;
    public float scaleLerpSpeed = 8f;
    public float centerLerpSpeed = 10f; 

    [Header("滚动设置")]
    public float scrollSensitivity = 0.1f;

    private List<RectTransform> songItemTransforms = new List<RectTransform>();
    private int currentSelectedIndex = 0;
    private Vector2 targetContentAnchoredPosition;
    private float accumulatedScroll = 0f;

    void Start()
    {
        // --- 安全检查 ---
        if (availableSongs == null || availableSongs.Count == 0) { Debug.LogError("没有可用的歌曲信息!"); return; }
        if (songItemPrefab == null || contentTransform == null || scrollRect == null) { Debug.LogError("列表相关的 UI 引用未设置!"); return; }
        if (coverArtImage == null || songTitleText == null || artistText == null || startButton == null) { Debug.LogError("详情相关的 UI 引用未设置!"); return; }

        // --- 禁用 ScrollRect 的默认交互 ---
        // 防止用户通过拖拽滚动，只用滚轮
        scrollRect.vertical = false; // 禁用垂直拖拽滚动
        scrollRect.horizontal = false; // 禁用水平拖拽滚动
        scrollRect.movementType = ScrollRect.MovementType.Unrestricted; // 防止回弹等效果干扰
        scrollRect.inertia = false; // 禁用惯性

        // --- 初始化 ---
        PopulateSongListUI();
        SetupInitialSelection();

        startButton.onClick.AddListener(StartSelectedSong);
    }

    void PopulateSongListUI()
    {
        songItemTransforms.Clear();
        foreach (Transform child in contentTransform) { Destroy(child.gameObject); }

        for (int i = 0; i < availableSongs.Count; i++)
        {
            GameObject itemGO = Instantiate(songItemPrefab, contentTransform);
            RectTransform itemRect = itemGO.GetComponent<RectTransform>();
            songItemTransforms.Add(itemRect);

            TextMeshProUGUI itemText = itemGO.GetComponentInChildren<TextMeshProUGUI>();
            if (itemText != null) { itemText.text = $"{availableSongs[i].songName}\n<size=80%>{availableSongs[i].artist}</size>"; }

            Button itemButton = itemGO.GetComponent<Button>();
            if (itemButton != null)
            {
                int index = i;
                // 点击条目仍然可以直接选中
                itemButton.onClick.AddListener(() => OnSongItemSelected(index));
            }
        }
    }

    void SetupInitialSelection()
    {
        currentSelectedIndex = 0;
        UpdateDetailPanel(availableSongs[currentSelectedIndex]);
        StartCoroutine(InitialLayoutUpdate());
    }

    IEnumerator InitialLayoutUpdate()
    {
        yield return null; // 等待布局计算 (如果需要)
        CalculateTargetContentPosition(); // 计算初始目标位置
        SnapToTargetPosition(); // 立即定位到目标
        UpdateItemScalesInstantly(); // 立即设置初始缩放
    }

    void Update()
    {
        // --- 处理鼠标滚轮输入 ---
        float scrollInput = Input.GetAxis("Mouse ScrollWheel");
        accumulatedScroll += scrollInput;

        // 当累积的滚动量足够大时，触发选择变化
        if (Mathf.Abs(accumulatedScroll) >= scrollSensitivity)
        {
            int previousIndex = currentSelectedIndex;
            if (accumulatedScroll > 0) // 向上滚动
            {
                currentSelectedIndex--;
            }
            else // 向下滚动
            {
                currentSelectedIndex++;
            }

            // 将索引限制在有效范围内
            currentSelectedIndex = Mathf.Clamp(currentSelectedIndex, 0, availableSongs.Count - 1);

            // 如果索引真的改变了
            if (previousIndex != currentSelectedIndex)
            {
                OnSelectionChanged();
            }

            // 重置累积滚动量
            accumulatedScroll = 0f;
        }

        // --- 平滑动画 ---
        // 平滑滚动到目标位置
        contentTransform.anchoredPosition = Vector2.Lerp(
            contentTransform.anchoredPosition,
            targetContentAnchoredPosition,
            Time.deltaTime * centerLerpSpeed
        );

        // 平滑缩放条目
        UpdateItemScalesSmoothly();
    }

    // 当通过点击或滚轮改变选择时调用
    void OnSelectionChanged()
    {
        UpdateDetailPanel(availableSongs[currentSelectedIndex]);
        CalculateTargetContentPosition(); // 计算新的目标位置
        // Update() 中的 Lerp 会处理滚动动画
    }

    // 当点击某个条目时 (保持这个功能)
    void OnSongItemSelected(int index)
    {
        if (index < 0 || index >= availableSongs.Count || index == currentSelectedIndex) return; // 防止重复选择或无效索引

        currentSelectedIndex = index;
        OnSelectionChanged(); // 调用统一的选择变化处理函数
    }

    // 计算选中项应该居中时的 Content 目标位置 (这个函数保持不变)
    void CalculateTargetContentPosition()
{
    if (songItemTransforms.Count == 0) return;

    float viewportHeight = ((RectTransform)scrollRect.viewport).rect.height;
    float contentHeight = contentTransform.rect.height;
    float itemHeight = songItemTransforms[0].rect.height;
    float spacing = 0f;
    VerticalLayoutGroup layoutGroup = contentTransform.GetComponent<VerticalLayoutGroup>();
    if (layoutGroup != null && layoutGroup.enabled) spacing = layoutGroup.spacing;

    // --- Debug Logs ---
    Debug.Log($"--- Calculating Target Position for Index: {currentSelectedIndex} ---");
    Debug.Log($"Viewport Height: {viewportHeight}");
    Debug.Log($"Content Height: {contentHeight}");
    Debug.Log($"Item Height: {itemHeight}");
    Debug.Log($"Spacing: {spacing}");
    // --- End Debug Logs ---

    float selectedItemCenterY = -(itemHeight * 0.5f + (itemHeight + spacing) * currentSelectedIndex);
    Debug.Log($"Selected Item Center Y (relative to Content top): {selectedItemCenterY}");

    float targetY = -viewportHeight * 0.5f - selectedItemCenterY;
    Debug.Log($"Calculated Target Y (before clamp/adjustment): {targetY}");

    // --- 修改 Clamp 逻辑 ---
    // 只有当内容高度大于视口高度时，才需要限制滚动范围
    if (contentHeight > viewportHeight)
    {
        float minY = -(contentHeight - viewportHeight);
        targetY = Mathf.Clamp(targetY, minY, 0f);
        Debug.Log($"Clamped Target Y (Content > Viewport): {targetY}");
    }


    targetContentAnchoredPosition = new Vector2(contentTransform.anchoredPosition.x, targetY);
    Debug.Log($"Final Target Anchored Position: {targetContentAnchoredPosition}");
    Debug.Log($"--- Calculation End ---");
}



    // 平滑更新所有条目的缩放
    void UpdateItemScalesSmoothly()
    {
        for (int i = 0; i < songItemTransforms.Count; i++)
        {
            Vector3 targetScale = (i == currentSelectedIndex) ? Vector3.one * selectedItemScale : Vector3.one * normalItemScale;
            songItemTransforms[i].localScale = Vector3.Lerp(songItemTransforms[i].localScale, targetScale, Time.deltaTime * scaleLerpSpeed);
        }
    }

    // 立即更新所有条目的缩放 (用于初始化)
    void UpdateItemScalesInstantly()
    {
        for (int i = 0; i < songItemTransforms.Count; i++)
        {
            songItemTransforms[i].localScale = (i == currentSelectedIndex) ? Vector3.one * selectedItemScale : Vector3.one * normalItemScale;
        }
    }


    // 立即将 Content 移动到目标位置 (用于初始化)
    void SnapToTargetPosition()
    {
         contentTransform.anchoredPosition = targetContentAnchoredPosition;
    }

    // --- 详情和开始游戏 ---
    void UpdateDetailPanel(SongInfo song)
    {
        if (song == null) return;
        songTitleText.text = song.songName;
        artistText.text = song.artist;
        if (bpmText != null) bpmText.text = $"BPM: {song.bpm}";
        if (coverArtImage != null)
        {
            coverArtImage.sprite = song.coverArt;
            coverArtImage.enabled = (song.coverArt != null);
        }
        startButton.interactable = true;
    }

    void StartSelectedSong()
    {
        if (currentSelectedIndex >= 0 && currentSelectedIndex < availableSongs.Count)
        {
            SelectSong(availableSongs[currentSelectedIndex]);
        }
    }

    void SelectSong(SongInfo selectedSong)
    {
        if (selectedSong==null) return;
        if (SelectedSongData.Instance!=null)
        {
            SelectedSongData.Instance.SetSelectedSong(selectedSong.chartFileName,selectedSong.audioFileName,selectedSong.imageFileName);
            SceneManager.LoadScene("GameplayScene");
        }
        else { Debug.LogError("SelectedSongData 实例未找到!"); }
    }
}
