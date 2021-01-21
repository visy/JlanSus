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
        ImpostorWinEnd,
        LanittajaWinEnd,
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

        public GameObject Toaster;

        void Start() 
        {
            if (isServer) // For the host to do: use the timer and control the time.
            { 
                if (isLocalPlayer) 
                {
                    serverTimer = this;
                    masterTimer = true;
                }
                CurrentState = GameState.Lobby;
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
            if (!isServer) return;

            var votes = new Dictionary<int,int>();
            for(var i = 0;i < 16;i++) 
            {
                votes[i] = 0;
            }

            var voteCount = 0;
            var aliveCount = 0;

            var players = GameObject.FindGameObjectsWithTag("Player");

            foreach(var p in players) 
            {
                var player = p.GetComponent<JlanPlayer>(); 
                var voteIndex = player.currentVote;
                var nick = player.nick;

                if (player.isAlive)
                {
                    aliveCount++;
                }

                if (voteIndex >= 0) 
                {
                    votes[voteIndex]++;
                    voteCount++;
                }
            }

            Debug.Log("current votes given: " + voteCount + "/" + aliveCount);

            if (voteCount >= aliveCount) 
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

                if (largest > 0 && largestIndex > 0 && (float)largest > (float)(aliveCount/2.0f)) 
                {
                    foreach(var player in players) 
                    {
                        if (player.GetComponent<JlanPlayer>().netId == largestIndex) 
                        {
                            text = "Evicted " + player.GetComponent<JlanPlayer>().nick + " with " + largest + " votes.";
                            player.GetComponent<JlanPlayer>().RpcKillPlayer(largestIndex, false);
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
            if (_currentState == GameState.ImpostorWinEnd || _currentState == GameState.LanittajaWinEnd) return;
            _currentState = newState;
            timer = -1;

            Debug.Log("Changing state to: " + newState);
        }

        void AssignImpostors() 
        {
            var players = GameObject.FindGameObjectsWithTag("Player");

            var numPlayers = players.Length;
            var numImpostors = numPlayers <= 6 ? 1 : 2;

            var impostors = new List<int>();

            var prevRandom = -1;
            for (var i = 0; i < numImpostors; i++) 
            {
                var randomIndex = prevRandom;
                while (randomIndex == prevRandom) 
                {
                    randomIndex = UnityEngine.Random.Range(0,numPlayers-1);
                }
                Debug.Log("impo:" + randomIndex);
                Debug.Log("le:" + numPlayers);
                prevRandom = randomIndex;
                players[randomIndex].GetComponent<JlanPlayer>().CmdSetRole(false);
                impostors.Add(randomIndex);
            }

            for (var i = 0; i < numPlayers; i++) 
            {
                if (!impostors.Contains(i)) 
                {
                    players[i].GetComponent<JlanPlayer>().CmdSetRole(true);
                }
            }
        }

        void Update()
        {

            if(masterTimer) // Only the MASTER timer controls the time
            { 

                if (NetworkServer.connections.Count >= minPlayers && _currentState == GameState.Lobby && timer == -1) 
                {
                    timer = 0;
                    // game start on enough players joined
                    AssignImpostors();

                    RpcStateChange(GameState.Freeroam);
                }

                if (timer>=gameTime)
                {
                    timer = -2;
                }
                else if (timer == -2) // Game done.
                {
                    
                }   
                else
                {
                    if (_currentState == GameState.Freeroam)
                    {
                        timer += Time.deltaTime;
                    }
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