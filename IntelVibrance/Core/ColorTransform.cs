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

            // Map 0..1 → a contrast/S-curve parameter
            // At vibrance=0.5 → gamma=1.0 (neutral/linear)
            // At vibrance=1.0 → higher contrast S-curve (vivid)
            // At vibrance=0.0 → gamma-compressed (muted/faded)

            // We use a smooth S-curve approach:
            // gamma controls the curve shape
            // contrast controls the slope around midpoint

            double contrast;   // [-1, 1] where 0 = neutral
            double gamma;      // power curve exponent

            if (vibrance >= 0.5)
            {
                // Above neutral: increase vibrance
                double t = (vibrance - 0.5) * 2.0; // 0..1
                contrast = t * 0.45;                 // 0..0.45 contrast boost
                gamma = 1.0 - t * 0.15;              // slight gamma pull (0.85..1.0)
            }
            else
            {
                // Below neutral: decrease vibrance (mute)
                double t = (0.5 - vibrance) * 2.0; // 0..1
                contrast = -t * 0.35;               // negative contrast
                gamma = 1.0 + t * 0.25;             // gamma push (1.0..1.25)
            }

            for (int i = 0; i < 256; i++)
            {
                double x = i / 255.0; // normalized 0..1

                // Step 1: Apply power gamma curve
                double y = Math.Pow(x, gamma);

                // Step 2: Apply S-curve contrast
                // S-curve: sigmoid-like around midpoint 0.5
                y = ApplySCurveContrast(y, contrast);

                // Step 3: Clamp and convert to 16-bit ramp value
                y = Math.Max(0.0, Math.Min(1.0, y));
                ushort val = (ushort)(y * 65535.0);

                ramp.Red[i] = val;
                ramp.Green[i] = val;
                ramp.Blue[i] = val;
            }

            // Ensure monotonicity (Windows requirement)
            EnsureMonotonic(ramp.Red);
            EnsureMonotonic(ramp.Green);
            EnsureMonotonic(ramp.Blue);

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
