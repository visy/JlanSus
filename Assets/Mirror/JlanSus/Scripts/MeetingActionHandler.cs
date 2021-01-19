using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MeetingActionHandler : MonoBehaviour
{
    public Button meetingButton;

	void Start () {
		var btn = meetingButton.GetComponent<Button>();
		btn.onClick.AddListener(MeetingComplete);
	}

    // Update is called once per frame
    void Update()
    {
        
    }

    public void MeetingComplete()
    {
        Mirror.JlanSus.JlanPlayer.MeetingComplete?.Invoke(true);
        
    }
}
