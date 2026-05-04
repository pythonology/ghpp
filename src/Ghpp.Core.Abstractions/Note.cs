using System;
using System.Collections.Generic;

namespace Ghpp.Core.Abstractions
{
    /// <summary>
    /// A single playable note (or chord) on the chart timeline.
    /// </summary>
    public sealed class Note
    {
        private static readonly double[] EmptyPerFret = new double[5];

        public Note(
            int timeTicks,
            double timeSeconds,
            Frets frets,
            int sustainTicks,
            double sustainSeconds,
            NoteType type)
            : this(timeTicks, timeSeconds, frets, sustainTicks, sustainSeconds, type, perFretSustainSeconds: null)
        {
        }

        public Note(
            int timeTicks,
            double timeSeconds,
            Frets frets,
            int sustainTicks,
            double sustainSeconds,
            NoteType type,
            double[] perFretSustainSeconds)
        {
            TimeTicks = timeTicks;
            TimeSeconds = timeSeconds;
            Frets = frets;
            SustainTicks = sustainTicks;
            SustainSeconds = sustainSeconds;
            Type = type;

            if (perFretSustainSeconds == null)
            {
                PerFretSustainSeconds = EmptyPerFret;
            }
            else
            {
                if (perFretSustainSeconds.Length != 5)
                    throw new ArgumentException("perFretSustainSeconds must have length 5 (GRYBO).", nameof(perFretSustainSeconds));
                var copy = new double[5];
                Array.Copy(perFretSustainSeconds, copy, 5);
                PerFretSustainSeconds = copy;
            }
        }

        /// <summary>Position on the chart's tick grid.</summary>
        public int TimeTicks { get; }

        /// <summary>
        /// Position on the chart's tick grid in seconds, with the chart's tempo map and offset already applied.
        /// </summary>
        public double TimeSeconds { get; }

        /// <summary>Frets held for this note. <see cref="Frets.None"/> means open.</summary>
        public Frets Frets { get; }

        /// <summary>
        /// Sustain length in ticks. Chord-max scalar (the parser still tracks
        /// the longest fret in the chord here for tick-based consumers). The
        /// per-fret breakdown lives in <see cref="PerFretSustainSeconds"/>.
        /// </summary>
        public int SustainTicks { get; }

        /// <summary>Chord-max sustain length in seconds. See <see cref="PerFretSustainSeconds"/> for per-fret durations.</summary>
        public double SustainSeconds { get; }

        /// <summary>
        /// Per-fret sustain length in seconds, indexed by GRYBO order
        /// (0=Green..4=Orange). Held frets without a sustain and untouched
        /// frets both report 0. Always length 5.
        /// </summary>
        public IReadOnlyList<double> PerFretSustainSeconds { get; }

        /// <summary>The strum-style classification of the note.</summary>
        public NoteType Type { get; }

        /// <summary><see langword="true"/> when no frets are held.</summary>
        public bool IsOpen => FretCount == 0;

        /// <summary>Number of frets held.</summary>
        public int FretCount
        {
            get
            {
                var count = 0;
                var bits = (byte)Frets;
                while (bits != 0)
                {
                    count += bits & 1;
                    bits >>= 1;
                }
                return count;
            }
        }
    }
}
