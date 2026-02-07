using UnityEngine;

namespace Skeletom.BattleStation.Server
{
	public abstract class BaseUnityEndpoint : MonoBehaviour, IEndpoint
	{

		[SerializeField]
		protected string _path;
		public string Path => this._path;

		public abstract EndpointResponse ProcessRequest(EndpointRequest request);

		private void OnEnable()
		{
			IServer selfServer = GetComponent<IServer>();
			IServer parentServer = GetComponentInParent<IServer>();
			if (selfServer != null)
			{
				selfServer.RegisterEndpoint(this);
			}
			else if (parentServer != null)
			{
				parentServer.RegisterEndpoint(this);
			}
		}

		private void OnDisable()
		{
			IServer selfServer = GetComponent<IServer>();
			IServer parentServer = GetComponentInParent<IServer>();
			if (selfServer != null)
			{
				selfServer.UnregisterEndpoint(this);
			}
			else if (parentServer != null)
			{
				parentServer.UnregisterEndpoint(this);
			}
		}
	}
}
