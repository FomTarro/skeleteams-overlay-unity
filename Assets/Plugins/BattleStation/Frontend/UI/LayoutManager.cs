using System.Collections;
using System.Collections.Generic;
using Skeletom.BattleStation.Server;
using Skeletom.Essentials.Lifecycle;
using UnityEngine;

namespace Skeletom.BattleStation {
    public class LayoutManager : Singleton<LayoutManager>
    {
        [SerializeField]
        private WebServer _webServer;

        [SerializeField]
        private List<Layout> _layouts;
        
        public override void Initialize()
        {
            throw new System.NotImplementedException();
        }
    }
}
