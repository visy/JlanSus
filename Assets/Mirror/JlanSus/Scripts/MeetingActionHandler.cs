using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mirror.JlanSus;

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
            var index = Int32.Parse(gameObject.name.Substring("CastVote".Length))-1;
            var players = (GameObject[])GameObject.FindGameObjectsWithTag("Player");

            if (index < players.Length) 
            {
                var player = players[index].GetComponent<JlanPlayer>();
                voteNum = (int)player.netId;
        		btn.onClick.AddListener(CastVote);
                btn.transform.GetChild(0).GetComponent<TextMeshProUGUI>().SetText("Vote " + player.nick);
            } 
            else 
            {
                btn.gameObject.SetActive(false);
            }
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
