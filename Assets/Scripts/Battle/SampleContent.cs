using BlackHole.Core;

namespace BlackHole.Sample
{
    public static class SampleContent
    {
        public static ContentData Create() => new ContentData
        {
            Session = new SessionData { TimeLimit = 14 },
        };
    }
}
