using System.Collections.Generic; // 需要 List
using UnityEngine;


public enum NoteType
{
    Tap,            // 普通单键
    GoldenTap,      // 金色单键
    HoldStart,      // 普通长按开始
    HoldEnd,        // 普通长按结束
    GoldenHoldStart,// 金色长按开始
    GoldenHoldEnd   // 金色长按结束
}


[System.Serializable]
public class NoteData
{
    public NoteType type;   // 音符的类型
    public float time;      // 精确判定时间 (秒)
    public int lane;   // 音符起始轨道索引 (0-5)
    public int width;       // 音符占据的轨道数量 (宽度, >= 1)
    public int id;          // 用于关联长按(Hold)开始和结束的唯一ID
}

[System.Serializable]
public class ChartData
{
    public string songName;
    public string artist;
    public List<BpmPoint> bpmPoints; // 存储BPM点的列表
    public float offset;    // 音频偏移量 (秒)
    public List<NoteData> notes; // 存储所有音符数据的列表
    public float songDuration; // 歌曲总时长 (秒)

    public float GetBpm(float time)
    {
        if(time<0f) return 0f; // 时间小于0时返回0BPM
        // 查找小于等于给定时间的最大BPM点
        float lastBpm=0f;
        foreach (var bpmPoint in bpmPoints)
        {
            if(time>=bpmPoint.time)
            {
                lastBpm=bpmPoint.bpm;
            }
            else if(lastBpm!=0) // 找到小于给定时间的最大BPM点
            {
                break;
            }
            else 
            {
                Debug.LogError("没有找到小于给定时间的最大BPM点");
            }
        }
        return lastBpm;
    }
}

public class BpmPoint
{
    public float time;      // 时间点 (秒)
    public float bpm;
}
