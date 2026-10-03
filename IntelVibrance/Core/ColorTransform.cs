using System;
using IntelVibrance.Windows;

namespace IntelVibrance.Core
{
    /// <summary>
    /// Computes gamma ramp LUTs that approximate Digital Vibrance.
    ///
    /// ALGORITHM EXPLANATION
    /// ─────────────────────
    /// True digital vibrance (as in NVIDIA driver) operates at the display
    /// driver level using a color transformation matrix applied to the
    /// YCbCr or RGB color space before the signal reaches the display.
    ///
    /// On Intel hardware without a public driver API, we simulate this using
    /// the Windows gamma ramp (SetDeviceGammaRamp). While gamma ramps are
    /// per-channel LUTs (not a full color matrix), we can approximate
    /// saturation boost by:
    ///
    ///   1. Analytically computing what a saturation matrix does to R/G/B values
    ///   2. Mapping those per-channel curves into the ramp LUT
    ///
    /// Saturation Matrix (standard luminance-weighted):
    ///   The grayscale luminance of a pixel is: L = 0.2126*R + 0.7152*G + 0.0722*B
    ///   At saturation s=1.0 (normal), output = input.
    ///   At saturation s=0.0, output = grayscale (R=G=B=L).
    ///   At saturation s>1.0, colors become more vivid.
    ///
    /// Limitation:
    ///   A 1D per-channel gamma ramp CANNOT encode a full 3x3 color matrix.
    ///   The ramp only changes how each channel maps to itself.
    ///   For example, ramp.Red[128] = value means "input red 128 → output value".
    ///   Cross-channel terms (e.g., "how much green adds to red") are not possible.
    ///
    /// What we CAN do with gamma ramp for vibrance simulation:
    ///   - Apply an S-curve or power curve to each channel
    ///   - This increases contrast which is perceptually similar to vibrance
    ///     at moderate levels (50–80%)
    ///   - This is the same technique used by many "vibrance" tools when
    ///     no driver API is available
    ///
    /// HONEST ASSESSMENT:
    ///   The gamma ramp approach produces a visible change to the whole display
    ///   and affects desktop, applications, videos, and borderless-windowed games.
    ///   However, it is NOT identical to NVIDIA Digital Vibrance:
    ///   - NVIDIA: Full color matrix (cross-channel saturation boost)
    ///   - This app: Per-channel S-curve (perceptual vibrance simulation)
    ///   The effect is visible and useful, but not driver-level color matrix.
    ///
    /// SLIDER MAPPING:
    ///   0%   → Desaturated / muted (low S-curve contrast)
    ///   50%  → Default / neutral (linear ramp, no change)
    ///   75%  → Noticeably more vivid
    ///   100% → Maximum supported vibrance
    /// </summary>
    public static class ColorTransform
    {
        /// <summary>
        /// Build a gamma ramp that simulates Digital Vibrance.
        /// vibrance: 0.0 (min) to 1.0 (max). Normal/default is 0.5.
        /// </summary>
        public static DisplayAPI.RAMP BuildVibranceRamp(double vibrance)
        {
            vibrance = Math.Max(0.0, Math.Min(1.0, vibrance));

            var ramp = new DisplayAPI.RAMP(true);

            // ─────────────────────────────────────────────────────────────────
            // VIBRANCE via DIFFERENTIAL CHANNEL BOOSTING
            // ─────────────────────────────────────────────────────────────────
            //
            // A true saturation matrix works by computing luminance:
            //   L = 0.2126*R + 0.7152*G + 0.0722*B
            // then lerping: out = L + sat * (input - L)
            //
            // For a gamma ramp (per-channel only), we can't do cross-channel
            // math directly. Instead we apply a channel-specific S-curve strength
            // based on that channel's luminance weight:
            //   - Red   (luma weight 0.2126): boost more → richer reds/blues
            //   - Green (luma weight 0.7152): boost least → green is the dominant luma channel
            //   - Blue  (luma weight 0.0722): boost most → blue is the weakest, so we push it hard
            //
            // At vibrance=0.5 → all channels are linear (neutral).
            // At vibrance=1.0 → R/B are boosted strongly, G moderately.
            // At vibrance=0.0 → all channels pulled toward midpoint (desaturation).
            //
            // The differential S-curve between channels is what creates the
            // perceived saturation shift — colors diverge from their gray value.

            // Saturation factor: 0.0 = gray, 1.0 = normal, >1.0 = vivid
            // Map slider 0..1 → saturation 0.0..2.5
            double saturation = vibrance * 2.5; // 0.5 → 1.25 (slight boost at neutral)
            // Remap so that 0.5 slider = exactly 1.0 saturation (neutral)
            saturation = vibrance <= 0.5
                ? vibrance * 2.0               // 0..1.0
                : 1.0 + (vibrance - 0.5) * 3.0; // 1.0..2.5

            // Per-channel S-curve strengths based on inverse luminance weight
            // (lower luma weight → we can push the channel harder without blowing out luminance)
            // R luma=0.2126 → mid strength
            // G luma=0.7152 → least boost (dominant channel)
            // B luma=0.0722 → most boost
            double[] lumaWeights = { 0.2126, 0.7152, 0.0722 };
            // Derive per-channel "aggressiveness": channels that contribute less luma
            // can be pushed more without changing perceived brightness
            double maxWeight = 0.7152;
            double[] channelBoost = new double[3];
            for (int c = 0; c < 3; c++)
                channelBoost[c] = 1.0 - (lumaWeights[c] / maxWeight); // R=0.70, G=0.0, B=0.90

            ushort[][] channels = { ramp.Red, ramp.Green, ramp.Blue };

            for (int c = 0; c < 3; c++)
            {
                // How much differential boost this channel gets
                // At saturation=1.0 → neutral for all
                // At saturation>1.0 → R/B get more S-curve than G
                // At saturation<1.0 → all get pulled flat but R/B pulled harder
                double boost = channelBoost[c];
                double channelSat = saturation;

                // Effective contrast for this channel's S-curve
                double contrast;
                double gamma;

                if (saturation >= 1.0)
                {
                    double t = saturation - 1.0; // 0..1.5
                    t = Math.Min(t, 1.5) / 1.5;   // normalize 0..1
                    // G gets less contrast boost, R/B get more
                    contrast = t * (0.20 + boost * 0.35); // G: 0..0.20, B: 0..0.515
                    gamma = 1.0 - t * (0.05 + boost * 0.12); // mild gamma pull
                }
                else
                {
                    double t = 1.0 - saturation; // 0..1
                    // All channels pull toward flat, R/B pull harder → desaturated
                    contrast = -t * (0.20 + boost * 0.20);
                    gamma = 1.0 + t * (0.10 + boost * 0.15);
                }

                for (int i = 0; i < 256; i++)
                {
                    double x = i / 255.0;

                    // Gamma curve
                    double y = Math.Pow(Math.Max(0, x), gamma);

                    // S-curve contrast
                    y = ApplySCurveContrast(y, contrast);

                    y = Math.Max(0.0, Math.Min(1.0, y));
                    channels[c][i] = (ushort)(y * 65535.0);
                }

                EnsureMonotonic(channels[c]);
            }

            return ramp;
        }

