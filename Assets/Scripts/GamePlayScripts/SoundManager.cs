using UnityEngine;

public class SoundManager:MonoBehaviour
{
    public static SoundManager Instance {get;private set;}
    
    public AudioClip tapSound; 
    public AudioClip goldenSound;
    public AudioClip goodSound;

    private AudioSource audioSource;

    void Awake()
    {
        Instance=this;
        audioSource=gameObject.GetComponent<AudioSource>();
    }

    public void PlayJudgementSound(NoteType nType,JudgementType jType)
    {
        if(jType==JudgementType.Bad||jType==JudgementType.Miss) return; //不播放

        AudioClip clipToPlay=null;
        if(nType==NoteType.GoldenTap||nType==NoteType.GoldenHoldStart||nType==NoteType.GoldenHoldEnd)
        {
            clipToPlay=goldenSound;
        }
        else
        {
            clipToPlay=tapSound;
        } 
        if(jType==JudgementType.Good) clipToPlay=goodSound; //优先播放Good

        if(clipToPlay!=null)
        {
            audioSource.PlayOneShot(clipToPlay);
        }
    }

    public void PlayHoldTickSound(NoteType type)
    {
        AudioClip clipToPlay=null;
        if(type==NoteType.HoldStart) clipToPlay=tapSound;
        else if(type==NoteType.GoldenHoldStart) clipToPlay=goldenSound;
        
        if(clipToPlay!=null)
        {
            audioSource.PlayOneShot(clipToPlay);
        }
    }
}
