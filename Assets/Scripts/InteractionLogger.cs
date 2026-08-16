using System.Collections.Generic;
using UnityEngine;

public class InteractionLogger : MonoBehaviour
{
    public static InteractionLogger Instance;

    [System.Serializable]
    public class SelectionEvent
    {
        public float timestamp;
        public InstrumentType picked;
        public InstrumentType target;
        public bool correct;
        public float reactionTimeSeconds; // time between prompt shown and pick
    }

    private List<SelectionEvent> log = new List<SelectionEvent>();
    private float promptShownTime;

    void Awake()
    {
        Instance = this;
    }

    public void MarkPromptShown()
    {
        promptShownTime = Time.time;
    }

    public void LogSelection(InstrumentType picked, InstrumentType target, bool correct)
    {
        log.Add(new SelectionEvent
        {
            timestamp = Time.time,
            picked = picked,
            target = target,
            correct = correct,
            reactionTimeSeconds = Time.time - promptShownTime
        });
    }

    // call this later when you're ready to export for your ML pipeline
    public void ExportToConsole()
    {
        foreach (var e in log)
            Debug.Log($"[{e.timestamp:F2}s] picked={e.picked} target={e.target} correct={e.correct} reaction={e.reactionTimeSeconds:F2}s");
    }
}