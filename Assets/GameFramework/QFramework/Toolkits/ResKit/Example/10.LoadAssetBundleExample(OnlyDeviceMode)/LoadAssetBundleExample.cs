using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace QFramework.Example
{
    public class LoadAssetBundleExample : MonoBehaviour
    {
        // Start is called before the first frame update
        void Start()
        {
            ResKit.Init();

            var resLoader = ResLoader.Allocate();

            var ab = resLoader.LoadSync<AssetBundle>("squareabc_png");
            
            ab.LogInfo();
            
            resLoader.Recycle2Cache();

        }

        // Update is called once per frame
        void Update()
        {

        }
    }
}