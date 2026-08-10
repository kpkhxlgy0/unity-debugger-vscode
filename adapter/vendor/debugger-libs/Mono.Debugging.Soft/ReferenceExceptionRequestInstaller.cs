using System;

namespace Mono.Debugging.Soft
{
	static class ReferenceExceptionRequestInstaller
	{
		public static TRequest[] Install<TRequest> (
			Func<bool, bool, TRequest> create,
			Action<TRequest> enable)
		{
			var unhandled = create (false, true);
			enable (unhandled);
			var caught = create (true, false);
			enable (caught);
			return new[] { unhandled, caught };
		}
	}
}
