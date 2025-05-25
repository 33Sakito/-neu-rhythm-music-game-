using UnityEngine;
using System.IO;
using UnityEngine.Networking;
using System.Collections;

public class BackgroundChanger:MonoBehaviour
{
    private SpriteRenderer spriteRenderer;

    void Start()
    {
        spriteRenderer=GetComponent<SpriteRenderer>();
        if(spriteRenderer==null)
        {
            Debug.LogError("BackgroundChanger: 未在此GameObject上找到SpriteRenderer组件。");
            this.enabled=false; // 禁用此脚本以防后续错误
            return;
        }

        if(SelectedSongData.Instance!=null&&!string.IsNullOrEmpty(SelectedSongData.Instance.selectedImageFileName))
        {
            string imageFilePath=Path.Combine(Application.streamingAssetsPath,SelectedSongData.Instance.selectedImageFileName);
            StartCoroutine(LoadAndSetSprite(imageFilePath));
        }
        else
        {
            Debug.LogWarning("BackgroundChanger: SelectedSongData未找到或selectedImageFileName为空。将使用默认背景。");
        }
    }

    IEnumerator LoadAndSetSprite(string filePath)
    {
        // 对于本地文件，UnityWebRequest需要 "file://" 前缀
        string pathForWebRequest="file://"+filePath;
        
        Debug.Log($"BackgroundChanger: 尝试从路径加载背景图片: {pathForWebRequest}");

        using(UnityWebRequest www=UnityWebRequestTexture.GetTexture(pathForWebRequest))
        {
            yield return www.SendWebRequest();

            if(www.result==UnityWebRequest.Result.Success)
            {
                Texture2D texture=DownloadHandlerTexture.GetContent(www);
                if(texture!=null)
                {
                    // 从加载的Texture2D创建Sprite
                    // Rect(0,0,texture.width,texture.height) 表示使用整个纹理
                    // Vector2(0.5f,0.5f) 表示Sprite的中心点为轴心
                    Sprite newSprite=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(0.5f,0.5f));
                    spriteRenderer.sprite=newSprite;
                    Debug.Log($"BackgroundChanger: 背景图片 '{Path.GetFileName(filePath)}' 已成功加载并应用。");
                }
                else
                {
                    Debug.LogError($"BackgroundChanger: 无法从下载的数据创建纹理: {filePath}");
                }
            }
            else
            {
                Debug.LogError($"BackgroundChanger: 加载背景图片失败: {filePath} - {www.error}");
            }
        }
    }
}