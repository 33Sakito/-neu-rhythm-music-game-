using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Globalization;

public class ChartLoader:MonoBehaviour
{
    public ChartData loadedChartData {get;private set;}

    // 用于临时存储未配对的长按音符信息
    private class PendingHoldNote
    {
        public int id;
        public int measure; // 小节号
        public int sliceIndex; // 在DataString中的位置索引
        public int numSlices; // DataString的总分片数
        public NoteType type; // HoldStart or HoldEnd (or Golden versions)
        public int lane;
        public int width;
        public float approximateTime; // 粗略计算的时间，用于排序和初步配对
    }

    // 存储BPM定义
    private Dictionary<string,float> bpmDefinitions=new Dictionary<string,float>();
    // 存储BPM变化事件 (小节号 -> BPM编号)
    private Dictionary<int,string> bpmChanges=new Dictionary<int,string>();
    // 存储小节长度变化 (小节号 -> 拍数)
    private Dictionary<int,float> measureLengthChanges=new Dictionary<int,float>();


    void Awake() //游戏开始时加载
    {
        LoadChart();
    }

    void LoadChart()
    {
        string chartToLoad=null;
        if(SelectedSongData.Instance!=null&&!string.IsNullOrEmpty(SelectedSongData.Instance.selectedChartFileName))
        {
            // 确保加载的是 .sus 文件
            chartToLoad=Path.ChangeExtension(SelectedSongData.Instance.selectedChartFileName,".sus");
            Debug.Log($"从SelectedSongData加载谱面: {chartToLoad}");
        }
        else
        {
            Debug.LogError("SelectedSongData未找到或未设置谱面文件名。");
            this.enabled=false;
            return;
        }

        string filePath=Path.Combine(Application.streamingAssetsPath,chartToLoad);
        if(!File.Exists(filePath))
        {
            Debug.LogError($"谱面文件'{filePath}'未找到！");
            this.enabled=false; // 禁用脚本以防后续错误
            return;
        }

        try
        {
            string susContent=File.ReadAllText(filePath);
            if(!string.IsNullOrEmpty(susContent))
            {
                loadedChartData=ParseSusData(susContent);
                if(loadedChartData!=null&&loadedChartData.notes!=null)
                {
                    Debug.Log($"成功加载并解析SUS铺面：{loadedChartData.songName}，包含{loadedChartData.notes.Count}个音符。");
                    // 对音符按时间排序 (非常重要!)
                    loadedChartData.notes.Sort((a,b)=>a.time.CompareTo(b.time));
                }
                else
                {
                    Debug.LogError($"解析SUS谱面文件'{chartToLoad}'失败！");
                    this.enabled=false;
                }
            }
            else
            {
                Debug.LogError($"读取SUS谱面文件'{chartToLoad}'内容为空！");
                this.enabled=false;
            }
        }
        catch(System.Exception e)
        {
            Debug.LogError($"加载或解析SUS文件时发生错误: {e.Message}\n{e.StackTrace}");
            this.enabled=false;
        }
    }

