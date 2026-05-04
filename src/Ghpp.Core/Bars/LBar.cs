using System;
using System.Collections.Generic;
using Ghpp.Core.Aggregation;
using Ghpp.Core.Models;

namespace Ghpp.Core.Bars
{
    /// <summary>
    /// Sustain-difficulty axis. Each note with a non-zero sustain distributes
    /// load across the sample bins covered by [TimeSeconds, TimeSeconds +
    /// SustainSeconds]. Bins where many sustains overlap saturate via tanh,
    /// so e.g. four held strings don't read as 4× one held string.
    /// </summary>
    /// <remarks>
    /// <see cref="Abstractions.Note.SustainSeconds"/> is a single scalar per
    /// chord stack (the parser reduces with max). True per-fret disjointed
    /// sustains are out of scope for this milestone — see TODO.
    /// </remarks>
    internal static class LBar
    {
        public static DifficultyCurve Build(
            IList<NoteContext> notes,
            DifficultyOptions options,
            double startTime,
            int sampleCount)
        {
            var values = new double[sampleCount];
            if (notes.Count == 0 || sampleCount == 0)
            {
                return new DifficultyCurve(values, options.SampleRateHz, startTime);
            }

            // Accumulate raw load per sample by walking each sustain note and
            // adding its weight to every bin overlapping [start, start+sustain].
            // TODO disjointed sustains: when Note exposes per-fret sustain
            // durations, accumulate per-fret instead of using the chord-max.
            var sampleRate = options.SampleRateHz;
            var weight = options.LBarSustainWeight;

            for (var i = 0; i < notes.Count; i++)
            {
                var n = notes[i];
                var sustain = n.Source.SustainSeconds;
                if (sustain <= 0) continue;

                var perBinWeight = n.PlayableUnits * weight;
                n.LBarSustainLoad = perBinWeight;

                var noteStart = n.Source.TimeSeconds;
                var noteEnd = noteStart + sustain;

                var firstBin = (int)Math.Floor((noteStart - startTime) * sampleRate);
                var lastBin = (int)Math.Ceiling((noteEnd - startTime) * sampleRate);
                if (firstBin < 0) firstBin = 0;
                if (lastBin > sampleCount) lastBin = sampleCount;

                for (var b = firstBin; b < lastBin; b++)
                {
                    values[b] += perBinWeight;
                }
            }

            // Density damping per sample: tanh saturation so overlapping
            // sustains don't sum linearly forever.
            var k = options.LBarSaturationK;
            var max = options.LBarMaxLoad;
            for (var i = 0; i < sampleCount; i++)
            {
                values[i] = max * Math.Tanh(values[i] / k);
            }

            return new DifficultyCurve(values, options.SampleRateHz, startTime);
        }
    }
}
