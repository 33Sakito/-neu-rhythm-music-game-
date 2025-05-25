using UnityEngine;

public class NoteMovement:MonoBehaviour
{
    public NoteData noteInfo;
    private float judgementY;
    private float screenBottomY;
    private float travelTime;
    private float wholeTravelTime;
    private float targetJudgeTime;
    private float startY;
    private float startTimeDsp;
    private float songStartTime;
    private float speed;
    private bool initialized=false;

    private bool isHoldNote=false;
    private float holdEndTime=-1f;
    private GameObject holdBodyInstance=null;
    public GameObject holdBodyPrefab;
    public GameObject goldenHoldBodyPrefab; 
    private Transform noteHead;


    private bool isHeldAtJudgementLine=false; // 标记Hold音符是否被按住并保持在判定线
    private float actualJudgementY; // 实际保持的Y坐标
    private JudgementManager judgementManagerInstance; // JudgementManager的引用
    
    // --- 硬编码值 ---
    private float bodyOriginalWidth=5.12f;
    private float bodyOriginalHeight=1.35f; 
    private float holdTapHeight=0.8f;
    private float holdTapWidth=2.56f;


    public void Initialize(float spawnY,float judgeY,float bottomY,float travelT,NoteData noteData,float songStartDspTime,float holdEndTimeIfAny)
    {
        this.noteInfo=noteData;
        this.judgementY=judgeY;
        this.screenBottomY=bottomY;
        this.startY=spawnY;
        this.travelTime=travelT;
        this.targetJudgeTime=noteData.time;
        this.songStartTime=songStartDspTime;
        this.startTimeDsp=this.targetJudgeTime-this.travelTime+this.songStartTime;
        this.speed=Mathf.Abs(this.judgementY-this.startY)/this.travelTime;
        if(speed==0)
        {
            this.wholeTravelTime=this.travelTime;
            Debug.LogWarning("Note speed is zero, check spawnY and judgementY.");
        }
        else
        {
            this.wholeTravelTime=Mathf.Abs(this.screenBottomY-this.startY)/speed;
        }

        this.noteHead=this.transform;

        if(noteData.type==NoteType.HoldStart||noteData.type==NoteType.GoldenHoldStart)
        {
            this.isHoldNote=true;
            this.holdEndTime=holdEndTimeIfAny;
            if(this.holdEndTime<=this.targetJudgeTime)
            {
                Debug.LogError($"Hold音符(ID: {noteData.id})的结束时间({this.holdEndTime})不大于开始时间({this.targetJudgeTime})!");
                this.isHoldNote=false;
            }
            else if(holdBodyPrefab!=null&&goldenHoldBodyPrefab!=null)
            {
                if(noteData.type==NoteType.HoldStart) holdBodyInstance=Instantiate(holdBodyPrefab,this.noteHead.position,Quaternion.identity);
                else holdBodyInstance=Instantiate(goldenHoldBodyPrefab,this.noteHead.position,Quaternion.identity);
                holdBodyInstance.transform.localScale=new Vector3(holdBodyInstance.transform.localScale.x,0.01f,holdBodyInstance.transform.localScale.z);
                holdBodyInstance.transform.localPosition=new Vector3(0,-0.01f,0); // 这个localPosition是针对holdBodyInstance自身的，不是父子关系
            }
            else
            {
                Debug.LogError("HoldBodyPrefab未在NoteMovement脚本的Inspector中设置! Hold连接体无法生成。");
                this.isHoldNote=false;
            }
        }
        else
        {
            this.isHoldNote=false;
        }

        // 获取JudgementManager实例
        judgementManagerInstance=JudgementManager.Instance;

        this.initialized=true;
    }

