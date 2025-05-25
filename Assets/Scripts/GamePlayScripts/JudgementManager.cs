// JudgementManager.cs
using System.Collections.Generic;
using System.Collections;
using System;
using UnityEngine;

public class JudgementManager:MonoBehaviour
{
    [Header("判定距离窗口 (Y轴距离)")]
    public float perfectWindowDistance=0.6f; // Perfect判定的距离无差范围 (+/-)
    public float greatWindowDistance=1.2f;   // Great判定的距离无差范围 (+/-)
    public float goodWindowDistance=1.8f;   // Good判定的距离无差范围 (+/-)
    public float badWindowDistance=2.5f;     // Bad判定的距离无差范围 (+/-)

    [Header("按键与轨道")]
    public KeyCode[] laneKeyCodes=new KeyCode[6] {KeyCode.S,KeyCode.D,KeyCode.F,KeyCode.J,KeyCode.K,KeyCode.L};

    [Header("轨道打击效果")]
    public GameObject[] laneHitEffects=new GameObject[6]; // 每个轨道的高亮效果
    public GameObject[] laneHoldHitEffects=new GameObject[6]; // 每个轨道的Hold高亮效果
    public GameObject[] laneGoldenHitEffects=new GameObject[6]; // 每个轨道的金色高亮效果
    public float glowDuration=0.1f;

    [Header("分数和Combo")]
    private int scorePerfect=100;
    private int scoreGreat=60;
    private int scoreGood=30;
    private int scoreBad=0; // Bad通常不给分或扣一点点
    private int scoreMiss=-20;
    private int scoreHoldTick=10; // Hold持续按住时每拍得分

    private GameResults currentResult;
    private int perfects=0;
    private int greats=0;
    private int goods=0;
    private int bads=0;
    private int misses=0;

    private ChartLoader chartLoader;
    private NoteSpawner noteSpawner;
    private float judgementLineY; // 从NoteSpawner获取判定线Y坐标
    private float beatDuration; // 每拍的持续时间，用于Hold计分

    // 存储当前所有在屏幕上且未被完全判定的音符GameObject
    // Key: NoteData (谱面原始数据), Value: 其对应的活动GameObject
    private Dictionary<NoteData,GameObject> activeGameObjects=new Dictionary<NoteData,GameObject>();
    // 存储当前正在被按住的Hold音符信息
    private Dictionary<int,ActiveHoldInfo> activeHolds=new Dictionary<int,ActiveHoldInfo>(); // Key: Hold音符的ID

    public static JudgementManager Instance {get;private set;}

    private int currentScore=0;
    private int currentCombo=0;
    private int maxCombo=0;

    // 用于跟踪活动Hold音符的状态
    private class ActiveHoldInfo
    {
        public NoteData noteData;       // HoldStart的NoteData
        public GameObject noteObject;   // HoldStart的GameObject
        public NoteMovement noteMovement; // HoldStart的NoteMovement组件
        public float holdEndTime;       // 这个Hold的精确结束时间
        public float nextBeatScoreTime; // 下一个节拍计分的时间点
        public bool playerIsHoldingKey; // 玩家是否当前正按住对应轨道的键
        public HashSet<int> heldLanes = new HashSet<int>(); // 记录此Hold覆盖的且当前被按下的轨道
    }

    void Awake()
    {
        if(Instance!=null&&Instance!=this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance=this;
        }
        currentScore=0;
        currentCombo=0;
        maxCombo=0;
        currentResult=new GameResults();

        if(UIManager.Instance!=null)
        {
            UIManager.Instance.UpdateScore(currentScore);
            UIManager.Instance.UpdateCombo(currentCombo,false);
        }
    }

    void Start()
    {
        chartLoader=FindFirstObjectByType<ChartLoader>();
        noteSpawner=FindFirstObjectByType<NoteSpawner>();

        if(chartLoader==null||chartLoader.loadedChartData==null||noteSpawner==null)
        {
            Debug.LogError("JudgementManager无法找到必要的组件(ChartLoader, NoteSpawner)!");
            this.enabled=false;
            return;
        }

        judgementLineY=noteSpawner.judgementYPosition; // 从NoteSpawner获取判定线Y坐标
        // 计算基础节拍时长 (BPM可能变化，这里用基础BPM简化处理Hold Tick)
        // 对于变速歌曲，Hold Tick的精确计算会更复杂，可能需要基于tick
        float startBpm=chartLoader.loadedChartData.GetBpm(0f);
        if(startBpm>0)
        {
            beatDuration=60f/startBpm;
        }
        else
        {
            beatDuration=0.5f; // 默认值
            Debug.LogWarning("Chart BPM is 0 or invalid, using default beat duration for Hold ticks.");
        }

        activeGameObjects.Clear();
        activeHolds.Clear();
    }
    
