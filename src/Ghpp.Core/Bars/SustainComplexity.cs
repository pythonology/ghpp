using System;
using System.Collections.Generic;
using Ghpp.Core.Abstractions;
using Ghpp.Core.Aggregation;
using Ghpp.Core.Models;

namespace Ghpp.Core.Bars
{
    /// <summary>
    /// Sustain-difficulty axis. Disjointed sustains are first-class: each
    /// held fret with a non-zero sustain distributes <see cref="DifficultyOptions.SustainPerFretWeight"/>
    /// across the sample bins it covers, accumulating independently of any
    /// other simultaneously-held frets. Open-note sustains use
    /// <see cref="DifficultyOptions.SustainOpenWeight"/>.
    /// </summary>
    /// <remarks>
    /// Bins where multiple per-fret sustains overlap accumulate higher raw
    /// load, then saturate via tanh, so e.g. four held strings don't read as
    /// 4× one held string. A chord with one fret released early naturally
    /// shows reduced load past the early drop-off, while the still-held
    /// frets continue to contribute.
    /// </remarks>
    internal static class SustainComplexity
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

            var sampleRate = options.SampleRateHz;
            var perFretWeight = options.SustainPerFretWeight;
            var openWeight = options.SustainOpenWeight;

            for (var i = 0; i < notes.Count; i++)
            {
                var n = notes[i];
                var note = n.Source;
                var noteStart = note.TimeSeconds;
                var lastLoad = 0.0;

                if (note.IsOpen)
                {
                    if (note.SustainSeconds <= 0) continue;
                    var noteEnd = noteStart + note.SustainSeconds;
                    AccumulateBins(values, sampleCount, sampleRate, startTime, noteStart, noteEnd, openWeight);
                    lastLoad = openWeight;
                }
                else
                {
                    var perFret = note.PerFretSustainSeconds;
                    for (var f = 0; f < 5; f++)
                    {
                        var sustain = perFret[f];
                        if (sustain <= 0) continue;
                        var noteEnd = noteStart + sustain;
                        AccumulateBins(values, sampleCount, sampleRate, startTime, noteStart, noteEnd, perFretWeight);
                        lastLoad += perFretWeight;
                    }
                }

                n.SustainLoad = lastLoad; // diagnostic (sum of weights this note contributed)
            }

            // Density damping per sample: tanh saturation so overlapping
            // sustains don't sum linearly forever.
            var k = options.SustainSaturationK;
            var max = options.SustainMaxLoad;
            for (var i = 0; i < sampleCount; i++)
            {
                values[i] = max * Math.Tanh(values[i] / k);
            }

            return new DifficultyCurve(values, options.SampleRateHz, startTime);
        }

        private static void AccumulateBins(
            double[] values, int sampleCount, double sampleRate, double startTime,
            double noteStart, double noteEnd, double perBinWeight)
        {
            var firstBin = (int)Math.Floor((noteStart - startTime) * sampleRate);
            var lastBin = (int)Math.Ceiling((noteEnd - startTime) * sampleRate);
            if (firstBin < 0) firstBin = 0;
            if (lastBin > sampleCount) lastBin = sampleCount;

            for (var b = firstBin; b < lastBin; b++)
            {
                values[b] += perBinWeight;
            }
        }
    }
}
