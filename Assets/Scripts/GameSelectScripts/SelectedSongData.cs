using UnityEngine;

public class SelectedSongData:MonoBehaviour
{
    public static SelectedSongData Instance {get;private set;}
    public string selectedChartFileName {get;private set;}
    public string selectedAudioFileName {get;private set;}
    public string selectedImageFileName {get;private set;}

    void Awake()
    {
        //实现单例模式
        if(Instance!=null&&Instance!=this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance=this;
            DontDestroyOnLoad(gameObject); //确保在场景切换时数据不会丢失
        }
    }

    public void SetSelectedSong(string chartName,string audioName,string imageName)
    {
        selectedAudioFileName=audioName;
        selectedChartFileName=chartName;
        selectedImageFileName=imageName;
        Debug.Log($"选择的歌曲信息已设置: {selectedChartFileName},{selectedAudioFileName},{selectedImageFileName}");
    }
}
