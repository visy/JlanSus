using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using SuperTiled2Unity;

namespace Mirror.JlanSus
{
    [RequireComponent(typeof(BoxCollider2D), typeof(Rigidbody2D))]
    public class JlanPlayer : NetworkBehaviour
    {
        [SyncVar(hook = "OnNick")]
        public string nick;

        [SyncVar]
        public bool isLanittaja;

        [SyncVar]
        public int currentVote = -1;

        [SyncVar]
        public bool isAlive = true;

        public bool usingMap = false;

        public string hatName;

        public int standingOnTaskNum = -1;
        public bool doingTask = false;

        public bool standingOnMeetingCall = false;

        public float speed = 30;

        public float minKillDistance;

        private Vector2 minimapFactor = new Vector2(0.08f,0.08f);
        private Vector2 minimapOffset = new Vector2(0.0f, 0.0f);

        private bool meetingLoaded = false;
        private bool meetingCalled = false;

        private bool killingStarted = false;

        private float killTimer = 0.0f;

        private Vector3 originalSpawnPos;

        private Color[] colorIndex = {
            Color.black,
            Color.blue,
            Color.cyan,
            Color.gray,
            Color.green,
            Color.grey,
            Color.magenta,
            Color.red,
            Color.white,
            Color.yellow
        };

        private string[] TaskNames = {
            "sähköt",
            "kytkin",
            "kiuas",
            "wlan-tukari",
            "megalihis"
        };

        public static Action<bool> TaskAbort;
        public static Action<bool> TaskComplete;

        public static Action<string> MeetingComplete;

        public static Action<int> CastVote;
        public static Action<int> KillPlayer;

        private List<int> assignedTasks = new List<int>() {
            0,1,4
        };

        private List<int> completedTasks = new List<int>();

        private GameManager gameManager;

        public GameObject bodyPrefab;

        //minimap
        private GameObject minimap = null;
        private GameObject playerblip = null;

        public override void OnStartServer() 
        {
            base.OnStartServer();
        }

        public override void OnStartClient() 
        {
            isLanittaja = true;
            gameManager = GameObject.Find("GameManager").GetComponent<GameManager>();

            Physics2D.IgnoreLayerCollision(9, 9);
            gameObject.layer = 9;

            if (isLocalPlayer) 
            {
                var cam = GameObject.Find("Main Camera");
                originalSpawnPos = gameObject.transform.position;
                cam.transform.parent = gameObject.transform;

                var pos = gameObject.transform.position;
                pos.z = -10;
                cam.transform.position = pos;

                minimap = GameObject.Find("minimap");
                playerblip = GameObject.Find("PlayerBlip");

                minimap.SetActive(false);
                playerblip.SetActive(false);

                nick = PlayerPrefs.GetString("nick", "Defaultti");
                SetNick();
            } 

            CmdSetNick(nick);

            GetComponent<SpriteRenderer>().color = colorIndex[(netId % 10)];

            TaskAbort += OnTaskAbort;
            TaskComplete += OnTaskComplete;

            MeetingComplete += OnMeetingComplete;
            CastVote += OnCastVote;

            UpdateTasks();
        }

        public void OnTriggerEnter2D( Collider2D col )
        {
            Debug.Log("onTriggerEnter:" + col.gameObject.GetComponent<SuperObject>().m_Type);
            var type = col.gameObject.GetComponent<SuperObject>().m_Type;

            if (type.StartsWith("MeetingCall")) 
            {
                standingOnMeetingCall = true;

                if (isLocalPlayer) 
                {
                    if (standingOnMeetingCall && isAlive && gameManager.CurrentState == GameState.Freeroam) 
                    {
                        var text = GetChildWithName(gameObject, "TextPress1");
                        text.GetComponent<TextMeshPro>().SetText("Press <color=\"red\">1</color> to call meeting");
                        text.SetActive(true);
                    }
                }
            }

            if (type.StartsWith("Task")) {
                var num = Int32.Parse(type.Substring("Task".Length));
                standingOnTaskNum = num;

                if (isLocalPlayer) 
                {
                    if (isLanittaja && assignedTasks.Contains(standingOnTaskNum) && !completedTasks.Contains(standingOnTaskNum) && isAlive && gameManager.CurrentState == GameState.Freeroam) 
                    {
                        var text = GetChildWithName(gameObject, "TextPress1");
                        text.GetComponent<TextMeshPro>().SetText("Press <color=\"red\">1</color> to do task");
                        text.SetActive(true);
                    }
                }

            }
        }