        /// <summary>
        /// Apply an S-curve contrast adjustment to a normalized value.
        /// contrast: -1.0 (flat) to +1.0 (extreme contrast)
        /// </summary>
        private static double ApplySCurveContrast(double x, double contrast)
        {
            if (Math.Abs(contrast) < 0.001) return x;

            // Map contrast to slope factor
            // contrast = 0.4 → slope ≈ 2.33 (moderate S-curve)
            double slope = (1.0 + Math.Abs(contrast) * 3.0);

            if (contrast > 0)
            {
                // Increase contrast: push values away from midpoint
                // Sigmoid-based S-curve
                double t = x - 0.5;
                double curved = t / (Math.Abs(t) * contrast + 1.0 / slope);
                return 0.5 + curved;
            }
            else
            {
                // Decrease contrast: pull values toward midpoint
                double t = x - 0.5;
                double flat = t / slope;
                return 0.5 + flat;
            }
        }

        /// <summary>
        /// Ensure the ramp is monotonically non-decreasing.
        /// Windows rejects non-monotonic ramps (silently returns failure).
        /// </summary>
        private static void EnsureMonotonic(ushort[] channel)
        {
            for (int i = 1; i < 256; i++)
            {
                if (channel[i] < channel[i - 1])
                    channel[i] = channel[i - 1];
            }
        }

        /// <summary>
        /// Build the default neutral ramp (no color transformation).
        /// This is what we restore on exit.
        /// </summary>
        public static DisplayAPI.RAMP BuildNeutralRamp()
        {
            return DisplayAPI.BuildLinearRamp();
        }

        /// <summary>
        /// Validate a vibrance value (0-100 integer).
        /// </summary>
        public static double NormalizeVibrance(int vibrance0to100)
        {
            return Math.Max(0, Math.Min(100, vibrance0to100)) / 100.0;
        }
    }
}
