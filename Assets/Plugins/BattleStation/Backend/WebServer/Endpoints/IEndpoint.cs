namespace Skeletom.BattleStation.Server
{
	public interface IEndpoint
	{
		string Path { get; }
		EndpointResponse ProcessRequest(EndpointRequest request);
	}

	[System.Serializable]
	public class EndpointRequest
	{
		public readonly IEndpoint endpoint;
		public readonly QueryParameter[] queryParameters;
		public readonly string body;
		public readonly string clientId;

		public EndpointRequest(IEndpoint endpoint, QueryParameter[] queryParameters, string body, string clientId)
		{
			this.endpoint = endpoint;
			this.queryParameters = queryParameters;
			this.body = body;
			this.clientId = clientId;
		}
	}

	[System.Serializable]
	public class QueryParameter
	{
		public readonly string key;
		public readonly string value;
		public QueryParameter(string key, string value)
		{
			this.key = key;
			this.value = value;
		}
	}

	[System.Serializable]
	public class EndpointResponse
	{
		public string body;
		public int status;
		public EndpointResponse(int status, string body)
		{
			this.status = status;
			this.body = body;
		}
	}
}

