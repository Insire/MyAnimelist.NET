using MyAnimelist.NET.Authentication;
using System.Collections.Concurrent;

namespace MyAnimelist.NET.Tests.OAuth
{
    internal sealed class AuthTestProgressCollector : IProgress<AuthProgress>
    {
        private readonly ConcurrentStack<AuthProgress> _stack;

        public AuthTestProgressCollector()
        {
            _stack = new ConcurrentStack<AuthProgress>();
        }

        public void Report(AuthProgress value)
        {
            _stack.Push(value);
        }

        public IReadOnlyCollection<AuthProgress> AllProgress()
            => _stack.ToArray();
    }
}