    void Update()
    {
        if(!noteSpawner.isAudioReady||chartLoader.loadedChartData==null)
        {
            return;
        }

        if(Input.GetKeyDown(KeyCode.Escape))
        {
            Debug.Log("ESC键被按下，准备提前结束游戏并进入结算。");
            if(GameManager.Instance!=null)
            {
                // 在调用GoToResultsScene之前，确保JudgementManager中的统计数据是最新的
                GameManager.Instance.GoToResultsScene(); 
                return; // 直接返回，避免本帧后续逻辑执行导致错误
            }
            else
            {
                Debug.LogError("GameManager.Instance未找到，无法通过ESC键进入结算。");
            }
        }
        
        float currentSongTime=(float)AudioSettings.dspTime-noteSpawner.songStartTimeDsp;

        // 检测玩家按键输入
        for(int laneIndex=0;laneIndex<6;laneIndex++)
        {
            if(Input.GetKeyDown(laneKeyCodes[laneIndex]))
            {
                TryJudgeNoteOnPress(laneIndex,currentSongTime);
            }
            else if(Input.GetKey(laneKeyCodes[laneIndex]))
            {
                UpdateHoldState(laneIndex,true,currentSongTime);
            }
            else if(Input.GetKeyUp(laneKeyCodes[laneIndex]))
            {
                UpdateHoldState(laneIndex,false,currentSongTime); // 先更新状态
                TryJudgeHoldRelease(laneIndex,currentSongTime);    // 再尝试判定释放
            }
        }

        // 更新正在进行的Hold音符（节拍计分、检查是否断开）
        UpdateActiveHoldsLogic(currentSongTime);

        // 更新GameResults
        UpdateGameResults();

        // 检测并处理Miss的音符 (基于距离)
        CheckForMissedNotesByDistance(currentSongTime);
    }

    void TryJudgeNoteOnPress(int pressedLaneIndex, float currentSongTime)
    {
        GameObject bestCandidateObject=null;
        NoteData bestCandidateData=null;
        float closestDistance=float.MaxValue;

        // 遍历所有活动音符，找到在按下轨道上且最接近判定线的音符
        foreach(var pair in new Dictionary<NoteData, GameObject>(activeGameObjects))
        {
            NoteData noteData=pair.Key;
            GameObject noteObject=pair.Value;

            if(noteObject==null) continue; // 音符可能已被销毁

            // 检查音符是否覆盖了按下的轨道
            if(pressedLaneIndex>=noteData.lane&&pressedLaneIndex<(noteData.lane+noteData.width))
            {
                // 对于Hold音符，只在HoldStart时响应GetKeyDown
                if((noteData.type==NoteType.HoldEnd||noteData.type==NoteType.GoldenHoldEnd))
                {
                    continue; // HoldEnd通过GetKeyUp判定
                }
                 // 如果一个HoldStart已经被激活（即在activeHolds中），则不应再次通过GetKeyDown判定
                if((noteData.type==NoteType.HoldStart||noteData.type==NoteType.GoldenHoldStart)&&activeHolds.ContainsKey(noteData.id))
                {
                    continue;
                }

                float noteYPos=noteObject.transform.position.y;
                float distanceToLine=Mathf.Abs(noteYPos-judgementLineY);
                

                // 寻找在判定窗口内且最接近判定线的音符
                if(distanceToLine<=badWindowDistance) // 初步筛选在最大判定范围内的
                {
                    Debug.Log($"Pressed lane {pressedLaneIndex}, noteYPos: {noteYPos}, distanceToLine: {distanceToLine}, noteData.id: {noteData.id}, currentSongTime: {currentSongTime}");
                    if(distanceToLine<closestDistance)
                    {
                        closestDistance=distanceToLine;
                        bestCandidateObject=noteObject;
                        bestCandidateData=noteData;
                    }    
                }
            }
        }

        if(bestCandidateObject!=null&&bestCandidateData!=null)
        {
            // 执行判定
            JudgementType judgement=EvaluateJudgementByDistance(bestCandidateObject.transform.position.y);
            Debug.Log($"Pressed lane {pressedLaneIndex}, Judgement: {judgement}, 差值: {closestDistance}");
            ProcessJudgement(judgement,pressedLaneIndex,bestCandidateData,bestCandidateObject,true); // isPress=true

            if(judgement!=JudgementType.Miss) // Miss不应该在这里发生，但以防万一
            {
                // 如果是HoldStart，则激活Hold逻辑
                if(bestCandidateData.type==NoteType.HoldStart||bestCandidateData.type==NoteType.GoldenHoldStart)
                {
                    NoteMovement nm=bestCandidateObject.GetComponent<NoteMovement>();
                    if(nm!=null&&!activeHolds.ContainsKey(bestCandidateData.id)) // 防止重复添加
                    {
                        nm.EngageHold(judgementLineY); // 通知NoteMovement保持在判定线
                        ActiveHoldInfo holdInfo=new ActiveHoldInfo
                        {
                            noteData=bestCandidateData,
                            noteObject=bestCandidateObject,
                            noteMovement=nm,
                            holdEndTime=nm.GetHoldEndTime(), // 从NoteMovement获取Hold结束的谱面时间
                            nextBeatScoreTime=bestCandidateData.time+beatDuration, // 第一次节拍计分时间
                            playerIsHoldingKey=true // 初始状态为按住
                        };
                        holdInfo.heldLanes.Add(pressedLaneIndex); // 记录是哪个键按下了这个Hold
                        activeHolds.Add(bestCandidateData.id,holdInfo);
                    }
                }
                else // 普通Tap，判定后直接移除
                {
                    RemoveActiveNote(bestCandidateData,bestCandidateObject,true); // 销毁GameObject
                }
            }
        }
    }
    
