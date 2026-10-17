namespace SoftThorn.MAL.ApiClient.Authentication
{
    /// <summary>
    /// A step of <see cref="ILoginClient.StartOAuthFlowAsync"/>, reported through the <see cref="IProgress{T}"/> passed to it.
    /// </summary>
    /// <param name="Message">A short English description of the current step, suitable for a status line.</param>
    /// <param name="Progress">How far the login has got, from 0 to 1. It reaches 1 when the login succeeded or failed.</param>
    public readonly record struct AuthProgress(string Message, double Progress);
}
