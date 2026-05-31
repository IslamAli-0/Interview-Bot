namespace TelegramInterviewBot.Services;

public static class EmbeddingHelper
{
    public static byte[] ToBytes(float[] values)
    {
        if (values.Length == 0)
        {
            return Array.Empty<byte>();
        }

        var bytes = new byte[values.Length * sizeof(float)];
        Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    public static float[] FromBytes(byte[] bytes)
    {
        if (bytes.Length == 0)
        {
            return Array.Empty<float>();
        }

        var values = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
        return values;
    }

    public static double CosineSimilarity(ReadOnlySpan<float> left, ReadOnlySpan<float> right)
    {
        if (left.Length == 0 || right.Length == 0 || left.Length != right.Length)
        {
            return 0;
        }

        double dot = 0;
        double leftMag = 0;
        double rightMag = 0;

        for (var i = 0; i < left.Length; i++)
        {
            var l = left[i];
            var r = right[i];
            dot += l * r;
            leftMag += l * l;
            rightMag += r * r;
        }

        var denom = Math.Sqrt(leftMag) * Math.Sqrt(rightMag);
        if (denom <= 0)
        {
            return 0;
        }

        return dot / denom;
    }

    public static bool IsTooSimilar(float[] candidate, IEnumerable<byte[]> existingEmbeddings, double threshold)
    {
        foreach (var bytes in existingEmbeddings)
        {
            var existing = FromBytes(bytes);
            if (existing.Length == 0)
            {
                continue;
            }

            var similarity = CosineSimilarity(candidate, existing);
            if (similarity >= threshold)
            {
                return true;
            }
        }

        return false;
    }
}
