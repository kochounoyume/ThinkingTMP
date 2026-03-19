namespace ThinkingTMP.Core;

/// <summary>
/// Converts a binary (rasterized) glyph bitmap into a Signed Distance Field (SDF).
///
/// Algorithm: 8-point Sequential Signed Euclidean Distance Transform (8SSEDT).
/// Two passes (forward + backward) propagate nearest-boundary information across
/// the entire bitmap in O(width × height) time.
///
/// Output byte encoding (matches TextMeshPro convention):
///   255 = fully inside, far from boundary
///   128 = on the boundary
///     0 = fully outside, far from boundary
/// </summary>
public static class SdfGenerator
{
    // Large sentinel value representing "infinite distance".
    private const float Inf = 1e9f;

    /// <summary>
    /// Generates an SDF image from a binary glyph bitmap.
    /// </summary>
    /// <param name="bitmap">
    ///   Rasterized glyph: <c>true</c> = inside (foreground), <c>false</c> = outside (background).
    ///   Layout: row-major, top-left origin, length = <paramref name="width"/> × <paramref name="height"/>.
    /// </param>
    /// <param name="width">Bitmap width in pixels.</param>
    /// <param name="height">Bitmap height in pixels.</param>
    /// <param name="spread">
    ///   Maximum distance (in pixels) that is encoded.  Values beyond this are clamped to 0 or 255.
    /// </param>
    /// <returns>
    ///   Grayscale byte array (same dimensions as input) with SDF values in [0, 255].
    /// </returns>
    public static byte[] Generate(bool[] bitmap, int width, int height, float spread)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        if (bitmap.Length != width * height)
            throw new ArgumentException("Bitmap length must equal width × height.", nameof(bitmap));
        if (spread <= 0f) throw new ArgumentOutOfRangeException(nameof(spread));

        // Compute inside distance (+ direction) and outside distance (− direction) separately,
        // then combine into a signed distance field.
        // distToOutside[i] = distance from pixel i to nearest background (false) pixel
        // distToInside[i]  = distance from pixel i to nearest foreground (true)  pixel
        float[] distToOutside = ComputeEdt(bitmap, width, height, foreground: false);
        float[] distToInside  = ComputeEdt(bitmap, width, height, foreground: true);

        byte[] result = new byte[width * height];
        for (int i = 0; i < result.Length; i++)
        {
            bool inside = bitmap[i];
            // Signed distance: positive inside the glyph (distance to boundary),
            // negative outside the glyph.
            float signed = inside ? distToOutside[i] : -distToInside[i];
            // normalise to [0, 1] then scale to [0, 255]
            float normalised = 0.5f + 0.5f * signed / spread;
            result[i] = (byte)Math.Clamp((int)(normalised * 255f + 0.5f), 0, 255);
        }
        return result;
    }

    // -------------------------------------------------------------------------
    // Euclidean Distance Transform (EDT) via the 8SSEDT two-pass algorithm.
    // Computes, for every pixel, the Euclidean distance to the nearest pixel
    // whose inside-ness equals <paramref name="foreground"/>.
    // -------------------------------------------------------------------------
    private static float[] ComputeEdt(bool[] bitmap, int width, int height, bool foreground)
    {
        // We store squared distances and take the square root at the very end.
        float[] dist = new float[width * height];
        // nearest[i] = (nearestX, nearestY) encoded as nearestY * width + nearestX
        int[] nearestX = new int[width * height];
        int[] nearestY = new int[width * height];

        // Initialise
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int idx = y * width + x;
                if (bitmap[idx] == foreground)
                {
                    dist[idx] = 0f;
                    nearestX[idx] = x;
                    nearestY[idx] = y;
                }
                else
                {
                    dist[idx] = Inf;
                    nearestX[idx] = -1;
                    nearestY[idx] = -1;
                }
            }
        }

        // Forward pass (top-left → bottom-right): neighbours NW, N, NE, W
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int idx = y * width + x;
                TryUpdate(dist, nearestX, nearestY, idx, x, y, x - 1, y - 1, width, height);
                TryUpdate(dist, nearestX, nearestY, idx, x, y, x,     y - 1, width, height);
                TryUpdate(dist, nearestX, nearestY, idx, x, y, x + 1, y - 1, width, height);
                TryUpdate(dist, nearestX, nearestY, idx, x, y, x - 1, y,     width, height);
            }
        }

        // Backward pass (bottom-right → top-left): neighbours SE, S, SW, E
        for (int y = height - 1; y >= 0; y--)
        {
            for (int x = width - 1; x >= 0; x--)
            {
                int idx = y * width + x;
                TryUpdate(dist, nearestX, nearestY, idx, x, y, x + 1, y + 1, width, height);
                TryUpdate(dist, nearestX, nearestY, idx, x, y, x,     y + 1, width, height);
                TryUpdate(dist, nearestX, nearestY, idx, x, y, x - 1, y + 1, width, height);
                TryUpdate(dist, nearestX, nearestY, idx, x, y, x + 1, y,     width, height);
            }
        }

        // Convert squared distances to Euclidean distances
        for (int i = 0; i < dist.Length; i++)
        {
            if (dist[i] >= Inf)
                dist[i] = Inf;
            else
                dist[i] = MathF.Sqrt(dist[i]);
        }

        return dist;
    }

    private static void TryUpdate(
        float[] dist, int[] nearestX, int[] nearestY,
        int idx, int x, int y,
        int nx, int ny,
        int width, int height)
    {
        if (nx < 0 || nx >= width || ny < 0 || ny >= height) return;
        int nIdx = ny * width + nx;
        if (dist[nIdx] >= Inf) return;

        int qx = nearestX[nIdx];
        int qy = nearestY[nIdx];
        if (qx < 0) return;

        float dx = x - qx;
        float dy = y - qy;
        float d2 = dx * dx + dy * dy;
        if (d2 < dist[idx])
        {
            dist[idx] = d2;
            nearestX[idx] = qx;
            nearestY[idx] = qy;
        }
    }
}
