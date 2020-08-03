using Microsoft.Graphics.Canvas;
using SalvageSpace.Code.GameManager.Universe.JoshWorldGen;
using SalvageSpace.Faction_Implementation;
using SalvageSpace.Universe;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;

namespace SalvageSpace.Data.Other
{
    class cSystemGen
    {
        cSystem system;

        public int astCanvasTileSize => 4 * aiUniverse.sectorGridLen;
        public int icebergCanvasTileSize => 4 * aiUniverse.sectorGridLen;
        public int warpFieldCanvasTileSize => 4 * aiUniverse.sectorGridLen;
        public int spawnAreaCanvasTileSize => 4 * aiUniverse.sectorGridLen;
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

        public cSystemGen(cSystem system)
        {
            this.system = system;
        }


        //Seamless updates

        public static List<cSystem> todoGenerateBitmaps = new List<cSystem>();
        public static List<cSystem> todoKnitting = new List<cSystem>();
        public static Task currentKnittingTask = null;
        public static cSystem currentTaskSystem = null;
        public static void UpdateSeamlessTiles()
        {

            if ((currentKnittingTask == null || currentKnittingTask.IsCompleted) && todoKnitting.Count > 0)
            {
                cSystem sys = todoKnitting[0];
                todoKnitting.RemoveAt(0);
                sys.gen.reGenerateCanvasBmps = false;

                var task = new Task(() => { KnitSystem(sys); });
                task.Start();
                currentKnittingTask = task;
                currentTaskSystem = sys;
            }

        }

        public static void KnitSystem(cSystem sys)
        {
            aiMapGen.CheckAndMergeNeigborSystems(sys);
            var GM = sys.GM;
            todoGenerateBitmaps.Add(sys);
            currentTaskSystem = null;
        }

    }
}