        public void OnTriggerStay2D( Collider2D col )
        {
        }

        public void OnTriggerExit2D( Collider2D col )
        {
            var type = col.gameObject.GetComponent<SuperObject>().m_Type;

            if (type.Contains("MeetingCall")) 
            {
                standingOnMeetingCall = false;

                if (isLocalPlayer) 
                {
                    var text = GetChildWithName(gameObject, "TextPress1");
                    text.SetActive(false);
                } 
            }

            if (type.StartsWith("Task")) {
                var prevTask = standingOnTaskNum;
                standingOnTaskNum = -1;

                if (isLocalPlayer) 
                {
                    if (isLanittaja && assignedTasks.Contains(prevTask) && !completedTasks.Contains(prevTask)) 
                    {
                        var text = GetChildWithName(gameObject, "TextPress1");
                        text.SetActive(false);
                    }
                }
            }
        }

        GameObject GetChildWithName(GameObject obj, string name) 
        {
            Transform trans = obj.transform;
            Transform childTrans = trans.Find(name);
            if (childTrans != null) {
                return childTrans.gameObject;
            } else {
                return null;
            }
        }

        public void OnNick(string o, string n)
        {
            SetNick();
        }

        [Command(ignoreAuthority = true)]
        void CmdSetNick(string n) 
        {
            nick = n;
        }

        void SetNick() 
        {
            var sign = GetChildWithName(gameObject, "NameSign");
            sign.GetComponent<TextMeshPro>().SetText(nick);
        }


        [Command(ignoreAuthority = true)]
        public void CmdSetRole(bool _isLanittaja) 
        {
            RpcSetRole(_isLanittaja);
        }

        [ClientRpc]
        void RpcSetRole(bool _isLanittaja) 
        {
            isLanittaja = _isLanittaja;

            if (isLocalPlayer) 
            {
                var resultGo = gameManager.Toaster;
                resultGo.SetActive(true);

                var text2 = GetChildWithName(resultGo, "Text");
                text2.GetComponent<TextMeshProUGUI>().SetText(isLanittaja ? "You are a gamer." : "You are the impostor!");
            }
        }

        [Command]
        void CmdDropBody(int playerId)
        {
            RpcDropBody(playerId);
        }

        [ClientRpc]
        void RpcDropBody(int playerId)
        {
            Vector3 pos = gameObject.transform.position;
            Quaternion rot = gameObject.transform.rotation;

            rot *= Quaternion.Euler(0.0f,0.0f,90.0f);

            GameObject body = Instantiate(bodyPrefab, pos, rot);

            body.GetComponent<SpriteRenderer>().color = colorIndex[(playerId % 10)];

            NetworkServer.Spawn(body);
        }

        void OnCastVote(int vote) 
        {
            CmdCastVote(vote);
        }

        [Command]
        void CmdCastVote(int vote) 
        {
            RpcCastVote(vote);
        }

        [Command(ignoreAuthority = true)]
        void CmdChangeState(GameState newState) 
        {
            if (newState != gameManager.CurrentState)
            {
                gameManager.RpcStateChange(newState);
            }
        }

        [ClientRpc]
        public void RpcKillPlayer(int playerId, bool leaveBody)
        {
            isAlive = false;

            // move inside walls while dead
            GetComponent<CharacterController2D>().platformMask = 0;

            // either invisible or partially visible for local player & other ghosts
            if (isLocalPlayer) 
            {
                var c = GetComponent<SpriteRenderer>().color;
                c.a = 0.3f;
                GetComponent<SpriteRenderer>().color = c;
            } 
            else 
            {
                var c = GetComponent<SpriteRenderer>().color;
                c.a = 0.0f;
                GetComponent<SpriteRenderer>().color = c;
            }

            if (isServer) 
            {
                var players = GameObject.FindGameObjectsWithTag("Player");
                var lanittajaAliveCount = 0;
                var impostorAliveCount = 0;
                foreach (var p in players) 
                {
                    var player = p.GetComponent<JlanPlayer>();
                    if (player.isLanittaja && player.isAlive) 
                    {
                        lanittajaAliveCount++;
                    }
                    if (!player.isLanittaja && player.isAlive) 
                    {
                        impostorAliveCount++;
                    }
                }

                Debug.Log("impostors alive: " + impostorAliveCount + " / lanittajat alive: " + lanittajaAliveCount);

                // lanittajat wins
                if (impostorAliveCount == 0 && lanittajaAliveCount > 0) 
                {
                    CmdChangeState(GameState.LanittajaWinEnd);
                    return;
                }

                // impostor win
                if (impostorAliveCount >= lanittajaAliveCount && impostorAliveCount > 0)
                {
                    CmdChangeState(GameState.ImpostorWinEnd);
                    return;
                }
            }

            if (leaveBody)
            {
                CmdDropBody(playerId);
            } 
            else 
            {
                CmdChangeState(GameState.Freeroam);
            }
        }

