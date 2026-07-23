// SplitMix64 기반 결정론적 PRNG — C#(클라)과 Java(서버)에서 동일한 seed로 항상 동일한 결과를 내야 함
// System.Random/java.util.Random은 언어별 내부 알고리즘이 달라 같은 seed에서도 다른 값이 나오므로 직접 구현.
// 서버 대응 구현: com.bk.sbs.util.CrossPlatformRandom.java — 두 파일은 항상 함께 수정할 것
public class CrossPlatformRandom
{
    private ulong m_state;

    public CrossPlatformRandom(int seed)
    {
        m_state = unchecked((ulong)seed);
    }

    private ulong NextUInt64()
    {
        m_state += 0x9E3779B97F4A7C15UL;
        ulong z = m_state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    // [0, maxExclusive) 반환 — maxExclusive<=0이면 0
    public int Next(int maxExclusive)
    {
        if (maxExclusive <= 0) return 0;
        return (int)(NextUInt64() % (ulong)maxExclusive);
    }

    // [minInclusive, maxExclusive) 반환 — maxExclusive<=minInclusive면 minInclusive
    public int Next(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive) return minInclusive;
        return minInclusive + Next(maxExclusive - minInclusive);
    }
}
