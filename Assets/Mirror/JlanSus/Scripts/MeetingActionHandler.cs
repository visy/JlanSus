using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.UI;

public class MeetingActionHandler : MonoBehaviour
{
    public Button button;
    public GameObject show;
    private int voteNum = -1;

	void Start () {
		var btn = button.GetComponent<Button>();

        if (gameObject.name.Contains("VoteButton")) 
        {
    		btn.onClick.AddListener(ShowVoteOptions);
        }

        if (gameObject.name.Contains("CastVote")) 
        {
            voteNum = Int32.Parse(gameObject.name.Substring("CastVote".Length));
    		btn.onClick.AddListener(CastVote);
        }

        if (gameObject.name.Contains("SkipButton"))
        {
            voteNum = 0;
    		btn.onClick.AddListener(CastVote);
        }
	}

    // Update is called once per frame
    void Update()
    {
        
    }

    public void CastVote()
    {
        Mirror.JlanSus.JlanPlayer.CastVote?.Invoke(voteNum);
    }

    public void ShowVoteOptions()
    {
        show.SetActive(true);
       
    }
}