        [ClientRpc]
        void RpcCastVote(int vote)
        {
            if (isAlive)
            {
                currentVote = vote;
                gameManager.CheckVotes();
            }
        }

        void UpdateTasks()
        {
            var tasklist = GetChildWithName(GameObject.Find("TaskList"), "Text").GetComponent<TextMeshProUGUI>();

            var text = "<b>TODO</b>\n";

            foreach (var task in assignedTasks)
            {
                text += "- <i>";
                text += TaskNames[task];
                if (completedTasks.Contains(task))
                {
                    text += "</i> [<color=\"green\">✓</color>]";
                } 
                else
                {
                    text += "</i> [<color=\"red\">✗</color>]";
                }

                text += "\n";
            }

            tasklist.SetText(text);
        }

        void OnTaskAbort(bool result) 
        {
            CloseTask();
        }

        void OnTaskComplete(bool result) 
        {
            // update hud / sim whatever
            completedTasks.Add(standingOnTaskNum);

            var text = GetChildWithName(gameObject, "TextPress1");
            text.SetActive(false);

            CloseTask();
            UpdateTasks();
        }

        public static UnityEngine.Object Find(string name, System.Type type)
        {
            UnityEngine.Object [] objs = Resources.FindObjectsOfTypeAll(type);
    
            foreach (UnityEngine.Object obj in objs)
            {
                if (obj.name == name)
                {
                    return obj;
                }
            }
    
            return null;
        }

        void OnMeetingComplete(string result) 
        {
            currentVote = -1;

            if (isLocalPlayer) 
            {
                var text = GetChildWithName(gameObject, "TextPress1");
                text.SetActive(false);

                var resultGo = gameManager.Toaster;
                resultGo.SetActive(true);

                var text2 = GetChildWithName(resultGo, "Text");
                text2.GetComponent<TextMeshProUGUI>().SetText(result);
                
                gameObject.transform.position = originalSpawnPos;
                GetComponent<CharacterController2D>().move(Vector3.zero);
            }

            // go to freeroam immediately, otherwise let the killplayer logic handle possible end condition
            if (result.Contains("Nobody was evicted.")) 
            {
                CmdChangeState(GameState.Freeroam);
            }

            UpdateTasks();
        }

        // additively load other scenes for the task minigames
        void LoadTask() 
        {
            var num = standingOnTaskNum;
            var name = "TaskScene"+num;
            SceneManager.LoadScene(name, LoadSceneMode.Additive);
            Debug.Log(name + " was loaded.");
        }

        void CloseTask() {
            var num = standingOnTaskNum;
            StartCoroutine(UnloadScene("TaskScene"+num));
        }

        // Additively load the meeting scene for voting
        void LoadMeeting() 
        {
            SceneManager.LoadScene("MeetingScene", LoadSceneMode.Additive);
            Debug.Log("MeetingScene was loaded.");
        }

        void CloseMeeting() {
            StartCoroutine(UnloadScene("MeetingScene"));
            meetingLoaded = false;
            meetingCalled = false;
        }

        IEnumerator UnloadScene(string oldSceneName)
        {
            AsyncOperation asyncUnload = SceneManager.UnloadSceneAsync(oldSceneName);

            while (!asyncUnload.isDone)
            {
                yield return null;
            }

            if (asyncUnload.isDone)
            {
                Debug.Log(oldSceneName + " was unloaded.");

                doingTask = false;
            }
        }

        void ShowMap() 
        {
            usingMap = true;

            minimap.SetActive(true);
            playerblip.SetActive(true);
        }

        void HideMap() 
        {
            usingMap = false;

            minimap.SetActive(false);
            playerblip.SetActive(false);
        }

        void UpdateMinimap() 
        {
            if (playerblip != null) 
            {
                var pos = gameObject.transform.position;

//                pos.x-=(originalSpawnPos.x+minimapOffset.x);
//                pos.y-=(originalSpawnPos.y+minimapOffset.y);
                pos = Vector3.Scale(pos, new Vector3(1.0f/minimapFactor.x,1.0f/minimapFactor.y,1.0f));
                pos.z = 0.0f;

                playerblip.transform.localPosition = pos;
            }
        }