    void Update()
    {
        if(!initialized) return;

        float currentTimeDsp=(float)AudioSettings.dspTime;
        
        float currentY;
        float currentSongPlayTime=currentTimeDsp-songStartTime; // 当前歌曲的播放时间（从0开始）

        if(isHeldAtJudgementLine)
        {
            currentY=actualJudgementY; // 头部Y坐标固定
            noteHead.position=new Vector3(noteHead.position.x,currentY,noteHead.position.z);
        }
        else
        {
            float timeSinceMovementStart=currentTimeDsp-startTimeDsp;
            float progress=timeSinceMovementStart/wholeTravelTime;
            if(timeSinceMovementStart<=wholeTravelTime)
            {
                currentY=Mathf.Lerp(startY,screenBottomY,progress);
            }
            else
            {
                float timeAfterReachBottom=timeSinceMovementStart-wholeTravelTime;
                currentY=screenBottomY-(this.speed*timeAfterReachBottom);
            }
            noteHead.position=new Vector3(noteHead.position.x,currentY,noteHead.position.z);
        }

        // --- Hold 音符连接体处理 ---
        if(isHoldNote&&holdBodyInstance!=null)
        {
            float lengthOfConnectorToShow; // 当前需要显示的连接体长度
            float widthOfConnectorToShow=noteInfo.width*holdTapWidth; // 当前需要显示的连接体宽度

            if(isHeldAtJudgementLine)
            {
                // 头部固定，连接体尾端向判定线移动（视觉上连接体从头部向下变短）
                float timeUntilHoldActualEnd=this.holdEndTime-currentSongPlayTime; // 距离Hold谱面结束时间的剩余秒数
                // 连接体的视觉长度是从固定头部下方到移动的尾巴的长度
                lengthOfConnectorToShow=timeUntilHoldActualEnd*this.speed; 
                lengthOfConnectorToShow=Mathf.Max(0.001f,lengthOfConnectorToShow); // 保持一个极小值避免完全消失或负数

                holdBodyInstance.transform.localScale=new Vector3(
                    widthOfConnectorToShow/bodyOriginalWidth,
                    lengthOfConnectorToShow/bodyOriginalHeight, // Y轴缩放
                    holdBodyInstance.transform.localScale.z
                );
                float headBottomY=currentY-(holdTapHeight*0.5f);
                holdBodyInstance.transform.position=new Vector3(
                    noteHead.position.x,
                    headBottomY+(lengthOfConnectorToShow*0.5f), //身体中心Y = 头部底部Y - 身体长度一半
                    noteHead.position.z
                );
            }
            else
            {
                // requiredBodyLength是Hold从头到尾的总长度（连接部分）
                float requiredBodyLength=(this.holdEndTime-this.noteInfo.time)*this.speed-this.holdTapHeight;
                requiredBodyLength=Mathf.Max(0.001f,requiredBodyLength);

                holdBodyInstance.transform.localScale=new Vector3(
                    widthOfConnectorToShow/bodyOriginalWidth,
                    requiredBodyLength/bodyOriginalHeight,
                    holdBodyInstance.transform.localScale.z
                );
                 holdBodyInstance.transform.position=new Vector3(
                    noteHead.position.x, 
                    noteHead.position.y+(requiredBodyLength+holdTapHeight)/2f,
                    noteHead.position.z
                );
            }
        }

        // --- 销毁逻辑 ---
        // Tap音符如果Miss了，或者Hold音符结束后，会自然下落并在这里销毁
        // 被正确判定的Tap音符会由JudgementManager通过ForceDestroy()提前销毁
        bool shouldBeDestroyedByMovement=false;
        if(!isHeldAtJudgementLine) // 只有在非保持状态下才考虑因移动出屏幕而销毁
        {
            if(isHoldNote)
            {
                // Hold音符：当其谱面时间结束，并且头部也移出屏幕底部时
                if(currentSongPlayTime>this.holdEndTime+0.5f&& // 给一点缓冲时间在Hold结束后下落
                   noteHead.position.y<screenBottomY-holdTapHeight) // 头部完全离开
                {
                    shouldBeDestroyedByMovement=true;
                }
            }
            // 对于所有类型的音符（包括可能被Miss的Tap），如果它们移出屏幕太远
            else if(noteHead.position.y<screenBottomY-2.0f) // 2.0f是一个通用缓冲
            {
                shouldBeDestroyedByMovement=true;
            }
        }

        if(shouldBeDestroyedByMovement)
        {
            // 在销毁前尝试从JudgementManager注销
            // OnDestroy() 也会做这件事，但这里更主动
            if(judgementManagerInstance!=null&&this.noteInfo!=null)
            {
                judgementManagerInstance.UnregisterNote(this.noteInfo);
            }
            // 先销毁关联对象，再销毁自身
            if(holdBodyInstance!=null) Destroy(holdBodyInstance);
            Destroy(gameObject);
        }
    }

    // --- 新增 Public 方法供 JudgementManager 调用 ---
    public void EngageHold(float yToHoldAt)
    {
        isHeldAtJudgementLine=true;
        actualJudgementY=yToHoldAt;
        // 立即将头部移动到判定线 (Y坐标可能在Update中再次被设置，但这里确保及时性)
        noteHead.position=new Vector3(noteHead.position.x,actualJudgementY,noteHead.position.z);
    }

    public void ReleaseHold()
    {
        isHeldAtJudgementLine=false;
        // 音符将从当前位置（判定线）继续下落，Update中的移动逻辑会自动接管
    }

    public bool IsHeld()
    {
        return isHeldAtJudgementLine;
    }

    public float GetHoldEndTime()
    {
        return this.holdEndTime;
    }

    // 由JudgementManager在判定Tap或非正常结束Hold时调用，立即销毁音符
    public void ForceDestroy()
    {
        if(judgementManagerInstance!=null&&this.noteInfo!=null)
        {
            judgementManagerInstance.UnregisterNote(this.noteInfo); // 确保注销
        }
        if(holdBodyInstance!=null) Destroy(holdBodyInstance);
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        // 确保从JudgementManager中注销，这是最后防线
        if(judgementManagerInstance!=null&&this.noteInfo!=null)
        {
             judgementManagerInstance.UnregisterNote(this.noteInfo);
        }
        // 确保清理，以防ForceDestroy未被调用或Update中的销毁逻辑未完全执行
        if(holdBodyInstance!=null&&holdBodyInstance.gameObject!=null) Destroy(holdBodyInstance.gameObject);
    }
}