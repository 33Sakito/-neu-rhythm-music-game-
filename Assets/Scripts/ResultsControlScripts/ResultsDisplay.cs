using UnityEngine;
using TMPro; 

public class ResultsDisplay:MonoBehaviour
{
    [Header("UI TextMeshPro Elements")]
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI maxComboText;
    public TextMeshProUGUI perfectCountText;
    public TextMeshProUGUI greatCountText;
    public TextMeshProUGUI goodCountText;
    public TextMeshProUGUI badCountText;
    public TextMeshProUGUI missCountText;

    void Start()
    {
        if(GameManager.Instance!=null&&GameManager.Instance.currentResults!=null)
        {
            DisplayResults(GameManager.Instance.currentResults);
        }
        else
        {
            Debug.LogError("GameManager或结算数据未找到! 将显示默认/空值。");
            // 可以选择显示一些默认值以防出错
            if(scoreText!=null) scoreText.text="SCORE 0";
            if(maxComboText!=null) maxComboText.text="COMBO 0";
            if(perfectCountText!=null) perfectCountText.text="PERFECT 0";
            if(greatCountText!=null) greatCountText.text="GREAT 0";
            if(goodCountText!=null) goodCountText.text="GOOD 0";
            if(badCountText!=null) badCountText.text="BAD 0";
            if(missCountText!=null) missCountText.text="MISS 0";
        }
    }

    void DisplayResults(GameResults results)
    {
        // 更新数值显示的TextMeshPro对象
        if(scoreText!=null) scoreText.text="SCORE "+results.finalScore.ToString();
        if(maxComboText!=null) maxComboText.text="COMBO "+results.maxCombo.ToString();
        if(perfectCountText!=null) perfectCountText.text="PERFECT "+results.perfectCount.ToString();
        if(greatCountText!=null) greatCountText.text="GREAT "+results.greatCount.ToString();
        if(goodCountText!=null) goodCountText.text="GOOD "+results.goodCount.ToString();
        if(badCountText!=null) badCountText.text="BAD "+results.badCount.ToString();
        if(missCountText!=null) missCountText.text="MISS "+results.missCount.ToString();
    }

    void Update()
    {
        // 检测回车键
        if(Input.GetKeyDown(KeyCode.Return)||Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            if(GameManager.Instance!=null)
            {
                GameManager.Instance.ReturnToSongSelection();
            }
            else
            {
                Debug.LogWarning("GameManager实例未找到，无法自动返回选歌界面。");

            }
        }
    }
}