    void UpdateHoldState(int laneIndex,bool isKeyDown,float currentSongTime)
    {
        // 遍历所有activeHolds，更新覆盖了当前laneIndex的Hold的按键状态
        foreach(var pair in activeHolds)
        {
            ActiveHoldInfo holdInfo=pair.Value;
            if(laneIndex>=holdInfo.noteData.lane&&laneIndex<(holdInfo.noteData.lane+holdInfo.noteData.width))
            {
                if(isKeyDown)
                {
                    holdInfo.heldLanes.Add(laneIndex);
                }
                else
                {
                    holdInfo.heldLanes.Remove(laneIndex);
                }
                // 更新整体的playerIsHoldingKey状态：只要Hold覆盖的任一轨道被按住，就视为被按住
                holdInfo.playerIsHoldingKey=holdInfo.heldLanes.Count>0;

                // 如果从按住变为不按住，通知NoteMovement
                if(!holdInfo.playerIsHoldingKey&&holdInfo.noteMovement.IsHeld())
                {
                     // 如果此时歌曲时间还没到Hold的结束时间，那么这是个Hold Break
                    if(currentSongTime<holdInfo.holdEndTime)
                    {
                        Debug.Log($"Hold ID {holdInfo.noteData.id} broken at lane {laneIndex} at time {currentSongTime}");
                    }
                }
                else if(holdInfo.playerIsHoldingKey&&!holdInfo.noteMovement.IsHeld()&&currentSongTime<holdInfo.holdEndTime)
                {
                    // 如果之前断了，但又按下了，并且Hold还没结束
                    holdInfo.noteMovement.EngageHold(judgementLineY);
                }
            }
        }
    } 

