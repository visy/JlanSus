using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace Mirror.JlanSus 
{
    public class GameStateTextUpdater : MonoBehaviour
    {
        public GameManager gameManager;

        // Start is called before the first frame update
        void Start()
        {
            
        }

        // Update is called once per frame
        void Update()
        {
            gameObject.transform.GetChild(0).GetComponent<TextMeshProUGUI>().SetText(Enum.GetName(typeof(GameState), gameManager.CurrentState));
        }
    }
}
