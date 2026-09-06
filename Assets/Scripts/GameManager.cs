using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public TMP_Text promptText;
    public TMP_Text scoreText;
    public TMP_Text feedbackText;

    private int score = 0;
    private InstrumentType currentTarget;
    private readonly List<InstrumentType> availableTypes = new List<InstrumentType>
    {
        InstrumentType.Scalpel,
        InstrumentType.Scissors,
        InstrumentType.NeedleHolder
    };

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        NextPrompt();
    }

    void NextPrompt()
    {
        currentTarget = availableTypes[Random.Range(0, availableTypes.Count)];
        promptText.text = "Pick up the " + currentTarget;
        feedbackText.text = "";
    }

    // Called by InstrumentItem when it's grabbed
    public void OnInstrumentSelected(InstrumentType selected)
    {
        bool correct = selected == currentTarget;

        if (correct)
        {
            score++;
            feedbackText.text = "Correct!";
            feedbackText.color = Color.green;
        }
        else
        {
            score = Mathf.Max(0, score - 1);
            feedbackText.text = "Wrong instrument";
            feedbackText.color = Color.red;
        }

        scoreText.text = "Score: " + score;

        if (InteractionLogger.Instance != null)
            InteractionLogger.Instance.LogSelection(selected, currentTarget, correct);

        Invoke(nameof(NextPrompt), 1.5f);
    }
}
