using Unity.Netcode;
using UnityEngine;
using System.Collections;
public class PlayerSpawn : NetworkBehaviour
{
    public GameObject SpawnOrange;
    public GameObject SpawnGreen;

    public GameObject Player;
    //public GameObject PlayerModel;

    private void Start()
    {
        if (SpawnOrange == null)
            SpawnOrange = GameObject.Find("Spawn Orange");
        if (SpawnGreen == null)
            SpawnGreen = GameObject.Find("Spawn Green");
        if (Player == null)
            Player = this.gameObject;

        if (IsServer)
        {
            StartCoroutine(DelayedSpawn());
        }
    }
    private IEnumerator DelayedSpawn()
    {
        yield return new WaitForSeconds(1f);
        // Ensure this only runs for this player object
        if (!IsServer) yield break;
        // Host is usually first client (clientId = 0)
        if (OwnerClientId == 0)
        {
            Player.transform.position = SpawnOrange.transform.position;
            //PlayerModel.transform.rotation = Quaternion.Euler(0, 180f, 0);
        }
        else
        {
            Player.transform.position = SpawnGreen.transform.position;
        }
    }
}