    void TryJudgeHoldRelease(int releasedLaneIndex, float currentSongTime)
    {
        ActiveHoldInfo activeHoldInstance=null; // 当前活动的HoldStart信息
        NoteData holdEndNoteData=null;          // 对应的HoldEnd的NoteData
        GameObject holdEndObject=null;          // 对应的HoldEnd的GameObject

        //步骤1: 找到与松手相关的活动HoldStart，并找出其对应的HoldEnd对象
        foreach(var pair in activeHolds)
        {
            ActiveHoldInfo currentActiveHold=pair.Value;
            bool coversReleasedLane=releasedLaneIndex>=currentActiveHold.noteData.lane&&releasedLaneIndex<(currentActiveHold.noteData.lane+currentActiveHold.noteData.width);

            if(coversReleasedLane&&!currentActiveHold.playerIsHoldingKey) //玩家确实松开了这个Hold
            {
                activeHoldInstance=currentActiveHold;
                //现在需要找到这个HoldStart对应的HoldEnd的NoteData和GameObject
                foreach(var activeObjPair in activeGameObjects)
                {
                    NoteData potentialEndData=activeObjPair.Key;
                    if(potentialEndData.id==activeHoldInstance.noteData.id&&(potentialEndData.type==NoteType.HoldEnd||potentialEndData.type==NoteType.GoldenHoldEnd))
                    {
                        holdEndNoteData=potentialEndData;
                        holdEndObject=activeObjPair.Value;
                        break; //找到HoldEnd
                    }
                }
                if(holdEndNoteData!=null&&holdEndObject!=null) break; //也找到了HoldStart，可以退出外层循环
                else activeHoldInstance=null; //如果没找到对应的End对象，则此HoldStart无效，继续查找下一个activeHold
            }
        }

        //步骤2: 如果成功找到了HoldStart及其对应的HoldEnd对象
        if(activeHoldInstance!=null&&holdEndNoteData!=null&&holdEndObject!=null)
        {
            JudgementType releaseJudgement;
            float holdEndYPos=holdEndObject.transform.position.y; //获取HoldEnd音符当前的Y坐标
            float distanceToLine=Mathf.Abs(holdEndYPos-judgementLineY); //计算与判定线的距离

            //根据距离判定
            if(distanceToLine<=perfectWindowDistance) releaseJudgement=JudgementType.Perfect;
            else if(distanceToLine<=greatWindowDistance) releaseJudgement=JudgementType.Great;
            else if(distanceToLine<=goodWindowDistance) releaseJudgement=JudgementType.Good;
            else if(distanceToLine<=badWindowDistance) releaseJudgement=JudgementType.Bad;
            else releaseJudgement=JudgementType.Miss; //距离太远则为Miss
            
            //调用ProcessJudgement，判定结果作用于HoldStart的noteData和object，但明确是isHoldEnd事件
            ProcessJudgement(releaseJudgement,releasedLaneIndex,activeHoldInstance.noteData,activeHoldInstance.noteObject,false,true);

            //通知NoteMovement组件，此Hold的按键已松开
            activeHoldInstance.noteMovement.ReleaseHold();
            //从正在进行的Hold列表中移除
            activeHolds.Remove(activeHoldInstance.noteData.id);
            
            //处理HoldStart的NoteData从activeGameObjects移除（不销毁其GameObject，让其自然下落）
            RemoveActiveNote(activeHoldInstance.noteData,activeHoldInstance.noteObject,false);
            //处理HoldEnd的NoteData和GameObject
            RemoveActiveNote(holdEndNoteData,holdEndObject,releaseJudgement!=JudgementType.Miss);
        }
    }
    void UpdateActiveHoldsLogic(float currentSongTime)
    {
        List<int> holdsToRemove=new List<int>();
        float currentBpm=chartLoader.loadedChartData.GetBpm(currentSongTime);
        beatDuration=60f/currentBpm;

        foreach(var pair in activeHolds)
        {
            ActiveHoldInfo holdInfo=pair.Value;
            NoteData holdStartNoteData=holdInfo.noteData;

            // 1. 检查是否持续按住，并进行节拍计分
            if(holdInfo.playerIsHoldingKey&&currentSongTime>=holdInfo.nextBeatScoreTime&&currentSongTime<holdInfo.holdEndTime)
            {
                // 给予节拍分数
                ProcessHoldTickScore(holdInfo);
                holdInfo.nextBeatScoreTime+=beatDuration;
                // 实现持续特效
                for(int l=holdStartNoteData.lane;l<holdStartNoteData.lane+holdStartNoteData.width;l++)
                {
                    if(holdStartNoteData.type==NoteType.HoldStart) laneHoldHitEffects[l].SetActive(true); // 显示Hold持续效果
                    else if(holdStartNoteData.type==NoteType.GoldenHoldStart) laneGoldenHitEffects[l].SetActive(true); // 显示Hold持续效果
                }
            }

            // 2. 检查Hold是否因为未按住而断开 (Break)
            if(!holdInfo.playerIsHoldingKey&&currentSongTime<holdInfo.holdEndTime)
            {
                // 玩家松手了，但Hold还没到结束时间
                Debug.Log($"Hold ID {holdInfo.noteData.id} Broken due to key release before end.");
                ProcessJudgement(JudgementType.Miss,holdInfo.noteData.lane,holdInfo.noteData,holdInfo.noteObject,false,true,true); // isPress=false, isHoldEnd=true (as in it ends now), isBreak=true
                holdInfo.noteMovement.ReleaseHold();
                holdsToRemove.Add(pair.Key);
                RemoveActiveNote(holdInfo.noteData,holdInfo.noteObject,false); // 从activeGameObjects移除，不销毁
                continue; // 处理下一个Hold
            }

            // 3. 检查Hold是否自然结束但玩家没有正确松手 (例如一直按住超过了HoldEnd时间)
            if(currentSongTime>holdInfo.holdEndTime+0.3f) // 超过Hold结束时间太多
            {
                Debug.Log($"Hold ID {holdInfo.noteData.id} Ended (player might have held too long or release was missed).");
                 // 算作Miss的HoldEnd，因为没有正确松手
                ProcessJudgement(JudgementType.Miss,holdInfo.noteData.lane,holdInfo.noteData,holdInfo.noteObject,false,true);
                holdInfo.noteMovement.ReleaseHold();
                holdsToRemove.Add(pair.Key);
                RemoveActiveNote(holdInfo.noteData, holdInfo.noteObject,false);
            }
        }

        foreach(int idToRemove in holdsToRemove)
        {
            activeHolds.Remove(idToRemove);
        }
    }
    
