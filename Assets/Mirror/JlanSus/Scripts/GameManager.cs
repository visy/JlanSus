using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Mirror.JlanSus
{
    public enum GameState 
    {
        Lobby,
        Intro,
        Freeroam,
        Meeting,
        Cutscene,
        End,
    };

    public class GameManager : NetworkBehaviour 
    {
        [SyncVar] 
        public float gameTime; // The length of a game, in seconds.
        
        [SyncVar] 
        public float timer; // How long the game has been running. -1=waiting for players, -2=game is done
        
        [SyncVar] 
        public int minPlayers; // Number of players required for the game to start
        
        [SyncVar] 
        public bool masterTimer = false; // Is this the master timer?

        [SyncVar]
        public GameState _currentState;

        public GameState CurrentState 
        {
            get { return _currentState; }
            set 
            {
                if (_currentState == value) return;
                _currentState = value;
            }
        }

        GameManager serverTimer;

        public GameObject MeetingResult;

        void Start() 
        {
            if (isServer) // For the host to do: use the timer and control the time.
            { 
                if (isLocalPlayer) 
                {
                    serverTimer = this;
                    masterTimer = true;
                }
                CurrentState = GameState.Freeroam;
            } 
            else if (isLocalPlayer) // For all the boring old clients to do: get the host's timer.
            { 
                GameManager[] timers = FindObjectsOfType<GameManager>();
                for(int i =0; i<timers.Length; i++)
                {
                    if(timers[i].masterTimer)
                    {
                        serverTimer = timers[i];
                    }
                }
            }
        }

        public void CheckVotes() 
        {
            var votes = new Dictionary<int,int>();
            for(var i = 0;i < 16;i++) 
            {
                votes[i] = 0;
            }

            var voteCount = 0;
            var playerCount = NetworkServer.connections.Count;

            var players = GameObject.FindGameObjectsWithTag("Player");

            foreach(var player in players) 
            {
                var voteIndex = player.GetComponent<JlanPlayer>().currentVote;
                var nick = player.GetComponent<JlanPlayer>().nick;
                if (voteIndex >= 0) 
                {
                    votes[voteIndex]++;
                    voteCount++;
                }
            }

            Debug.Log("current votes given: " + voteCount + "/" + playerCount);

            if (voteCount >= playerCount) 
            {
                var largest = -1;
                var largestIndex = -1;
                for(var i=0;i<16;i++) {
                    if (votes[i] > largest) 
                    {
                        largest = votes[i];
                        largestIndex = i;
                    }
                }

                var text = "";

                if (largest > 0 && largestIndex > 0 && (float)largest > (float)(playerCount/2.0f)) 
                {
                    foreach(var player in players) 
                    {
                        if (player.GetComponent<JlanPlayer>().netId == largestIndex) 
                        {
                            text = "Evicted " + player.GetComponent<JlanPlayer>().nick + " with " + largest + " votes.";
                            player.GetComponent<JlanPlayer>().RpcKillPlayer(largestIndex);
                            break;
                        }
                    }
                } 
                else 
                {
                    text = "Nobody was evicted.";
                }

                RpcMeetingComplete(text);
            }
        }

        [ClientRpc]
        void RpcMeetingComplete(string text) 
        {
            Mirror.JlanSus.JlanPlayer.MeetingComplete?.Invoke(text);
        }

        [ClientRpc]
        public void RpcStateChange(GameState newState) 
        {
            if (!isServer) return;
            _currentState = newState;
            timer = -1;

            Debug.Log("Changing state to: " + newState);
        }

        void Update()
        {

            if(masterTimer) // Only the MASTER timer controls the time
            { 
                if (timer>=gameTime)
                {
                    timer = -2;
                }
                else if (timer == -1)
                {
                    if (NetworkServer.connections.Count >= minPlayers) 
                    {
                        timer = 0;
                    }
                } 
                else if (timer == -2) // Game done.
                {
                    
                }   
                else
                {
                    timer += Time.deltaTime;
                }
            }

            if(isLocalPlayer) // EVERYBODY updates their own time accordingly.
            { 
                if (serverTimer) 
                {
                    gameTime = serverTimer.gameTime;
                    timer = serverTimer.timer;
                    minPlayers = serverTimer.minPlayers;
                } 
                else // Maybe we don't have it yet?
                { 
                    GameManager[] timers = FindObjectsOfType<GameManager>();
                    for(int i =0; i<timers.Length; i++)
                    {
                        if(timers[i].masterTimer)
                        {
                            serverTimer = timers [i];
                        }
                    }
                }
            }
        }

    }
}