#define DEBUG_CC2D_RAYS
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
        // based on CharacterController2D by prime31 - Attribution-NonCommercial-ShareAlike 3.0 Unported (CC BY-NC-SA 3.0) 
       	#region internal types
        struct CharacterRaycastOrigins
        {
            public Vector3 topLeft;
            public Vector3 bottomRight;
            public Vector3 bottomLeft;
        }

       	public class CharacterCollisionState2D
        {
            public bool right;
            public bool left;
            public bool above;
            public bool below;
            public bool becameGroundedThisFrame;
            public bool wasGroundedLastFrame;
            public bool movingDownSlope;
            public float slopeAngle;

            public bool hasCollision()
            {
                return below || right || left || above;
            }
            public void reset()
            {
                right = left = above = below = becameGroundedThisFrame = movingDownSlope = false;
                slopeAngle = 0f;
            }

            public override string ToString()
            {
                return string.Format( "[CharacterCollisionState2D] r: {0}, l: {1}, a: {2}, b: {3}, movingDownSlope: {4}, angle: {5}, wasGroundedLastFrame: {6}, becameGroundedThisFrame: {7}",
                                    right, left, above, below, movingDownSlope, slopeAngle, wasGroundedLastFrame, becameGroundedThisFrame );
            }
        }
        #endregion

       	#region events, properties and fields

        public event Action<RaycastHit2D> onControllerCollidedEvent;
        public event Action<Collider2D> onTriggerEnterEvent;
        public event Action<Collider2D> onTriggerStayEvent;
        public event Action<Collider2D> onTriggerExitEvent;

        /// <summary>
        /// when true, one way platforms will be ignored when moving vertically for a single frame
        /// </summary>
        public bool ignoreOneWayPlatformsThisFrame;

        [SerializeField]
        [Range(0.001f, 0.3f)]
        float _skinWidth = 0.02f;

        /// <summary>
        /// defines how far in from the edges of the collider rays are cast from. If cast with a 0 extent it will often result in ray hits that are
        /// not desired (for example a foot collider casting horizontally from directly on the surface can result in a hit)
        /// </summary>
        public float skinWidth
        {
            get { return _skinWidth; }
            set
            {
                _skinWidth = value;
                recalculateDistanceBetweenRays();
            }
        }

       	/// <summary>
        /// mask with all layers that the player should interact with
        /// </summary>
        public LayerMask platformMask = 0;

        /// <summary>
        /// mask with all layers that trigger events should fire when intersected
        /// </summary>
        public LayerMask triggerMask = 0;

        /// <summary>
        /// mask with all layers that should act as one-way platforms. Note that one-way platforms should always be EdgeCollider2Ds. This is because it does not support being
        /// updated anytime outside of the inspector for now.
        /// </summary>
        [SerializeField]
        LayerMask oneWayPlatformMask = 0;

        /// <summary>
        /// the max slope angle that the CC2D can climb
        /// </summary>
        /// <value>The slope limit.</value>
        [Range( 0f, 90f )]
        public float slopeLimit = 30f;

       	/// <summary>
        /// the threshold in the change in vertical movement between frames that constitutes jumping
        /// </summary>
        /// <value>The jumping threshold.</value>
        public float jumpingThreshold = 0.07f;


        /// <summary>
        /// curve for multiplying speed based on slope (negative = down slope and positive = up slope)
        /// </summary>
        public AnimationCurve slopeSpeedMultiplier = new AnimationCurve( new Keyframe( -90f, 1.5f ), new Keyframe( 0f, 1f ), new Keyframe( 90f, 0f ) );

        [Range( 2, 20 )]
        public int totalHorizontalRays = 8;
        [Range( 2, 20 )]
        public int totalVerticalRays = 4;

        /// <summary>
        /// this is used to calculate the downward ray that is cast to check for slopes. We use the somewhat arbitrary value 75 degrees
        /// to calculate the length of the ray that checks for slopes.
        /// </summary>
        float _slopeLimitTangent = Mathf.Tan( 75f * Mathf.Deg2Rad );



        [HideInInspector][NonSerialized]
        public CharacterCollisionState2D collisionState = new CharacterCollisionState2D();
        [HideInInspector][NonSerialized]
        public Vector3 velocity;
        public bool isGrounded { get { return collisionState.below; } }

        const float kSkinWidthFloatFudgeFactor = 0.001f;

        #endregion

       	/// <summary>
        /// holder for our raycast origin corners (TR, TL, BR, BL)
        /// </summary>
        CharacterRaycastOrigins _raycastOrigins;

        /// <summary>
        /// stores our raycast hit during movement
        /// </summary>
        RaycastHit2D _raycastHit;

        /// <summary>
        /// stores any raycast hits that occur this frame. we have to store them in case we get a hit moving
        /// horizontally and vertically so that we can send the events after all collision state is set
        /// </summary>
        List<RaycastHit2D> _raycastHitsThisFrame = new List<RaycastHit2D>( 2 );

        // horizontal/vertical movement data
        float _verticalDistanceBetweenRays;
        float _horizontalDistanceBetweenRays;

        // we use this flag to mark the case where we are travelling up a slope and we modified our delta.y to allow the climb to occur.
        // the reason is so that if we reach the end of the slope we can make an adjustment to stay grounded
        bool _isGoingUpSlope = false;

        [SyncVar(hook = "OnNick")]
        public string nick;

        [SyncVar]
        public bool isLanittaja;

        [SyncVar]
        public int currentVote = -1;

        [SyncVar]
        public bool isAlive = true;

        public string hatName;

        public int standingOnTaskNum = -1;
        public bool doingTask = false;

        public bool standingOnMeetingCall = false;

        public float speed = 30;

        public float minKillDistance;

        private bool meetingLoaded = false;
        private bool meetingCalled = false;

        private bool killingStarted = false;

        private float killTimer = 0.0f;


        private Vector3 originalSpawnPos;

        [HideInInspector][NonSerialized]
        public new Transform transform;
        [HideInInspector][NonSerialized]
        public BoxCollider2D boxCollider;

        [HideInInspector][NonSerialized]
        public Rigidbody2D rigidbody2d;

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

        public static Action<bool> TaskAbort;
        public static Action<bool> TaskComplete;

        public static Action<string> MeetingComplete;

        public static Action<int> CastVote;
        public static Action<int> KillPlayer;

        private List<int> assignedTasks = new List<int>() {
            1,2,3
        };

        private List<int> completedTasks = new List<int>();

        private GameManager gameManager;

        public GameObject bodyPrefab;

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

                nick = PlayerPrefs.GetString("nick", "Defaultti");
                SetNick();
            } 

            CmdSetNick(nick);

            // add our one-way platforms to our normal platform mask so that we can land on them from above
            platformMask |= oneWayPlatformMask;

            // cache some components
            transform = GetComponent<Transform>();
            boxCollider = GetComponent<BoxCollider2D>();
            rigidbody2d = GetComponent<Rigidbody2D>();

            // here, we trigger our properties that have setters with bodies
            skinWidth = _skinWidth;

            // we want to set our CC2D to ignore all collision layers except what is in our triggerMask
            for( var i = 0; i < 32; i++ )
            {
                // see if our triggerMask contains this layer and if not ignore it
                if( ( triggerMask.value & 1 << i ) == 0 )
                    Physics2D.IgnoreLayerCollision( gameObject.layer, i );
            }

            GetComponent<SpriteRenderer>().color = colorIndex[(netId % 10)];

            TaskAbort += OnTaskAbort;
            TaskComplete += OnTaskComplete;

            MeetingComplete += OnMeetingComplete;
            CastVote += OnCastVote;

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

            if( onTriggerEnterEvent != null )
                onTriggerEnterEvent( col );
        }


        public void OnTriggerStay2D( Collider2D col )
        {
            if( onTriggerStayEvent != null )
                onTriggerStayEvent( col );
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

            if( onTriggerExitEvent != null )
                onTriggerExitEvent( col );
        }

        [System.Diagnostics.Conditional( "DEBUG_CC2D_RAYS" )]
        void DrawRay( Vector3 start, Vector3 dir, Color color )
        {
            Debug.DrawRay( start, dir, color );
        }

        /// <summary>
        /// attempts to move the character to position + deltaMovement. Any colliders in the way will cause the movement to
        /// stop when run into.
        /// </summary>
        /// <param name="deltaMovement">Delta movement.</param>
        public void move( Vector3 deltaMovement )
        {
            // save off our current grounded state which we will use for wasGroundedLastFrame and becameGroundedThisFrame
            collisionState.wasGroundedLastFrame = collisionState.below;

            // clear our state
            collisionState.reset();
            _raycastHitsThisFrame.Clear();
            _isGoingUpSlope = false;

            primeRaycastOrigins();


            // first, we check for a slope below us before moving
            // only check slopes if we are going down and grounded
            if( deltaMovement.y < 0f && collisionState.wasGroundedLastFrame )
                handleVerticalSlope( ref deltaMovement );

            // now we check movement in the horizontal dir
            if( deltaMovement.x != 0f )
                moveHorizontally( ref deltaMovement );

            // next, check movement in the vertical dir
            if( deltaMovement.y != 0f )
                moveVertically( ref deltaMovement );

            // move then update our state
            deltaMovement.z = 0;
            transform.Translate( deltaMovement, Space.World );

            // only calculate velocity if we have a non-zero deltaTime
            if( Time.deltaTime > 0f )
                velocity = deltaMovement / Time.deltaTime;

            // set our becameGrounded state based on the previous and current collision state
            if( !collisionState.wasGroundedLastFrame && collisionState.below )
                collisionState.becameGroundedThisFrame = true;

            // if we are going up a slope we artificially set a y velocity so we need to zero it out here
            if( _isGoingUpSlope )
                velocity.y = 0;

            // send off the collision events if we have a listener
            if( onControllerCollidedEvent != null )
            {
                for( var i = 0; i < _raycastHitsThisFrame.Count; i++ )
                    onControllerCollidedEvent( _raycastHitsThisFrame[i] );
            }

            ignoreOneWayPlatformsThisFrame = false;
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
            platformMask = 0;

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
                move(Vector3.zero);
            }

            // go to freeroam immediately, otherwise let the killplayer logic handle possible end condition
            if (result.Contains("Nobody was evicted.")) 
            {
                CmdChangeState(GameState.Freeroam);
            }
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

                    move(m);
    //                rigidbody2d.velocity = move;
                    rigidbody2d.freezeRotation = true;
                    gameObject.transform.rotation = Quaternion.identity;

                    // START TASK

                    // check if pressing one, were are standing on valid task that has not yet been completed
                    if (Input.GetKey("1") && standingOnTaskNum >= 0 && assignedTasks.Contains(standingOnTaskNum) && !completedTasks.Contains(standingOnTaskNum) && state == GameState.Freeroam && isLanittaja)
                    {
                        doingTask = true;
                        LoadTask();
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

       	#region Public


        /// <summary>
        /// moves directly down until grounded
        /// </summary>
        public void warpToGrounded()
        {
            do
            {
                move( new Vector3( 0, -1f, 0 ) );
            } while( !isGrounded );
        }


        /// <summary>
        /// this should be called anytime you have to modify the BoxCollider2D at runtime. It will recalculate the distance between the rays used for collision detection.
        /// It is also used in the skinWidth setter in case it is changed at runtime.
        /// </summary>
        public void recalculateDistanceBetweenRays()
        {
            // figure out the distance between our rays in both directions
            // horizontal
            var colliderUseableHeight = boxCollider.size.y * Mathf.Abs( transform.localScale.y ) - ( 2f * _skinWidth );
            _verticalDistanceBetweenRays = colliderUseableHeight / ( totalHorizontalRays - 1 );

            // vertical
            var colliderUseableWidth = boxCollider.size.x * Mathf.Abs( transform.localScale.x ) - ( 2f * _skinWidth );
            _horizontalDistanceBetweenRays = colliderUseableWidth / ( totalVerticalRays - 1 );
        }

        #endregion


        #region Movement Methods

        /// <summary>
        /// resets the raycastOrigins to the current extents of the box collider inset by the skinWidth. It is inset
        /// to avoid casting a ray from a position directly touching another collider which results in wonky normal data.
        /// </summary>
        /// <param name="futurePosition">Future position.</param>
        /// <param name="deltaMovement">Delta movement.</param>
        void primeRaycastOrigins()
        {
            // our raycasts need to be fired from the bounds inset by the skinWidth
            var modifiedBounds = boxCollider.bounds;
            modifiedBounds.Expand( -2f * _skinWidth );

            _raycastOrigins.topLeft = new Vector2( modifiedBounds.min.x, modifiedBounds.max.y );
            _raycastOrigins.bottomRight = new Vector2( modifiedBounds.max.x, modifiedBounds.min.y );
            _raycastOrigins.bottomLeft = modifiedBounds.min;
        }


        /// <summary>
        /// we have to use a bit of trickery in this one. The rays must be cast from a small distance inside of our
        /// collider (skinWidth) to avoid zero distance rays which will get the wrong normal. Because of this small offset
        /// we have to increase the ray distance skinWidth then remember to remove skinWidth from deltaMovement before
        /// actually moving the player
        /// </summary>
        void moveHorizontally( ref Vector3 deltaMovement )
        {
            var isGoingRight = deltaMovement.x > 0;
            var rayDistance = Mathf.Abs( deltaMovement.x ) + _skinWidth;
            var rayDirection = isGoingRight ? Vector2.right : -Vector2.right;
            var initialRayOrigin = isGoingRight ? _raycastOrigins.bottomRight : _raycastOrigins.bottomLeft;

            for( var i = 0; i < totalHorizontalRays; i++ )
            {
                var ray = new Vector2( initialRayOrigin.x, initialRayOrigin.y + i * _verticalDistanceBetweenRays );

                DrawRay( ray, rayDirection * rayDistance, Color.red );

                // if we are grounded we will include oneWayPlatforms only on the first ray (the bottom one). this will allow us to
                // walk up sloped oneWayPlatforms
                if( i == 0 && collisionState.wasGroundedLastFrame )
                    _raycastHit = Physics2D.Raycast( ray, rayDirection, rayDistance, platformMask );
                else
                    _raycastHit = Physics2D.Raycast( ray, rayDirection, rayDistance, platformMask & ~oneWayPlatformMask );

                if( _raycastHit )
                {
                    // the bottom ray can hit a slope but no other ray can so we have special handling for these cases
                    if( i == 0 && handleHorizontalSlope( ref deltaMovement, Vector2.Angle( _raycastHit.normal, Vector2.up ) ) )
                    {
                        _raycastHitsThisFrame.Add( _raycastHit );
                        // if we weren't grounded last frame, that means we're landing on a slope horizontally.
                        // this ensures that we stay flush to that slope
                        if ( !collisionState.wasGroundedLastFrame )
                        {
                            float flushDistance = Mathf.Sign( deltaMovement.x ) * ( _raycastHit.distance - skinWidth );
                            transform.Translate( new Vector2( flushDistance, 0 ) );
                        }
                        break;
                    }

                    // set our new deltaMovement and recalculate the rayDistance taking it into account
                    deltaMovement.x = _raycastHit.point.x - ray.x;
                    rayDistance = Mathf.Abs( deltaMovement.x );

                    // remember to remove the skinWidth from our deltaMovement
                    if( isGoingRight )
                    {
                        deltaMovement.x -= _skinWidth;
                        collisionState.right = true;
                    }
                    else
                    {
                        deltaMovement.x += _skinWidth;
                        collisionState.left = true;
                    }

                    _raycastHitsThisFrame.Add( _raycastHit );

                    // we add a small fudge factor for the float operations here. if our rayDistance is smaller
                    // than the width + fudge bail out because we have a direct impact
                    if( rayDistance < _skinWidth + kSkinWidthFloatFudgeFactor )
                        break;
                }
            }
        }


        /// <summary>
        /// handles adjusting deltaMovement if we are going up a slope.
        /// </summary>
        /// <returns><c>true</c>, if horizontal slope was handled, <c>false</c> otherwise.</returns>
        /// <param name="deltaMovement">Delta movement.</param>
        /// <param name="angle">Angle.</param>
        bool handleHorizontalSlope( ref Vector3 deltaMovement, float angle )
        {
            // disregard 90 degree angles (walls)
            if( Mathf.RoundToInt( angle ) == 90 )
                return false;

            // if we can walk on slopes and our angle is small enough we need to move up
            if( angle < slopeLimit )
            {
                // we only need to adjust the deltaMovement if we are not jumping
                // TODO: this uses a magic number which isn't ideal! The alternative is to have the user pass in if there is a jump this frame
                if( deltaMovement.y < jumpingThreshold )
                {
                    // apply the slopeModifier to slow our movement up the slope
                    var slopeModifier = slopeSpeedMultiplier.Evaluate( angle );
                    deltaMovement.x *= slopeModifier;

                    // we dont set collisions on the sides for this since a slope is not technically a side collision.
                    // smooth y movement when we climb. we make the y movement equivalent to the actual y location that corresponds
                    // to our new x location using our good friend Pythagoras
                    deltaMovement.y = Mathf.Abs( Mathf.Tan( angle * Mathf.Deg2Rad ) * deltaMovement.x );
                    var isGoingRight = deltaMovement.x > 0;

                    // safety check. we fire a ray in the direction of movement just in case the diagonal we calculated above ends up
                    // going through a wall. if the ray hits, we back off the horizontal movement to stay in bounds.
                    var ray = isGoingRight ? _raycastOrigins.bottomRight : _raycastOrigins.bottomLeft;
                    RaycastHit2D raycastHit;
                    if( collisionState.wasGroundedLastFrame )
                        raycastHit = Physics2D.Raycast( ray, deltaMovement.normalized, deltaMovement.magnitude, platformMask );
                    else
                        raycastHit = Physics2D.Raycast( ray, deltaMovement.normalized, deltaMovement.magnitude, platformMask & ~oneWayPlatformMask );

                    if( raycastHit )
                    {
                        // we crossed an edge when using Pythagoras calculation, so we set the actual delta movement to the ray hit location
                        deltaMovement = (Vector3)raycastHit.point - ray;
                        if( isGoingRight )
                            deltaMovement.x -= _skinWidth;
                        else
                            deltaMovement.x += _skinWidth;
                    }

                    _isGoingUpSlope = true;
                    collisionState.below = true;
                    collisionState.slopeAngle = -angle;
                }
            }
            else // too steep. get out of here
            {
                deltaMovement.x = 0;
            }

            return true;
        }


        void moveVertically( ref Vector3 deltaMovement )
        {
            var isGoingUp = deltaMovement.y > 0;
            var rayDistance = Mathf.Abs( deltaMovement.y ) + _skinWidth;
            var rayDirection = isGoingUp ? Vector2.up : -Vector2.up;
            var initialRayOrigin = isGoingUp ? _raycastOrigins.topLeft : _raycastOrigins.bottomLeft;

            // apply our horizontal deltaMovement here so that we do our raycast from the actual position we would be in if we had moved
            initialRayOrigin.x += deltaMovement.x;

            // if we are moving up, we should ignore the layers in oneWayPlatformMask
            var mask = platformMask;
            if( ( isGoingUp && !collisionState.wasGroundedLastFrame ) || ignoreOneWayPlatformsThisFrame )
                mask &= ~oneWayPlatformMask;

            for( var i = 0; i < totalVerticalRays; i++ )
            {
                var ray = new Vector2( initialRayOrigin.x + i * _horizontalDistanceBetweenRays, initialRayOrigin.y );

                DrawRay( ray, rayDirection * rayDistance, Color.red );
                _raycastHit = Physics2D.Raycast( ray, rayDirection, rayDistance, mask );
                if( _raycastHit )
                {
                    // set our new deltaMovement and recalculate the rayDistance taking it into account
                    deltaMovement.y = _raycastHit.point.y - ray.y;
                    rayDistance = Mathf.Abs( deltaMovement.y );

                    // remember to remove the skinWidth from our deltaMovement
                    if( isGoingUp )
                    {
                        deltaMovement.y -= _skinWidth;
                        collisionState.above = true;
                    }
                    else
                    {
                        deltaMovement.y += _skinWidth;
                        collisionState.below = true;
                    }

                    _raycastHitsThisFrame.Add( _raycastHit );

                    // this is a hack to deal with the top of slopes. if we walk up a slope and reach the apex we can get in a situation
                    // where our ray gets a hit that is less then skinWidth causing us to be ungrounded the next frame due to residual velocity.
                    if( !isGoingUp && deltaMovement.y > 0.00001f )
                        _isGoingUpSlope = true;

                    // we add a small fudge factor for the float operations here. if our rayDistance is smaller
                    // than the width + fudge bail out because we have a direct impact
                    if( rayDistance < _skinWidth + kSkinWidthFloatFudgeFactor )
                        break;
                }
            }
        }


        /// <summary>
        /// checks the center point under the BoxCollider2D for a slope. If it finds one then the deltaMovement is adjusted so that
        /// the player stays grounded and the slopeSpeedModifier is taken into account to speed up movement.
        /// </summary>
        /// <param name="deltaMovement">Delta movement.</param>
        private void handleVerticalSlope( ref Vector3 deltaMovement )
        {
            // slope check from the center of our collider
            var centerOfCollider = ( _raycastOrigins.bottomLeft.x + _raycastOrigins.bottomRight.x ) * 0.5f;
            var rayDirection = -Vector2.up;

            // the ray distance is based on our slopeLimit
            var slopeCheckRayDistance = _slopeLimitTangent * ( _raycastOrigins.bottomRight.x - centerOfCollider );

            var slopeRay = new Vector2( centerOfCollider, _raycastOrigins.bottomLeft.y );
            DrawRay( slopeRay, rayDirection * slopeCheckRayDistance, Color.yellow );
            _raycastHit = Physics2D.Raycast( slopeRay, rayDirection, slopeCheckRayDistance, platformMask );
            if( _raycastHit )
            {
                // bail out if we have no slope
                var angle = Vector2.Angle( _raycastHit.normal, Vector2.up );
                if( angle == 0 )
                    return;

                // we are moving down the slope if our normal and movement direction are in the same x direction
                var isMovingDownSlope = Mathf.Sign( _raycastHit.normal.x ) == Mathf.Sign( deltaMovement.x );
                if( isMovingDownSlope )
                {
                    // going down we want to speed up in most cases so the slopeSpeedMultiplier curve should be > 1 for negative angles
                    var slopeModifier = slopeSpeedMultiplier.Evaluate( -angle );
                    // we add the extra downward movement here to ensure we "stick" to the surface below
                    deltaMovement.y += _raycastHit.point.y - slopeRay.y - skinWidth;
                    deltaMovement = new Vector3( 0, deltaMovement.y, 0 ) +
                                    ( Quaternion.AngleAxis( -angle, Vector3.forward ) * new Vector3( deltaMovement.x * slopeModifier, 0, 0 ) );
                    collisionState.movingDownSlope = true;
                    collisionState.slopeAngle = angle;
                }
            }
        }

    	#endregion
    }   
}
