﻿using System.Linq;
using SerdesNet;

namespace UAlbion.Formats.Assets
{
    public class WaveLib
    {
        WaveLibSample[] _headers;
        WaveLib() {}
        public static WaveLib Serdes(WaveLib w, ISerializer s)
        {
            w ??= new WaveLib();
            s.Begin();
            w._headers ??= new WaveLibSample[512];
            s.List(nameof(w._headers), w._headers, 512, WaveLibSample.Serdes);

            foreach (var header in w._headers.Where(x => x.IsValid != -1))
                header.Samples = s.ByteArray(nameof(header.Samples), header.Samples, (int)header.Length);

            s.End();
            return w;
        }

        public ISample GetSample(int instrument) => _headers.FirstOrDefault(x => x.Instrument == instrument);
    }
}
