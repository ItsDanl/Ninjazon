namespace Ninjazon.Runtime.Content
{
    public static class SegmentRules
    {
        private const int ValidRouteMask = 0b111;

        public static bool AreCompatible(int exitOpenRoutes, int entryOpenRoutes)
        {
            return ((exitOpenRoutes & entryOpenRoutes) & ValidRouteMask) != 0;
        }
    }
}

