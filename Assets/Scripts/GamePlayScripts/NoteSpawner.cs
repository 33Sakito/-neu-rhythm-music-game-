using System.Collections;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class NoteSpawner:MonoBehaviour
{
    //可在Inspector面板中设置
    [Header("轨道位置定义(Track Positions)")]
    [Tooltip("每个轨道生成音符的精确位置(空GameObject的Transform)")] public Transform[] trackSpawnPoints;
    [Tooltip("或者直接定义每个轨道的X坐标")] public float[] trackXPositions=new float[6];

    [Header("Tap音符预制件(Tap Note Prefabs)")]
    [Tooltip("宽度为1的普通Tap")] public GameObject tapNotePrefabW1;
    [Tooltip("宽度为2的普通Tap")] public GameObject tapNotePrefabW2;
    [Tooltip("宽度为3的普通Tap")] public GameObject tapNotePrefabW3;
    [Tooltip("宽度为1的金色Tap")] public GameObject goldenTapNotePrefabW1;
    [Tooltip("宽度为2的金色Tap")] public GameObject goldenTapNotePrefabW2;
    [Tooltip("宽度为3的金色Tap")] public GameObject goldenTapNotePrefabW3;
    // 你可以根据需要扩展到宽度4, 5, 6，如果需要的话

    [Header("Hold音符预制件(Hold Note Prefabs)")]
    [Tooltip("宽度为1的普通Hold音符(包含起始键和管理脚本)")] public GameObject holdNotePrefabW1;
    [Tooltip("宽度为2的普通Hold音符(包含起始键和管理脚本)")] public GameObject holdNotePrefabW2;
    [Tooltip("宽度为3的普通Hold音符(包含起始键和管理脚本)")] public GameObject holdNotePrefabW3;
    [Tooltip("宽度为1的金色Hold音符(包含起始键和管理脚本)")] public GameObject goldenHoldNotePrefabW1;
    [Tooltip("宽度为2的金色Hold音符(包含起始键和管理脚本)")] public GameObject goldenHoldNotePrefabW2;
    [Tooltip("宽度为3的金色Hold音符(包含起始键和管理脚本)")] public GameObject goldenHoldNotePrefabW3;
    // 注意: Hold的连接体拉伸和尾键显示应由Hold预制件上的脚本处理

    [Header("生成与判定位置(Spawning & Judgement Position)")]
    [Tooltip("音符生成的起始Y坐标")] public float spawnYPosition=175f;
    [Tooltip("判定线的Y坐标")] public float judgementYPosition=-4.5f;
    [Tooltip("屏幕最下方的Y坐标")] public float screenBottomYPosition=-7.5f;

    [Header("时间与速度(Timing $ Speed)")]
    [Tooltip("音符从生成到到达判定区域所需的【固定】时间(秒)")] public float noteTravelTime=2f;

    private ChartLoader chartLoader;
    private List<NoteData> notesToSpawn;
    private int nextNoteIndex=0;

    private AudioSource audioSource;
    public float songStartTimeDsp {get;private set;}

    private JudgementManager judgementManager; //引用

    public bool isAudioReady=false; //标记音频是否加载完成

    void Start()
    {
        chartLoader=FindFirstObjectByType<ChartLoader>();
        audioSource=GetComponent<AudioSource>();
        judgementManager=JudgementManager.Instance;

        if(chartLoader==null||chartLoader.loadedChartData==null)
        {
            Debug.LogError("NoteSpawner无法获取谱面数据!");
            this.enabled=false;
            return;
        }
        // 检查所有必要的预制件是否已设置
        if(tapNotePrefabW1==null||tapNotePrefabW2==null||tapNotePrefabW3==null||
           goldenTapNotePrefabW1==null||goldenTapNotePrefabW2==null||goldenTapNotePrefabW3==null||
           holdNotePrefabW1==null||holdNotePrefabW2==null||holdNotePrefabW3==null||goldenHoldNotePrefabW1==null||goldenHoldNotePrefabW2==null||goldenHoldNotePrefabW3==null)
        {
            Debug.LogError("部分或全部音符预制件(Note Prefabs)未在NoteSpawner中设置!");
            this.enabled=false;
            return;
        }

        bool spawnPointsValid=(trackSpawnPoints!=null&&trackSpawnPoints.Length>=6);
        bool xPositionValid=(trackXPositions!=null&&trackXPositions.Length>=6);
        if(!spawnPointsValid&&!xPositionValid)
        {
            Debug.LogError("轨道生成点(Track Spawn Points)或X坐标(Track X Positions)未完全设置(需要6个)!");
            this.enabled=false;
            return;
        }

        if(judgementManager==null)
        {
            Debug.LogError("NoteSpawner无法找到JudgementManager!");
            this.enabled=false;
            return;
        }

        if(audioSource==null)
        {
            Debug.LogError("AudioSource未找到!");
            this.enabled=false;
            return;
        }

        string audioToLoad=null;
        if(SelectedSongData.Instance!=null&&!string.IsNullOrEmpty(SelectedSongData.Instance.selectedAudioFileName))
        {
            audioToLoad=SelectedSongData.Instance.selectedAudioFileName;
            Debug.Log($"从SelectedSongData加载音频: {audioToLoad}");
            StartCoroutine(LoadAudioClipFromStreamingAssets(audioToLoad));
        }
        else
        {
            Debug.LogError("SelectedSongData未找到或未设置音频文件名。");
            this.enabled=false;
            return;
        }
    }

    IEnumerator LoadAudioClipFromStreamingAssets(string audioFileName)
    {
        string audioPath=Path.Combine(Application.streamingAssetsPath,audioFileName);
        string fullPath="file://"+audioPath;
        AudioType audioType=GetAudioTypeFromExtension(Path.GetExtension(audioFileName));
        if(audioType==AudioType.UNKNOWN)
        {
            Debug.LogError($"未知的音频文件类型: {audioFileName}");
            yield break;
        }
        Debug.Log($"尝试加载音频: {fullPath} (type: {audioType})");

        using(UnityWebRequest www=UnityWebRequestMultimedia.GetAudioClip(fullPath,audioType))
        {
            yield return www.SendWebRequest();

            if(www.result==UnityWebRequest.Result.ConnectionError||www.result==UnityWebRequest.Result.ProtocolError||www.result==UnityWebRequest.Result.DataProcessingError)
            {
                Debug.LogError($"加载音频失败: {www.error} at path {fullPath}");
            }
            else
            {
                AudioClip clip=DownloadHandlerAudioClip.GetContent(www);
                if(clip!=null)
                {
                    clip.name=Path.GetFileNameWithoutExtension(audioFileName);
                    audioSource.clip=clip;
                    isAudioReady=true;
                    Debug.Log($"音频加载完成:{clip.name}");
                    InitializeGameplay();
                }
                else
                {
                    Debug.LogError($"无法从下载的数据中获取AudioClip: {fullPath}");
                }
            }
        }
    }

    AudioType GetAudioTypeFromExtension(string extension)
    {
        extension=extension.ToLower();
        switch(extension)
        {
            case ".mp3": 
            case ".aac": return AudioType.MPEG;
            case ".wav": return AudioType.WAV;
            case ".ogg": return AudioType.OGGVORBIS;
            case ".aiff": return AudioType.AIFF;
            default: return AudioType.UNKNOWN;
        }
    }

    void InitializeGameplay()
    {
        notesToSpawn=chartLoader.loadedChartData.notes;
        nextNoteIndex=0;

        float startDelay=2.0f;
        songStartTimeDsp=(float)AudioSettings.dspTime+startDelay+chartLoader.loadedChartData.offset;
        audioSource.PlayScheduled(songStartTimeDsp);
    }

    void Update()
    {
        if(notesToSpawn==null||!isAudioReady)
        {
            return;
        }

        float currentSongTime=(float)AudioSettings.dspTime-songStartTimeDsp;
        if(nextNoteIndex>=notesToSpawn.Count&&currentSongTime>=chartLoader.loadedChartData.songDuration) // 确保所有音符都生成完毕
        {
            Debug.Log("歌曲结束，准备结算");
            if(GameManager.Instance!=null)
            {
                GameManager.Instance.GoToResultsScene();
            }
            else
            {
                Debug.LogError("GameManager.Instance 未找到! 无法跳转到结算场景。");
            }
        }

        while(nextNoteIndex<notesToSpawn.Count)
        {
            NoteData nextNote=notesToSpawn[nextNoteIndex];

            float targetSpawnTime=nextNote.time-noteTravelTime;
            if(currentSongTime>=targetSpawnTime)
            {
                if(nextNote.lane<0||nextNote.lane>=6||nextNote.width<1||nextNote.lane+nextNote.width>6)
                {
                    Debug.LogWarning($"音符时间{nextNote.time}的轨道({nextNote.lane})或宽度({nextNote.width})无效，已跳过。");
                    goto SkipNote;
                }

                // 1. 选择正确的预制件 (根据类型和宽度)
                GameObject prefabToSpawn=null;
                switch(nextNote.type)
                {
                    case NoteType.Tap:
                        switch(nextNote.width)
                        {
                            case 1: prefabToSpawn=tapNotePrefabW1; break;
                            case 2: prefabToSpawn=tapNotePrefabW2; break;
                            case 3: prefabToSpawn=tapNotePrefabW3; break;
                            default: Debug.LogWarning($"不支持的普通Tap宽度: {nextNote.width}"); break;
                        }
                        break;
                    case NoteType.GoldenTap:
                         switch(nextNote.width)
                        {
                            case 1: prefabToSpawn=goldenTapNotePrefabW1; break;
                            case 2: prefabToSpawn=goldenTapNotePrefabW2; break;
                            case 3: prefabToSpawn=goldenTapNotePrefabW3; break;
                            default: Debug.LogWarning($"不支持的金色Tap宽度: {nextNote.width}"); break;
                        }
                        break;
                    case NoteType.HoldStart:
                    case NoteType.HoldEnd:
                        switch(nextNote.width)
                        {
                            case 1: prefabToSpawn=holdNotePrefabW1; break;
                            case 2: prefabToSpawn=holdNotePrefabW2; break;
                            case 3: prefabToSpawn=holdNotePrefabW3; break;
                            default: Debug.LogWarning($"不支持的Hold宽度: {nextNote.width}"); break;
                        }
                        break;
                    case NoteType.GoldenHoldStart:
                    case NoteType.GoldenHoldEnd:
                        switch(nextNote.width)
                        {
                            case 1: prefabToSpawn=goldenHoldNotePrefabW1; break;
                            case 2: prefabToSpawn=goldenHoldNotePrefabW2; break;
                            case 3: prefabToSpawn=goldenHoldNotePrefabW3; break;
                            default: Debug.LogWarning($"不支持的金色Hold宽度: {nextNote.width}"); break;
                        }
                        break;
                    default:
                        Debug.LogWarning($"未知的音符类型: {nextNote.type}，无法生成。");
                        break; // 直接跳过未知类型
                }

                if(prefabToSpawn==null) // 如果没有找到合适的预制件
                {
                    Debug.LogError($"没有为类型 {nextNote.type} 宽度 {nextNote.width} 找到合适的预制件！");
                    goto SkipNote;
                }

                // 2. 计算生成位置 (X坐标居中)
                Vector3 spawnPos;
                float firstLaneX, lastLaneX, centerX;
                // (计算 spawnPos 的逻辑保持不变，根据 trackSpawnPoints 或 trackXPositions 计算中心点 centerX)
                 if(trackSpawnPoints!=null&&trackSpawnPoints.Length>=6)
                {
                    if(nextNote.lane<trackSpawnPoints.Length&&trackSpawnPoints[nextNote.lane]!=null&&
                       nextNote.lane+nextNote.width-1<trackSpawnPoints.Length&&trackSpawnPoints[nextNote.lane+nextNote.width-1]!=null)
                    {
                        firstLaneX=trackSpawnPoints[nextNote.lane].position.x;
                        lastLaneX=trackSpawnPoints[nextNote.lane+nextNote.width-1].position.x;
                        centerX=(firstLaneX+lastLaneX)/2f;
                        spawnPos=new Vector3(centerX,spawnYPosition,trackSpawnPoints[nextNote.lane].position.z);
                    }
                    else
                    {
                         Debug.LogError($"轨道{nextNote.lane}或宽度{nextNote.width}对应的TrackSpawnPoints无效!");
                         goto SkipNote;
                    }
                }
                else if(trackXPositions!=null&&trackXPositions.Length>=6)
                {
                     if(nextNote.lane<trackXPositions.Length&&
                       nextNote.lane+nextNote.width-1<trackXPositions.Length)
                    {
                        firstLaneX=trackXPositions[nextNote.lane];
                        lastLaneX=trackXPositions[nextNote.lane+nextNote.width-1];
                        centerX=(firstLaneX+lastLaneX)/2f;
                        spawnPos=new Vector3(centerX,spawnYPosition,0f);
                    }
                    else
                    {
                        Debug.LogError($"轨道{nextNote.lane}或宽度{nextNote.width}对应的TrackXPositions无效!");
                        goto SkipNote;
                    }
                }
                else
                {
                    Debug.LogError($"无法确定轨道{nextNote.lane}的生成位置!");
                    goto SkipNote;
                }


                // 3. 实例化音符
                GameObject newNote=Instantiate(prefabToSpawn,spawnPos,prefabToSpawn.transform.rotation);
                newNote.transform.SetParent(this.transform,true);

                NoteMovement noteMovement=newNote.GetComponent<NoteMovement>();
                if(noteMovement!=null)
                {
                    float holdEndTime=-1f;
                    if(nextNote.type==NoteType.HoldStart||nextNote.type==NoteType.GoldenHoldStart)
                    {
                        NoteData holdEndData=FindHoldEndData(nextNote.id);
                        if(holdEndData!=null)
                        {
                            holdEndTime=holdEndData.time;
                            // 生成endNote
                        }
                        else
                        {
                            Debug.LogError($"关键错误: 无法为ID为 {nextNote.id} 的HoldStart找到对应的HoldEnd数据!");
                        }
                    }
                    // NoteMovement的Initialize方法需要能处理NoteData和Hold结束时间
                    noteMovement.Initialize(spawnYPosition,judgementYPosition,screenBottomYPosition,noteTravelTime,nextNote,songStartTimeDsp,holdEndTime);
                }
                else
                {
                    Debug.LogWarning($"生成的音符Prefab '{prefabToSpawn.name}' 上没有找到NoteMovement脚本!");
                }

                judgementManager.RegisterActiveNote(nextNote,newNote);

                SkipNote:
                    nextNoteIndex++;
            }
            else
            {
                break;
            }
        }
    }

    private NoteData FindHoldEndData(int holdId)
    {
        for(int i=nextNoteIndex; i<notesToSpawn.Count; i++)
        {
            NoteData note=notesToSpawn[i];
            if((note.type==NoteType.HoldEnd||note.type==NoteType.GoldenHoldEnd)&&note.id==holdId)
            {
                return note;
            }
        }
        return null;
    }
}
