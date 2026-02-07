using System;
using UnityEngine;
using UnityEngine.Events;

namespace Skeletom.BattleStation.Server
{
	public class UnityEventEndpoint : BaseUnityEndpoint
	{
		[Serializable]
		public class WebServerPostEvent : UnityEvent<string> { }
		public WebServerPostEvent OnPost;

		public override EndpointResponse ProcessRequest(EndpointRequest request)
		{
			string body = request.body;
			try
			{
				OnPost.Invoke(body);
				return new EndpointResponse(200, JsonUtility.ToJson(new ResponseMessage()));
			}
			catch (Exception e)
			{
				return new EndpointResponse(500, e.Message);
			}
		}

		[Serializable]
		public class ResponseMessage
		{
			public string message = "OK!";
		}
	}
}
