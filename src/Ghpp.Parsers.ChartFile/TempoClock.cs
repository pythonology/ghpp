namespace Ghpp.Parsers.ChartFile;

/// <summary>Tick-to-seconds conversion via a sorted tempo map.</summary>
internal sealed class TempoClock
{
    private const double DefaultBpm = 120.0;

    private readonly int _resolution;
    private readonly List<Tokenizer.Tempo> _tempos;
    private readonly double[] _cumulativeSeconds;

    public TempoClock(int resolution, List<Tokenizer.Tempo> sortedTempos, double offsetSeconds)
    {
        _resolution = resolution;
        _tempos = new List<Tokenizer.Tempo>(sortedTempos.Count + 1);
        if (sortedTempos.Count == 0 || sortedTempos[0].Tick != 0)
        {
            _tempos.Add(new Tokenizer.Tempo(0, DefaultBpm));
        }
        _tempos.AddRange(sortedTempos);

        _cumulativeSeconds = new double[_tempos.Count];
        _cumulativeSeconds[0] = offsetSeconds;
        for (var i = 1; i < _tempos.Count; i++)
        {
            var previous = _tempos[i - 1];
            var current = _tempos[i];
            var secondsPerTick = 60.0 / (previous.Bpm * _resolution);
            _cumulativeSeconds[i] = _cumulativeSeconds[i - 1] + (current.Tick - previous.Tick) * secondsPerTick;
        }
    }

    public double TickToSeconds(int tick)
    {
        var index = 0;
        for (var i = _tempos.Count - 1; i >= 0; i--)
        {
            if (_tempos[i].Tick > tick)
            {
                continue;
            }
            index = i;
            break;
        }
        var tempoEvent = _tempos[index];
        var secondsPerTick = 60.0 / (tempoEvent.Bpm * _resolution);
        return _cumulativeSeconds[index] + (tick - tempoEvent.Tick) * secondsPerTick;
    }
}
