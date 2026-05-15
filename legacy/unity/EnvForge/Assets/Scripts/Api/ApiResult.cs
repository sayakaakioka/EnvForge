public class ApiResult<T>
{
    public bool IsSuccess { get; }
    public T Data { get; }
    public string Error { get; }
    public string RawBody { get; }

    private ApiResult(bool isSuccess, T data, string error, string rawBody)
    {
        IsSuccess = isSuccess;
        Data = data;
        Error = error;
        RawBody = rawBody;
    }

    public static ApiResult<T> Success(T data, string rawBody)
    {
        return new ApiResult<T>(true, data, "", rawBody);
    }

    public static ApiResult<T> Failure(string error, string rawBody)
    {
        return new ApiResult<T>(false, default(T), error, rawBody);
    }
}
