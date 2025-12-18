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

		public override IResponseArgs ProcessRequest(IRequestArgs request)
		{
			string body = request.Body;
			try
			{
				OnPost.Invoke(body);
				return new UnityEventResponse(200, JsonUtility.ToJson(new ResponseMessage()));
			}
			catch (Exception e)
			{
				return new UnityEventResponse(500, e.Message);
			}
		}

		[Serializable]
		public class ResponseMessage
		{
			public string message = "OK!";
		}

		[Serializable]
		public class UnityEventResponse : IResponseArgs
		{
			public string Body { get; private set; }
			public int Status { get; private set; }

			public UnityEventResponse(int status, string body)
			{
				Status = status;
				Body = body;
			}
		}
	}
}
