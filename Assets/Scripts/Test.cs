using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using QFramework;
using GameFish;
using UnityEngine.Pool;

public struct MoveComponent
{
    public Vector3 Pos;
}

public class Test : MonoBehaviour
{
    private ResLoader _resLoader = ResLoader.Allocate();

    public Transform FishParent ;
    public Transform BulletParent ;

    // Start is called before the first frame update
    void Start()
    {
        ResKit.Init();
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.M))
        {
            FishGameLauncher.BulletFactory?.SpawnById(1, new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0), new Vector3(0,0,Random.Range(-180,180)), BulletParent);
        }
        

        
        if (Input.GetKeyDown(KeyCode.B))
        {
            for (int i = 0; i < 100; i++)
            {
                FishEntity fish = FishGameLauncher.FishFactory?.SpawnById(Random.Range(1,3), Vector3.zero, new Vector3(Random.Range(0, 360), Random.Range(0, 360), Random.Range(0, 360)), FishParent);
            }
        }


        if (Input.GetKeyDown(KeyCode.V))
        {
            FishGameLauncher.FishFactory?.RecycleAll();
        }
    }

    void OnDestroy()
    {
        _resLoader.Recycle2Cache();
        _resLoader = null;
    }
}