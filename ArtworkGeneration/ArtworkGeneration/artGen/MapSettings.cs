using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;

namespace ArtworkGeneration.artGen
{
    public class MapSettings
    {
        public int X = 0;
        public int Y = 0;

        public Color[,] outputMap;
        public Color[,] thisHeatmap = null;
        public Color[] totalBytes;
    }
}
