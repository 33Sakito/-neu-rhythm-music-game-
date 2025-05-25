using UnityEngine;
using TMPro;
using System.Collections;
using System;

public class UIManager:MonoBehaviour
{
    public static UIManager Instance {get;private set;}
    public TextMeshProUGUI judgementText;
    public float judgementTextDisplayTime=0.4f; //判定文字显示的时间
    public float scaleUpDuration=0.1f; //缩放动画持续时间
    public float startScaleFactor=0.2f; //初始缩放因子
    private Coroutine judgementCoroutine=null;
    private Vector3 initialJudgementScale;

    public TextMeshProUGUI scoreText;
    public GameObject comboGroup; //包含Combo文本和数字的父对象
    public TextMeshProUGUI comboNumberText;
    private Coroutine comboCoroutine=null; //用于控制Combo显示动画的协程
    private Vector3 initialComboNumberScale;

    void Awake()
    {
        Instance=this;
        if(judgementText!=null)
        {
            initialJudgementScale=judgementText.transform.localScale;
            judgementText.enabled=false; //初始时隐藏文字
        }
        if(comboGroup!=null)
        {
            comboGroup.SetActive(false); //初始时隐藏Combo
        }
        if(comboNumberText!=null)
        {
            initialComboNumberScale=comboNumberText.transform.localScale;
        }
    }

    public void ShowJudgement(JudgementType type)
    {
        if(judgementText==null) return;
        if(judgementCoroutine!=null)
        {
            StopCoroutine(judgementCoroutine);
            judgementText.enabled=false;
        }
        switch(type)
        {
            case JudgementType.Perfect:
                judgementText.text="Perfect";
                judgementText.enableVertexGradient=true; //启用渐变
                VertexGradient gradient=new VertexGradient();
                gradient.topLeft=new Color(0.8f,0.6f,1f);
                gradient.topRight=new Color(0.6f,1f,1f);
                gradient.bottomLeft=new Color(1f,0.8f,0.9f);
                gradient.bottomRight=new Color(1f,1f,0.8f);
                judgementText.colorGradient=gradient;
                judgementText.color=Color.white;
                break;
            case JudgementType.Great:
                judgementText.text="Great";
                judgementText.color=new Color(0.5f,0f,0.5f);
                break;
            case JudgementType.Good:
                judgementText.text="Good";
                judgementText.color=Color.blue;
                break;
            case JudgementType.Bad:
                judgementText.text="Bad";
                judgementText.color=Color.red;
                break;
            case JudgementType.Miss:
                judgementText.text="Miss";
                judgementText.color=Color.grey;
                break;
        }
        //开始协程
        judgementCoroutine=StartCoroutine(ScaleUpAndDisappear());
    }

    //协程，用于逐渐淡出文字
    IEnumerator ScaleUpAndDisappear()
    {
        judgementText.enabled=true;
        judgementText.alpha=1f; //确保不透明
        Transform textTransform=judgementText.transform;
        Vector3 startScale=initialJudgementScale*startScaleFactor; //初始缩放
        float timer=0f;
        //缩放
        while(timer<scaleUpDuration)
        {
            timer+=Time.deltaTime;
            float progress=Mathf.Clamp01(timer/scaleUpDuration);
            float smoothProgress=Mathf.SmoothStep(0f,1f,progress);
            textTransform.localScale=Vector3.LerpUnclamped(startScale,initialJudgementScale,smoothProgress);
            yield return null;
        }
        textTransform.localScale=initialJudgementScale;//确保最终大小
        float waitTime=judgementTextDisplayTime-scaleUpDuration;
        if(waitTime>0f)
        {
            yield return new WaitForSeconds(waitTime);
        }
        judgementText.enabled=false;
        judgementCoroutine=null;
    }

    public void UpdateScore(int score)
    {
        if(scoreText!=null)
        {
            
            scoreText.text=$"SCORE\n{score}";
        }
    }

    public void UpdateCombo(int combo,bool triggerAnimation)
    {
        if(comboGroup==null||comboNumberText==null) return;
        if(combo>0)
        {
            comboGroup.SetActive(true); //确保Combo组可见
            comboNumberText.text=combo.ToString(); //更新数字
            if(triggerAnimation)
            {
                if(comboCoroutine!=null)
                {
                    StopCoroutine(comboCoroutine);
                    comboNumberText.transform.localScale=initialComboNumberScale; //如果有正在进行的动画，重置缩放
                }
                comboCoroutine=StartCoroutine(AnimateComboNumber());
            }
            else
            {
                comboNumberText.transform.localScale=initialComboNumberScale; //如果没有动画，直接设置为初始大小
            }
        }
        else
        {
            comboGroup.SetActive(false);
            if(comboCoroutine!=null)
            {
                StopCoroutine(comboCoroutine);
                comboNumberText.transform.localScale=initialComboNumberScale;
                comboCoroutine=null;
            }
        }
    }

    IEnumerator AnimateComboNumber()
    {
        if(comboNumberText==null) yield break; //安全检查
        Transform numberTransform=comboNumberText.transform;
        Vector3 startScale=initialComboNumberScale*startScaleFactor; //初始缩放
        float timer=0f;
        //缩放
        while(timer<scaleUpDuration)
        {
            timer+=Time.deltaTime;
            float progress=Mathf.Clamp01(timer/scaleUpDuration);
            float smoothProgress=Mathf.SmoothStep(0f,1f,progress);
            numberTransform.localScale=Vector3.LerpUnclamped(startScale,initialComboNumberScale,smoothProgress);
            yield return null;
        }
        numberTransform.localScale=initialComboNumberScale;
        comboCoroutine=null; //重置协程引用，确保下一次可以正确启动
    }
}