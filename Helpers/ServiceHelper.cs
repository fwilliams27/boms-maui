using System;

namespace BOMS
{
    public static class ServiceHelper
    {
        public static IServiceProvider Services { get; private set; } = default!;

        public static void Initialize(IServiceProvider services)
            => Services = services ?? throw new ArgumentNullException(nameof(services));
    }
}
