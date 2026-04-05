using System.Collections;
using System.Collections.Generic;
using RivenFramework;
using TMPro;
using UnityEngine;

public class WB_VoteKick : MonoBehaviour
{
    public TMP_Text kickPlayerText, voteYesText, voteNoText, timerText;
    private GI_NetworkManager networkManager;
    public GameObject visuals;
    private int yesCount, noCount, timeLeft;
    private bool hasVoted;
    private Coroutine timerCoroutine;

    private void OnEnable()
    {
        networkManager = GameInstance.Get<GI_NetworkManager>();
        networkManager.OnVoteKickReceived       += OnVoteKickStarted;
        networkManager.OnVoteKickResultReceived += OnVoteKickResult;
        visuals.SetActive(false);
    }

    private void OnDestroy()
    {
        if (networkManager != null)
        {
            networkManager.OnVoteKickReceived       -= OnVoteKickStarted;
            networkManager.OnVoteKickResultReceived -= OnVoteKickResult;
        }
        if (timerCoroutine != null) StopCoroutine(timerCoroutine);
    }

    private void Update()
    {
        if (hasVoted) return;
        if (Input.GetKeyDown(KeyCode.F1)) CastVote(true);
        if (Input.GetKeyDown(KeyCode.F2)) CastVote(false);
    }

    private void OnVoteKickStarted(VoteKickPacket packet)
    {
        visuals.SetActive(true);
        
        hasVoted    = false;
        yesCount = 0;
        noCount  = 0;
        timeLeft = packet.TimeSeconds;

        kickPlayerText.text = $"Kick player:\n<b>{packet.TargetName}</b>?";
        RefreshVoteCounts();


        if (timerCoroutine != null) StopCoroutine(timerCoroutine);
        timerCoroutine = StartCoroutine(CountdownTimer());
    }

    private void OnVoteKickResult(VoteKickResultPacket result)
    {
        if (timerCoroutine != null) StopCoroutine(timerCoroutine);
        kickPlayerText.text = result.Passed
            ? $"<b>{result.TargetName}</b> was kicked."
            : $"Vote to kick <b>{result.TargetName}</b> failed.";
        timerText.text = "";
        StartCoroutine(HideAfterDelay(3f));
    }

    private void CastVote(bool yes)
    {
        hasVoted = true;
        if (yes) yesCount++; else noCount++;
        RefreshVoteCounts();
        GameInstance.Get<GI_NetworkManager>().CastVoteKick(yes);
    }

    private void RefreshVoteCounts()
    {
        voteYesText.text = $"(F1) <b>[YES]</b> {yesCount}";
        voteNoText.text  = $"(F2) <b>[NO]</b> {noCount}";
    }

    private IEnumerator CountdownTimer()
    {
        while (timeLeft > 0)
        {
            timerText.text = $"{timeLeft}s";
            yield return new WaitForSecondsRealtime(1f);
            timeLeft--;
        }
        timerText.text = "0s";
    }

    private IEnumerator HideAfterDelay(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        visuals.SetActive(false);
    }
}