    ChartData ParseSusData(string susContent)
    {
        ChartData chart=new ChartData();
        chart.notes=new List<NoteData>();
        chart.bpmPoints=new List<BpmPoint>();

        // --- 默认值和临时存储 ---
        float currentBpm=120.0f;
        float waveOffset=0.0f;
        int ticksPerBeat=480;
        float beatsPerMeasure=4.0f; // 默认4/4拍
        string initialBpmId="01";

        bpmDefinitions.Clear();
        bpmChanges.Clear(); // Key: measure, Value: bpmId
        measureLengthChanges.Clear(); // Key: measure, Value: beats

        // 用于精确时间计算：存储每个BPM段的 (起始Tick, BPM值)
        List<KeyValuePair<long,float>> bpmTimeline=new List<KeyValuePair<long,float>>();

        // --- 第一遍扫描: 解析元数据和定义 ---
        string[] lines=susContent.Split(new[]{'\r','\n'},System.StringSplitOptions.RemoveEmptyEntries);
        foreach(string line in lines)
        {
            if(!line.StartsWith("#")) continue; // 忽略注释

            string command;
            string value;

            int colonIndex=line.IndexOf(':');
            if(colonIndex>0)
            {
                command=line.Substring(1,colonIndex-1).Trim();
                value=line.Substring(colonIndex+1).Trim();
            }
            else
            {
                // 处理没有冒号的命令，例如 #REQUEST
                string[] parts=line.Substring(1).Split(new[]{' '},2);
                command=parts[0].Trim();
                value=parts.Length>1?parts[1].Trim():"";
            }

            // 去除字符串值的引号
            if(value.StartsWith("\"")&&value.EndsWith("\""))
            {
                value=value.Substring(1,value.Length-2);
            }

            // 解析元数据
            switch(command.ToUpperInvariant())
            {
                case "TITLE": chart.songName=value; break;
                case "ARTIST": chart.artist=value; break;
                case "DURATION": chart.songDuration=float.Parse(value,NumberStyles.Float,CultureInfo.InvariantCulture); break;
                case "WAVEOFFSET":
                    if(float.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out float offset))
                    {
                        waveOffset=offset;
                    }
                    else Debug.LogWarning($"无法解析WAVEOFFSET: {value}");
                    break;
                default:
                    // 解析BPM定义 (#BPMzz: value)
                    if(command.StartsWith("BPM")&&command.Length==5) // e.g., BPM01
                    {
                        string bpmId=command.Substring(3);
                        if(float.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out float bpmVal))
                        {
                            bpmDefinitions[bpmId]=bpmVal;
                            // 如果这是第一个定义的BPM，设为初始BPM
                            if(bpmDefinitions.Count==1)
                            {
                                currentBpm=bpmVal;
                                initialBpmId=bpmId; // 记录第一个BPM的ID
                            }
                        }
                        else Debug.LogWarning($"无法解析BPM定义: {line}");
                    }
                    // 解析 #REQUEST "ticks_per_beat <value>"
                    else if(command=="REQUEST"&&value.StartsWith("ticks_per_beat"))
                    {
                        string tickValStr=value.Substring("ticks_per_beat".Length).Trim();
                        if(int.TryParse(tickValStr,out int tpb))
                        {
                            ticksPerBeat=tpb;
                        }
                        else Debug.LogWarning($"无法解析ticks_per_beat: {value}");
                    }
                    // 解析小节长度变化 (#mmm02: value) 和 BPM变化 (#mmm08: zz)
                    else if(command.Length==5&&int.TryParse(command.Substring(0,3),out int measureNum))
                    {
                        string typeCode=command.Substring(3);
                        if(typeCode=="02") // 小节长度
                        {
                            if(float.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out float beats))
                            {
                                measureLengthChanges[measureNum]=beats;
                            }
                            else Debug.LogWarning($"无法解析小节长度: {line}");
                        }
                        else if(typeCode=="08") // BPM变化应用
                        {
                            bpmChanges[measureNum]=value; // value是BPM ID (e.g., "01")
                        }
                    }
                    break;
            }
        }

        // 设置ChartData中的基础BPM和Offset
        chart.bpmPoints.Add(new BpmPoint{time=0,bpm=bpmDefinitions.ContainsKey(initialBpmId)?bpmDefinitions[initialBpmId]:currentBpm}); // 使用第一个定义的BPM作为基础值
        chart.offset=waveOffset;

        // 初始化BPM时间线，至少包含一个初始BPM点
        bpmTimeline.Add(new KeyValuePair<long,float>(0,chart.bpmPoints[0].bpm));

        // --- 第二遍扫描: 解析音符数据 ---
        long currentTick=0;
        int nextNoteId=0; // 用于生成唯一的音符ID
        Dictionary<int,PendingHoldNote> openHolds=new Dictionary<int,PendingHoldNote>(); // Key: lane, Value: 正在进行的HoldStart

        // 预处理BPM变化，将其转换为 (tick, bpmValue) 形式并排序
        // 这一步是为了在音符解析前就建立好BPM时间线
        long tempTickCounter=0;
        float tempCurrentBeatsPerMeasure=4.0f; // 默认4/4拍
        float tempCurrentBpmValue=chart.bpmPoints[0].bpm;

        // 找出谱面最大小节数，以便迭代
        int maxMeasure=0;
        foreach(string line in lines)
        {
            if(line.StartsWith("#")&&line.Contains(":")&&line.Length>3)
            {
                if(int.TryParse(line.Substring(1,3),out int m))
                {
                    maxMeasure=Mathf.Max(maxMeasure,m);
                }
            }
        }

        // 遍历所有小节，完成bpmTimeline的构建
        for(int m=0; m<=maxMeasure; m++)
        {
            // 更新当前小节的拍数
            if(measureLengthChanges.ContainsKey(m))
            {
                tempCurrentBeatsPerMeasure=measureLengthChanges[m];
            }
            // 更新当前小节的BPM
            if(bpmChanges.ContainsKey(m))
            {
                string bpmIdToUse=bpmChanges[m];
                if(bpmDefinitions.ContainsKey(bpmIdToUse))
                {
                    float newBpm=bpmDefinitions[bpmIdToUse];
                    if(newBpm!=tempCurrentBpmValue) // 只有BPM实际改变时才添加
                    {
                        // 确保BPM时间线中的tick是唯一的，如果当前tick已存在，则更新其BPM
                        int existingIndex=bpmTimeline.FindIndex(kvp=>kvp.Key==tempTickCounter);
                        if(existingIndex!=-1)
                        {
                            bpmTimeline[existingIndex]=new KeyValuePair<long,float>(tempTickCounter,newBpm);
                        }
                        else
                        {
                             bpmTimeline.Add(new KeyValuePair<long,float>(tempTickCounter,newBpm));
                        }
                        tempCurrentBpmValue=newBpm;
                    }
                }
            }
            tempTickCounter+=(long)(tempCurrentBeatsPerMeasure*ticksPerBeat);
        }
        // 对BPM时间线按tick排序，确保顺序正确
        bpmTimeline.Sort((a,b)=>a.Key.CompareTo(b.Key));
        // 移除重复tick的条目，保留最后一个（最新的BPM）
        for(int i=bpmTimeline.Count-1; i>0; i--)
        {
            if(bpmTimeline[i].Key==bpmTimeline[i-1].Key)
            {
                bpmTimeline.RemoveAt(i-1); // 移除较早的那个
            }
        }

        // 添加bpmPoints到ChartData
        chart.bpmPoints.Clear(); // 清空现有的bpmPoints
        for(int i=0;i<bpmTimeline.Count;i++)
        {
            chart.bpmPoints.Add(new BpmPoint() {time=ConvertTickToSeconds(bpmTimeline[i].Key,ticksPerBeat,bpmTimeline),bpm=bpmTimeline[i].Value});
        }


        // --- 正式解析音符 ---
        currentTick=0; // 重置tick计数器
        beatsPerMeasure=4.0f; // 重置为默认
        currentBpm=chart.bpmPoints[0].bpm; // 重置为初始BPM

        for(int currentMeasure=0; currentMeasure<=maxMeasure; currentMeasure++)
        {
            // 更新当前小节的BPM和拍号 (仅用于计算小节内tick)
            if(bpmChanges.ContainsKey(currentMeasure))
            {
                string bpmIdToUse=bpmChanges[currentMeasure];
                if(bpmDefinitions.ContainsKey(bpmIdToUse))
                {
                    currentBpm=bpmDefinitions[bpmIdToUse];
                }
            }
            if(measureLengthChanges.ContainsKey(currentMeasure))
            {
                beatsPerMeasure=measureLengthChanges[currentMeasure];
            }

            long ticksInCurrentMeasure=(long)(beatsPerMeasure*ticksPerBeat);

            foreach(string line in lines)
            {
                if(!line.StartsWith($"#{currentMeasure:D3}")) continue;
                int colonIdx=line.IndexOf(':');
                if(colonIdx<0) continue;

                string header=line.Substring(1,colonIdx-1);
                string dataString=line.Substring(colonIdx+1).Trim();

                // 验证Header格式 #mmm1x (Tap) 或 #mmm3x0/mmm3x1 (Hold)
                if(header.Length<5||header.Length>6) continue; // 长度必须是5或6
                if(!int.TryParse(header.Substring(0,3),out int measureNum)||measureNum!=currentMeasure) continue;

                char channelType=header[3]; // '1' for Tap, '3' for Hold
                char laneMarker;
                // Hold的格式是 #mmm3x0/mmm3x1, laneMarker是x, 最后一个字符是'0'或'1' (代表通道)
                // Tap的格式是 #mmm1x, laneMarker是x
                if(channelType=='3'&&header.Length==6) // Hold
                {
                    laneMarker=header[4];
                    // char holdChannel = header[5]; // 应该是 '0'
                }
                else if(channelType=='1'&&header.Length==5) // Tap
                {
                    laneMarker=header[4];
                }
                else
                {
                    Debug.LogWarning($"无法识别的Header格式: {header} 在行: {line}");
                    continue; // 无法识别的Header格式
                }


                int numSlices=dataString.Length/2;
                if(numSlices==0) continue;

                long ticksPerSlice=ticksInCurrentMeasure/numSlices;

                for(int i=0; i<numSlices; i++)
                {
                    string zz=dataString.Substring(i*2,2);
                    if(zz=="00") continue;

                    long noteAbsoluteTick=currentTick+(i*ticksPerSlice);
                    float noteTime=ConvertTickToSeconds(noteAbsoluteTick,ticksPerBeat,bpmTimeline)+waveOffset;

                    int susLaneMarkerVal=HexCharToInt(laneMarker); // 2-'d'
                    if(susLaneMarkerVal<2) continue;
                    int susLeftLane=susLaneMarkerVal-2; // 0-11

                    char typeChar=zz[0];
                    char widthChar=zz[1];
                    int susWidth=HexCharToInt(widthChar);
                    if(susWidth<1) continue;

                    int gameStartLane=susLeftLane/2; // 0-5
                    int gameWidth=Mathf.Max(1,susWidth/2);
                    if(gameStartLane+gameWidth>6) gameWidth=6-gameStartLane; // 确保不超出轨道

                    NoteData note=new NoteData();
                    note.time=noteTime;
                    note.lane=gameStartLane;
                    note.width=gameWidth;

                    if(channelType=='1') // Tap
                    {
                        if(typeChar=='1') note.type=NoteType.Tap;
                        else if(typeChar=='2') note.type=NoteType.GoldenTap;
                        else continue;
                        note.id=nextNoteId++;
                        chart.notes.Add(note);
                    }
                    else if(channelType=='3') // Hold
                    {
                        if(typeChar=='1') // Hold Start
                        {
                            note.type=NoteType.HoldStart;
                            note.id=nextNoteId++;

                            if(openHolds.ContainsKey(gameStartLane))
                            {
                                Debug.LogWarning($"小节 {currentMeasure}, 轨道 {gameStartLane}: 新HoldStart覆盖未结束的Hold。");
                            }
                            openHolds[gameStartLane]=new PendingHoldNote{
                                id=note.id,
                                measure=currentMeasure,
                                sliceIndex=i,
                                numSlices=numSlices,
                                type=note.type,
                                lane=gameStartLane,
                                width=gameWidth,
                                approximateTime=noteTime // 实际时间
                            };
                            chart.notes.Add(note);
                        }
                        else if(typeChar=='2') // Hold End
                        {
                            note.type=NoteType.HoldEnd;
                            if(openHolds.TryGetValue(gameStartLane,out PendingHoldNote matchingStart))
                            {
                                note.id=matchingStart.id;
                                chart.notes.Add(note);
                                openHolds.Remove(gameStartLane);
                            }
                            else
                            {
                                Debug.LogWarning($"小节 {currentMeasure}, 轨道 {gameStartLane}: 孤立HoldEnd。");
                            }
                        }
                    }
                }
            }
            currentTick+=ticksInCurrentMeasure;
        }

        // 处理GoldenHoldStart
        List<NoteData> notesToRemove=new List<NoteData>();
        foreach(NoteData note in chart.notes)
        {
            if(note.type==NoteType.GoldenTap)
            {
                foreach(NoteData otherNote in chart.notes)
                {
                    if(otherNote.type==NoteType.HoldStart&&otherNote.time==note.time&&otherNote.lane==note.lane&&otherNote.width==note.width)
                    {
                        otherNote.type=NoteType.GoldenHoldStart;
                        // 将HoldEnd转换为GoldenHoldEnd
                        foreach(NoteData endNote in chart.notes)
                        {
                            if(endNote.type==NoteType.HoldEnd&&endNote.id==otherNote.id)
                            {
                                endNote.type=NoteType.GoldenHoldEnd;
                                break;
                            }
                        }
                        notesToRemove.Add(note); // 标记GoldenTap为删除
                        break;
                    }
                }
            }
        }
        foreach(NoteData note in notesToRemove) chart.notes.Remove(note);

        // 检查是否有未闭合的Hold (在所有小节处理完毕后)
        if(openHolds.Count>0)
        {
            Debug.LogWarning($"谱面解析结束时，仍有 {openHolds.Count} 个未闭合的长按键。");
            foreach(var pair in openHolds)
            {
                PendingHoldNote openHold=pair.Value;
                // 估算一个结束时间，例如谱面最后音符时间 + 一个小节
                float estimatedEndTime = chart.notes.Count > 0 ? chart.notes[chart.notes.Count -1].time + (float)(4.0 * 60.0 / currentBpm) : ConvertTickToSeconds(currentTick, ticksPerBeat, bpmTimeline) + waveOffset;

                NoteData forcedEndNote = new NoteData {
                    type = (openHold.type == NoteType.HoldStart || openHold.type == NoteType.GoldenHoldStart) ? (openHold.type == NoteType.HoldStart ? NoteType.HoldEnd : NoteType.GoldenHoldEnd) : NoteType.HoldEnd, // 确保类型匹配
                    time = estimatedEndTime,
                    lane = openHold.lane,
                    width = openHold.width,
                    id = openHold.id
                };
                chart.notes.Add(forcedEndNote);
                Debug.Log($"为轨道 {openHold.lane} 上未闭合的Hold (ID: {openHold.id}) 强制添加结束点于 {estimatedEndTime}s");
            }
            openHolds.Clear();
        }

        return chart;
    }

    // 精确计算Tick到秒的转换，考虑BPM变化
    private float ConvertTickToSeconds(long targetTick, int ticksPerBeat, List<KeyValuePair<long,float>> sortedBpmTimeline)
    {
        if(ticksPerBeat<=0) return 0f;
        if(sortedBpmTimeline==null||sortedBpmTimeline.Count==0) return 0f; // 应该至少有一个初始BPM

        double accumulatedSeconds=0.0;
        long lastTick=0;
        float currentBpm=sortedBpmTimeline[0].Value; // 使用时间线上的第一个BPM作为起始

        for(int i=0; i<sortedBpmTimeline.Count; i++)
        {
            long bpmChangeTick=sortedBpmTimeline[i].Key;
            float bpmValue=sortedBpmTimeline[i].Value;

            if(targetTick<=lastTick) break; // 已经超过目标tick，无需继续

            long ticksInThisSegment;
            if(i+1<sortedBpmTimeline.Count) // 不是最后一个BPM
            {
                if(targetTick<=sortedBpmTimeline[i+1].Key) // 目标tick在当前和下一个BPM之间
                {
                    ticksInThisSegment=targetTick-lastTick;
                    currentBpm=bpmValue;
                    accumulatedSeconds+=(double)ticksInThisSegment/ticksPerBeat*(60.0/currentBpm);
                    lastTick=targetTick; // 到达目标
                    break;
                }
                else // 目标tick在下一个BPM段内，计算当前BPM段的full duration
                {
                    ticksInThisSegment=sortedBpmTimeline[i+1].Key-lastTick;
                    currentBpm=bpmValue;
                    accumulatedSeconds+=(double)ticksInThisSegment/ticksPerBeat*(60.0/currentBpm);
                    lastTick=sortedBpmTimeline[i+1].Key;
                    continue;
                }
            }
            else // 是最后一个BPM
            {
                ticksInThisSegment=targetTick-lastTick;
                currentBpm=bpmValue;
                accumulatedSeconds+=(double)ticksInThisSegment/ticksPerBeat*(60.0/currentBpm);
                lastTick=targetTick; // 到达目标
                break;
            }
        }

        return (float)accumulatedSeconds;
    }

    // 辅助函数：将十六进制字符 ('0'-'9', 'a'-'z', 不区分大小写) 转换为整数 (0-35)
    private int HexCharToInt(char c)
    {
        c=char.ToLowerInvariant(c);
        if(c>='0'&&c<='9')
        {
            return c-'0';
        }
        if(c>='a'&&c<='z')
        {
            return c-'a'+10;
        }
        return -1; // 无效字符
    }
}
