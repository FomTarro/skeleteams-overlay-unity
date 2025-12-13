using System.Collections;
using System.Collections.Generic;
using Klak.Spout;
using UnityEngine;

public class SpoutSourceManager : TextureSource
{
    [SerializeField]
    private SpoutReceiver _prefab;

    public override void FromSaveData(RouteData data)
    {
        foreach (TextureRouter.SerializedTextureRoute route in data.routes)
        {
            SpoutReceiver receiver = Instantiate(_prefab);
            receiver.sourceName = route.source;
        }
    }

    public override RouteData ToSaveData()
    {
        throw new System.NotImplementedException();
    }
}
