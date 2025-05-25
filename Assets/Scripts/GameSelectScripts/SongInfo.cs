using UnityEngine;

[System.Serializable]
public class SongInfo
{
    public string songName="歌曲名称";
    public string artist="艺术家";
    public string chartFileName="ChartFile.json";
    public string audioFileName="AudioFile.mp3";
    public string imageFileName="ImageFile.png";
    public Sprite coverArt;
    public float bpm=120;
}