    void ProcessHoldTickScore(ActiveHoldInfo holdInfo)
    {
        currentScore+=scoreHoldTick;
        currentCombo++;
        if(currentCombo>maxCombo) maxCombo=currentCombo;

        if(UIManager.Instance!=null)
        {
            UIManager.Instance.UpdateScore(currentScore);
            UIManager.Instance.UpdateCombo(currentCombo,true); // true for combo animation
            UIManager.Instance.ShowJudgement(JudgementType.Perfect); // Hold Tick通常显示Perfect或特定反馈
            SoundManager.Instance.PlayHoldTickSound(holdInfo.noteData.type);
        }
    }


    JudgementType EvaluateJudgementByDistance(float noteYPosition)
    {
        float distanceDifference=Mathf.Abs(noteYPosition-judgementLineY);
        Debug.Log($"Distance Difference: {distanceDifference},noteYPosition: {noteYPosition} , Perfect: {perfectWindowDistance}, Great: {greatWindowDistance}, Good: {goodWindowDistance}, Bad: {badWindowDistance}");
        if(distanceDifference<=perfectWindowDistance) return JudgementType.Perfect;
        if(distanceDifference<=greatWindowDistance) return JudgementType.Great;
        if(distanceDifference<=goodWindowDistance) return JudgementType.Good;
        if(distanceDifference<=badWindowDistance) return JudgementType.Bad;
        
        return JudgementType.Miss; // 理论上不会到这里，因为TryJudgeNoteOnPress已经筛选过badWindowDistance
    }

    void CheckForMissedNotesByDistance(float currentSongTime)
    {
        List<NoteData> notesToProcessForMiss=new List<NoteData>();
        foreach(var pair in activeGameObjects)
        {
            notesToProcessForMiss.Add(pair.Key);
        }

        foreach(NoteData noteData in notesToProcessForMiss)
        {
            if (!activeGameObjects.TryGetValue(noteData,out GameObject noteObject)||noteObject==null)
            {
                continue; // 音符已被处理或销毁
            }

            // HoldEnd类型的音符不在这里判Miss，它们依赖HoldStart的状态和松手时机
            if(noteData.type==NoteType.HoldEnd||noteData.type==NoteType.GoldenHoldEnd)
            {
                continue;
            }

            // 如果是正在进行的Hold (HoldStart已经被判定，并且在activeHolds中)，则不通过此逻辑判Miss
            if((noteData.type==NoteType.HoldStart||noteData.type==NoteType.GoldenHoldStart)&&activeHolds.ContainsKey(noteData.id))
            {
                // 其Miss/Break逻辑在UpdateActiveHoldsLogic中处理
                continue;
            }

            float noteYPos=noteObject.transform.position.y;
            // 当音符的头部完全低于判定线减去Bad判定窗口的距离时，视为Miss
            // (judgementLineY 通常为负数, e.g., -4.5.  badWindowDistance为正数, e.g., 0.5. Miss触发线在 -4.5 - 0.5 = -5.0)
            if(noteYPos<judgementLineY-badWindowDistance)
            {
                //Debug.Log($"Missed Note: Type={noteData.type}, Time={noteData.time}, Lane={noteData.lane}, Y={noteYPos}");
                ProcessJudgement(JudgementType.Miss,noteData.lane,noteData,noteObject,false); // isPress=false
                RemoveActiveNote(noteData,noteObject,false); // 从字典移除，但不在此处销毁GameObject (交给NoteMovement)
            }
        }
    }

