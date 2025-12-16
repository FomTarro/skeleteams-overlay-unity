public interface IEndpoint
{
	string Path { get; }
	IResponseArgs ProcessRequest(IRequestArgs request);
}

public interface IRequestArgs
{
	IEndpoint Endpoint { get; }
	IQueryParameter[] QueryParameters { get; }
	string Body { get; }
	string ClientID { get; }
}

public interface IQueryParameter
{
	string Key { get; }
	string Value { get; }
}

public interface IResponseArgs
{
	string Body { get; }
	int Status { get; }
}

