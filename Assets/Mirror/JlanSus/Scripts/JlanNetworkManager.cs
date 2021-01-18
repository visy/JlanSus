using UnityEngine;
using TMPro;

namespace Mirror.JlanSus
{
    [AddComponentMenu("")]
    public class JlanNetworkManager : NetworkManager
    {
        public RectTransform playerSpawn;

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

        public override void OnServerAddPlayer(NetworkConnection conn)
        {
            // add player at correct spawn position
            var s = playerSpawn;
            
            GameObject player = Instantiate(
                playerPrefab, 
                new Vector3(Random.Range(s.rect.xMin, s.rect.xMax), Random.Range(s.rect.yMin, s.rect.yMax), 0) + s.transform.position, 
                Quaternion.identity);

            player.layer = 9; // players layer

            // setup roles
            player.GetComponent<JlanPlayer>().isLanittaja = true;

            NetworkServer.AddPlayerForConnection(conn, player);

//                ball = Instantiate(spawnPrefabs.Find(prefab => prefab.name == "Ball"));
//                NetworkServer.Spawn(ball);
        }

        public override void OnServerDisconnect(NetworkConnection conn)
        {
//            if (ball != null)
//                NetworkServer.Destroy(ball);

            // call base functionality (actually destroys the player)
            base.OnServerDisconnect(conn);
        }
    }
}
