namespace LocalLlmAssistant.Services.Rag;

public static class VectorMath
{
    public static double Dot(IReadOnlyList<double> a, IReadOnlyList<double> b)
        => Enumerable.Zip(a, b, (x, y) => x * y).Sum();

    public static double Norm(IReadOnlyList<double> a)
        => Math.Sqrt(a.Sum(x => x * x));

    public static double Cosine(IReadOnlyList<double> a, IReadOnlyList<double> b)
    {
        var na = Norm(a); var nb = Norm(b);
        if (na == 0 || nb == 0) return 0.0;
        return Dot(a, b) / (na * nb);
    }
}
