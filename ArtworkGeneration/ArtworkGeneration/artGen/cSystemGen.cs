using Microsoft.Graphics.Canvas;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;

namespace ArtworkGeneration.artGen
{
    class cSystemGen
    {

        public int astCanvasTileSize => (int)(0.25 * totalCanvasTileSize);
        public int icebergCanvasTileSize => (int)(0.25 * totalCanvasTileSize);
        public int warpFieldCanvasTileSize => (int)(0.25 * totalCanvasTileSize);
        public int spawnAreaCanvasTileSize => (int)(0.25 * totalCanvasTileSize);
        public static int totalCanvasTileSize = 256;

        public List<Task> loadTasks = new List<Task>();

        public List<PriorityObject> freeUOPoints = null;
        public bool spawnedTokensAndUOs = false;

        //Heatmap
        public Color[,] thisHeatmap = null;
        public Color[,] tlHeatmap = null;
        public Color[,] tHeatmap = null;
        public Color[,] trHeatmap = null;
        public Color[,] lHeatmap = null;
        public Color[,] rHeatmap = null;
        public Color[,] blHeatmap = null;
        public Color[,] bHeatmap = null;
        public Color[,] brHeatmap = null;

        public bool givenTop = false;
        public bool receivedTop = false;

        //Total image
        public bool reGenerateCanvasBmps = true;
        public CanvasBitmap totalCanvasBmp;
        public Color[] totalBytes;

        //Asteroid heatmap
        public CanvasBitmap astCanvasBmp;
        public Color[,] astColors;
        public Color[] astBytes;

        //cloud1 heatmap
        public CanvasBitmap icebergCanvasBmp;
        public Color[,] icebergColors;
        public Color[] icebergBytes;

        //WarpField heatmap
        public CanvasBitmap warpFieldCanvasBmp;
        public Color[,] warpFieldColors;
        public Color[] warpFieldBytes;

        //Spawn areas
        public CanvasBitmap spawnAreaCanvasBmp;
        public Color[,] spawnAreaColors;
        public Color[] spawnAreaBytes;

        public cSystemGen()
        {
        }



    }
}
