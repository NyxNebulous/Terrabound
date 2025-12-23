namespace Terrabound.Runtime.Utilities
{
    public readonly struct Result<T>
    {
        public bool Success { get; }
        public T Value { get; }
        public string Error { get; }

        private Result(bool success, T value, string error)
        {
            Success = success;
            Value = value;
            Error = error;
        }

        public static Result<T> Ok(T value) => new Result<T>(true, value, null);
        public static Result<T> Fail(string error) => new Result<T>(false, default, error);

        public override string ToString() => Success ? $"Ok: {Value}" : $"Fail: {Error}";
    }
}