    void ProcessJudgement(JudgementType type,int laneIndex,NoteData judgedNote,GameObject noteObject,bool isPress,bool isHoldEnd=false,bool isHoldBreak=false)
    {
        if(judgedNote==null) return;

        //Debug.Log($"轨道{laneIndex} 时间{judgedNote.time} 类型{judgedNote.type} 判定: {type} (HoldEnd: {isHoldEnd}, Break: {isHoldBreak})");

        bool comboBrokenOrMaintained=type!=JudgementType.Miss&&type!=JudgementType.Bad&&type!=JudgementType.Good;
        bool triggerComboAnimation=false;

        if(isHoldBreak) // Hold断了直接算Miss
        {
            currentScore+=scoreMiss; // 或者特定的Hold Break惩罚
            currentCombo=0;
            comboBrokenOrMaintained = false; // 明确断combo
        }
        else
        {
             switch(type)
            {
                case JudgementType.Perfect: currentScore+=scorePerfect; currentCombo++; perfects++; triggerComboAnimation=true; break;
                case JudgementType.Great: currentScore+=scoreGreat; currentCombo++; greats++; triggerComboAnimation=true; break;
                case JudgementType.Good: currentScore+=scoreGood; currentCombo++; goods++; comboBrokenOrMaintained=true; break;
                case JudgementType.Bad: currentScore+=scoreBad; currentCombo=0; bads++; comboBrokenOrMaintained=false; break;
                case JudgementType.Miss: currentScore+=scoreMiss; currentCombo=0; misses++; comboBrokenOrMaintained=false; break;
            }
        }


        if(currentCombo>maxCombo) maxCombo=currentCombo;
        
        if(UIManager.Instance!=null)
        {
            UIManager.Instance.ShowJudgement(type);
            if(currentScore<0) currentScore=0;
            UIManager.Instance.UpdateScore(currentScore);
            UIManager.Instance.UpdateCombo(currentCombo,triggerComboAnimation);
        }
        if(SoundManager.Instance!=null&&(isPress||isHoldEnd||isHoldBreak)) // 按下、Hold尾或Break时播放音效
        {
            SoundManager.Instance.PlayJudgementSound(judgedNote.type,type);
        }

        // --- 特效处理 ---
        // Hold的持续特效由NoteMovement处理
        if(comboBrokenOrMaintained||((judgedNote.type==NoteType.HoldStart||judgedNote.type==NoteType.GoldenHoldStart)&&type!=JudgementType.Miss))
        {
             // 对于多押音符，所有覆盖的轨道都发光
            for(int l=judgedNote.lane;l<judgedNote.lane+judgedNote.width;l++)
            {
                if(l<6) TriggerHitEffect(l,judgedNote.type);
            }
        }
    }

    void TriggerHitEffect(int laneIndex,NoteType type)
    {
        if(type==NoteType.Tap&&laneIndex>=0&&laneIndex<laneHitEffects.Length&&laneHitEffects[laneIndex]!=null)
        {
            // 可能需要停止之前的协程，如果glowDuration很短或者按键很快
            StopCoroutine(ShowGlowCoroutine(laneHitEffects[laneIndex])); 
            StartCoroutine(ShowGlowCoroutine(laneHitEffects[laneIndex]));
        }
        if(type==NoteType.HoldStart&&laneIndex>=0&&laneIndex<laneHoldHitEffects.Length&&laneHoldHitEffects[laneIndex]!=null)
        {
            laneHoldHitEffects[laneIndex].SetActive(true); // 显示Hold持续效果
        }
        if(type==NoteType.GoldenHoldStart&&laneIndex>=0&&laneIndex<laneHoldHitEffects.Length&&laneHoldHitEffects[laneIndex]!=null)
        {
            laneGoldenHitEffects[laneIndex].SetActive(true); // 显示Hold持续效果
        }
        if((type==NoteType.GoldenTap)&&laneIndex>=0&&laneIndex<laneGoldenHitEffects.Length&&laneGoldenHitEffects[laneIndex]!=null)
        {
            StopCoroutine(ShowGlowCoroutine(laneGoldenHitEffects[laneIndex]));
            StartCoroutine(ShowGlowCoroutine(laneGoldenHitEffects[laneIndex]));
        }  
    }