        // need to use FixedUpdate for rigidbody
        void FixedUpdate()
        {
            // only let the local player control the player.
            if (isLocalPlayer) 
            {
                var state = gameManager.CurrentState;
       
                // INPUT & PHYSICS in freeroam mode
                if (!doingTask && (state == GameState.Freeroam || state == GameState.Lobby))  
                {
                    float h = Input.GetAxisRaw("Horizontal");
                    float v = Input.GetAxisRaw("Vertical");

                    Vector3 m;
                    m.x = h;
                    m.y = v;
                    m.z = 0;
                    m.Normalize();

                    if (h != 0 || v != 0) transform.right = m;

                    m *= speed * Time.fixedDeltaTime;

                    GetComponent<CharacterController2D>().move(m);
    //                rigidbody2d.velocity = move;
                    GetComponent<CharacterController2D>().rigidbody2d.freezeRotation = true;
                    gameObject.transform.rotation = Quaternion.identity;

                    UpdateMinimap();

                    // START TASK

                    // check if pressing one, were are standing on valid task that has not yet been completed
                    if (Input.GetKey("1") && standingOnTaskNum >= 0 && assignedTasks.Contains(standingOnTaskNum) && !completedTasks.Contains(standingOnTaskNum) && state == GameState.Freeroam && isLanittaja)
                    {
                        doingTask = true;
                        LoadTask();
                    }

                    // MAP

                    if (Input.GetKey("2") && (state == GameState.Freeroam || state == GameState.Lobby)) 
                    {
                        ShowMap();
                    } 
                    else
                    {
                        HideMap();    
                    }

                    if (!isLanittaja && state == GameState.Freeroam && !standingOnMeetingCall) 
                    {
                        // KILL AS IMPOSTOR
                        CheckPlayersCloseAndKill();
                        if (killTimer > 0.0f) 
                        {
                            killTimer-=Time.fixedDeltaTime;
                        }
                    }

                    if (state == GameState.Freeroam) 
                    {
                        // CALL MEETING
                        if (Input.GetKey("1") && standingOnMeetingCall && !meetingCalled && isAlive)
                        {
                            // call meeting
                            meetingCalled = true;
                            CmdChangeState(GameState.Meeting);
                            meetingLoaded = false;
                        }
                    }
                } 

                if (state == GameState.Meeting && !meetingLoaded) 
                {
                    meetingLoaded = true;
                    LoadMeeting();
                }

                if (state == GameState.Freeroam && meetingLoaded) 
                {
                    meetingLoaded = false;
                    CloseMeeting();
                }

                // debug
                var sign = GetChildWithName(gameObject, "NameSign");
//                sign.GetComponent<TextMeshPro>().SetText(nick + (doingTask ? " / working:" + standingOnTaskNum : (standingOnTaskNum >= 0) ? " / onTask:" + standingOnTaskNum : standingOnMeetingCall ? " / call meeting" : ""));

                sign.GetComponent<TextMeshPro>().SetText(nick + (!isAlive ? " (dead)" : ""));
            }
            else 
            {
                var sign = GetChildWithName(gameObject, "NameSign");
                if (isAlive) 
                {
                    sign.GetComponent<TextMeshPro>().SetText(nick);
                } 
                else 
                {
                    if (sign.activeSelf)
                    {
                        sign.SetActive(false);
                    }
                }
            }
        }

        public void CheckPlayersCloseAndKill() 
        {
            var players = GameObject.FindGameObjectsWithTag("Player");

            var oneClose = false;

            var pressingKey = Input.GetKey("1");

            foreach (var player in players) 
            {
                JlanPlayer other = player.GetComponent<JlanPlayer>();
                if (other != null) 
                {
                    float dist = Vector3.Distance(other.gameObject.transform.position, gameObject.transform.position);
                    if (dist <= minKillDistance && !oneClose && other.netId != netId && other.isLanittaja && other.isAlive) 
                    {
                        oneClose = true;
                        var text = GetChildWithName(gameObject, "TextPress1");
                        text.GetComponent<TextMeshPro>().SetText("Press <color=\"red\">1</color> to eat");
                        text.SetActive(true);
                    }

                    if (oneClose && pressingKey && killTimer <= 0.0f)
                    {
                        killTimer = 60.0f;
                        other.RpcKillPlayer((int)other.netId, true);
                        break;
                    }
                }
            }

            if (!oneClose)
            {
                var text = GetChildWithName(gameObject, "TextPress1");
                text.GetComponent<TextMeshPro>().SetText("");
            }
        }

    }   
}
