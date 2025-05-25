using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager:MonoBehaviour
{
    public static GameManager Instance {get;private set;}
    public GameResults currentResults; // 用于在游戏结束时存储结果

    // 场景名称 (在Inspector中配置)
    public string resultsSceneName="ResultsScene";
    public string songSelectionSceneName="SongSelectScene";

    private void Awake()
    {
        if(Instance==null)
        {
            Instance=this;
            DontDestroyOnLoad(gameObject);
        }
        else if(Instance!=this)
        {
            Destroy(gameObject);
        }
    }

    public void GoToResultsScene()
    {
        if(JudgementManager.Instance!=null) // 确保 JudgementManager 存在以获取数据
        {

            // 从 JudgementManager获取
            currentResults=JudgementManager.Instance.GetCurrentResult();
            Debug.Log($"准备跳转到结算场景。");
        }
        else
        {
            Debug.LogError("JudgementManager.Instance 未找到! 无法获取完整的结算数据。");
            currentResults = new GameResults(); // 创建一个空的 results 对象
        }
        
        SceneManager.LoadScene(resultsSceneName);
    }

    // 由 ResultsDisplay 脚本在结算界面按回车时调用
    public void ReturnToSongSelection()
    {
        Debug.Log("返回选歌场景。");
        SceneManager.LoadScene(songSelectionSceneName);
    }

}

public class GameResults
{
    public int finalScore;
    public int maxCombo;
    public int perfectCount;
    public int greatCount;
    public int goodCount;
    public int badCount;
    public int missCount;
}