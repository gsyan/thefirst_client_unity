// SplitMix64 기반 결정론적 PRNG(클라 전용) — 같은 seed면 항상 같은 결과를 내는 시드 기반 난수열이 필요한 곳(적 함대 생성 등)에 사용
// System.Random은 실행 환경에 따라 내부 알고리즘이 보장되지 않아 대신 직접 구현.
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

    // [0,1) 균등분포 — NextUInt64()의 상위 53비트를 double 정밀도로 정규화
    private double NextDouble01()
    {
        return (NextUInt64() >> 11) * (1.0 / 9007199254740992.0);
    }

    // 표준정규분포(평균0, 표준편차1) 샘플 1개 — Box-Muller 변환
    public double NextGaussian()
    {
        double u1 = NextDouble01();
        if (u1 <= 0.0) u1 = double.Epsilon; // Log(0) 방지
        double u2 = NextDouble01();
        return System.Math.Sqrt(-2.0 * System.Math.Log(u1)) * System.Math.Cos(2.0 * System.Math.PI * u2);
    }
}