    IEnumerator ShowGlowCoroutine(GameObject hitEffect)
    {
        hitEffect.SetActive(true);
        yield return new WaitForSeconds(glowDuration);
        hitEffect.SetActive(false);
    }

    public void RegisterActiveNote(NoteData noteData,GameObject noteObject)
    {
        if(!activeGameObjects.ContainsKey(noteData))
        {
            activeGameObjects.Add(noteData,noteObject);
        }
        else
        {
            Debug.LogWarning($"尝试重复注册音符GameObject: Time={noteData.time}, Lane={noteData.lane}");
            activeGameObjects[noteData]=noteObject; // 更新
        }
    }
    
    // 由NoteMovement在自身销毁前调用，或者在JudgementManager判定后调用
    public void UnregisterNote(NoteData noteData)
    {
        if(noteData==null) return;

        if(activeGameObjects.ContainsKey(noteData))
        {
            activeGameObjects.Remove(noteData);
        }
        // 如果是Hold音符，且它在activeHolds中，也需要考虑移除 (通常在Hold结束或Break时已处理)
        if((noteData.type==NoteType.HoldStart||noteData.type==NoteType.GoldenHoldStart)&&activeHolds.ContainsKey(noteData.id))
        {
            // 这种情况通常意味着Hold音符的头部因为完全移出屏幕而被NoteMovement销毁了
            // 而此时Hold逻辑可能还认为它在activeHolds里。
            // 这表示一个异常情况，比如HoldEnd没有被正确判定，或者Hold被Miss后没有从activeHolds移除
            Debug.LogWarning($"Hold ID {noteData.id} was unregistered while still in activeHolds. Possible logic error or missed HoldEnd/Break processing.");
            activeHolds[noteData.id].noteMovement?.ReleaseHold(); // 确保释放
            activeHolds.Remove(noteData.id);
        }
    }

    // 辅助方法，用于在判定后处理音符（主要用于Tap音符的移除和销毁）
    private void RemoveActiveNote(NoteData noteData,GameObject noteObject,bool destroyGameObject)
    {
        if(noteData==null) return;

        //消除持续特效
        if(noteData.type==NoteType.HoldStart||noteData.type==NoteType.GoldenHoldStart)
        {
            for(int l=noteData.lane;l<noteData.lane+noteData.width;l++)
            {
                if(noteData.type==NoteType.HoldStart) laneHoldHitEffects[l].SetActive(false); // 隐藏Hold持续效果
                else if(noteData.type==NoteType.GoldenHoldStart) laneGoldenHitEffects[l].SetActive(false); // 隐藏Hold持续效果
            }
        }

        if(activeGameObjects.ContainsKey(noteData))
        {
            activeGameObjects.Remove(noteData);
        }

        if(destroyGameObject&&noteObject!=null)
        {
            NoteMovement nm=noteObject.GetComponent<NoteMovement>();
            if(nm!=null) nm.ForceDestroy(); // 调用NoteMovement的方法来清理（比如销毁连接体）
            else Destroy(noteObject);
        }
    }

    private void UpdateGameResults()
    {
        if(currentResult==null) currentResult=new GameResults();

        currentResult.finalScore=currentScore;
        currentResult.maxCombo=maxCombo;
        currentResult.perfectCount=perfects;
        currentResult.greatCount=greats;
        currentResult.goodCount=goods;
        currentResult.badCount=bads;
        currentResult.missCount=misses;
    }

    public GameResults GetCurrentResult()
    {
        if(currentResult==null)
        {
            Debug.LogWarning("GetCurrentResult 被调用时 currentResults 为空。");
            return new GameResults(); // 返回一个默认结果;
        }
        return currentResult;
    }
}

public enum JudgementType {Perfect,Great,Good,Bad,Miss}