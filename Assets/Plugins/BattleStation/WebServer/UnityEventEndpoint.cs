using UnityEngine;
using UnityEngine.Events;

public class UnityEventEndpoint : BaseUnityEndpoint
{

	[System.Serializable]
	public class WebServerPostEvent : UnityEvent<string> { }
	[SerializeField]
	public WebServerPostEvent callback;

	public override IResponseArgs ProcessRequest(IRequestArgs request)
	{
		string body = request.Body;
		Debug.Log(body);
		try
		{
			callback.Invoke(body);
			return new UnityEventResponse(200, JsonUtility.ToJson(new ResponseMessage()));
		}
		catch (System.Exception e)
		{
			return new UnityEventResponse(500, e.Message);
		}
	}

	[System.Serializable]
	public class ResponseMessage
	{
		public string message = "OK!";
	}

	[System.Serializable]
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
