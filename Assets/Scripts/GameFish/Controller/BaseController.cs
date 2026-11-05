using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using QFramework;

namespace GameFish
{
    public class BaseController : MonoBehaviour , IController
    {
        public IArchitecture GetArchitecture()
        {
            return GameFishArchitecture.Interface;
        }
    }
}